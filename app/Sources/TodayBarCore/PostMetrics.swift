import Foundation

enum ContentPeriod: String, CaseIterable, Identifiable, Sendable {
    case today, yesterday, week
    var id: String { rawValue }
    var title: String {
        switch self { case .today: L10n.text("오늘"); case .yesterday: L10n.text("최근 2일"); case .week: L10n.text("최근 7일") }
    }
    var days: Int {
        switch self { case .today: 1; case .yesterday: 2; case .week: 7 }
    }
    func range(at date: Date, zone: TimeZone = .current) -> ContentDateRange {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = zone
        let start = calendar.date(byAdding: .day, value: 1 - days, to: calendar.startOfDay(for: date))!
        return ContentDateRange(from: MetricRecord.dayKey(for: start, in: zone), to: MetricRecord.dayKey(for: date, in: zone))
    }
}

struct ContentDateRange: Codable, Equatable, Sendable {
    let from: String
    let to: String
    var key: String { "\(from)|\(to)" }
    var url: URL {
        var parts = URLComponents(string: "https://x.com/i/account_analytics/content")!
        parts.queryItems = [URLQueryItem(name: "type", value: "posts"), URLQueryItem(name: "sort", value: "impressions"),
            URLQueryItem(name: "dir", value: "desc"), URLQueryItem(name: "from", value: from), URLQueryItem(name: "to", value: to)]
        return parts.url!
    }
}

struct DisplayCount: Equatable, Sendable {
    let value: Double
    let approximate: Bool

    init?(_ text: String) {
        guard MetricRecord.isValidDisplay(text) else { return nil }
        let compact = text.replacingOccurrences(of: ",", with: "").replacingOccurrences(of: " ", with: "")
        let multipliers: [Character: Double] = ["K": 1e3, "k": 1e3, "M": 1e6, "m": 1e6,
            "B": 1e9, "b": 1e9, "천": 1e3, "만": 1e4, "억": 1e8, "万": 1e4, "億": 1e8]
        let multiplier = compact.last.flatMap { multipliers[$0] }
        guard let number = Double(multiplier == nil ? compact : String(compact.dropLast())),
              number.isFinite, number * (multiplier ?? 1) <= 9e15 else { return nil }
        value = number * (multiplier ?? 1)
        approximate = multiplier != nil || compact.contains(".")
    }

    var exactInteger: Int? { approximate ? nil : Int(value) }
}

struct ContentPost: Codable, Equatable, Identifiable, Sendable {
    let id: String
    let title: String
    let impressions: String
    let likes: String
    let reposts: String

    var displayTitle: String { title == "__XBAR_MEDIA_POST__" ? L10n.text("media.post") : title }

    var isValid: Bool {
        id.range(of: #"\A[0-9]{10,25}\z"#, options: .regularExpression) != nil &&
        !title.isEmpty && title.count <= 500 && DisplayCount(impressions) != nil && DisplayCount(likes) != nil && DisplayCount(reposts) != nil
    }
    var analyticsURL: URL? {
        guard isValid else { return nil }
        return URL(string: "https://x.com/i/account_analytics/content/\(id)")
    }
}

struct ContentReading: Decodable, Equatable, Sendable {
    let status: String
    let account: String?
    let dayKey: String?
    let range: ContentDateRange?
    let posts: [ContentPost]?

    // Numeric counters and ranking order can legitimately change while the page settles.
    func hasSameScopeAndPosts(as previous: ContentReading?) -> Bool {
        guard let previous, let posts, let oldPosts = previous.posts else { return false }
        return status == previous.status && account?.lowercased() == previous.account?.lowercased() &&
            dayKey == previous.dayKey && range == previous.range && Set(posts.map(\.id)) == Set(oldPosts.map(\.id))
    }
}

struct RankedPost: Identifiable, Equatable, Sendable {
    let post: ContentPost
    let increase: Double?
    let approximate: Bool
    let observedSince: Date
    var id: String { post.id }
    var increaseText: String {
        guard let increase else { return L10n.text("측정 중") }
        return "\(approximate ? "≈" : "")+\(Int(increase).formatted())"
    }
}

struct RepostSurge: Identifiable, Equatable, Sendable {
    let post: ContentPost
    let delta: Int
    let minutes: Int
    let detectedAt: Date
    var id: String { post.id }
    var label: String { L10n.format("reposts.surge", String(minutes), String(delta)) }
}

/// App-defined signal, not an X viral classification. Only exact repost counters qualify.
struct RepostDetector: Sendable {
    private struct Sample: Sendable {
        var count: Int
        var at: Date
        var previousRate: Double?
    }
    private var samples: [String: Sample] = [:]
    private var lastAlerts: [String: Date] = [:]

    mutating func observe(_ post: ContentPost, at now: Date) -> RepostSurge? {
        lastAlerts = lastAlerts.filter { now.timeIntervalSince($0.value) < 30 * 60 }
        guard let count = DisplayCount(post.reposts)?.exactInteger else {
            samples.removeValue(forKey: post.id)
            return nil
        }
        guard var old = samples[post.id] else {
            samples[post.id] = Sample(count: count, at: now)
            return nil
        }
        let elapsed = now.timeIntervalSince(old.at)
        // A sleep/offline gap or a corrected counter cannot become a burst on return.
        guard elapsed > 0, elapsed <= 15 * 60, count >= old.count else {
            samples[post.id] = Sample(count: count, at: now)
            return nil
        }
        guard elapsed >= 5 * 60 else { return nil } // manual refresh does not move the comparison baseline
        let delta = count - old.count
        let rate = Double(delta) / elapsed * 600
        let qualifies = delta >= 5 && rate >= 5 &&
            old.previousRate.map { rate >= max($0, 1) * 3 } == true &&
            lastAlerts[post.id].map { now.timeIntervalSince($0) >= 30 * 60 } != false
        old.count = count
        old.at = now
        old.previousRate = rate
        if qualifies { lastAlerts[post.id] = now }
        samples[post.id] = old
        return qualifies ? RepostSurge(post: post, delta: delta,
            minutes: Int((elapsed / 60).rounded()), detectedAt: now) : nil
    }

    mutating func retain(ids: Set<String>) { samples = samples.filter { ids.contains($0.key) } }
}

struct PostLedger: Sendable {
    private struct Baseline: Sendable { let count: DisplayCount; let at: Date }
    private var baselines: [String: Baseline] = [:]
    private var detector = RepostDetector()
    private(set) var context: String?
    private(set) var rows: [RankedPost] = []

    mutating func reset() { self = PostLedger() }

    mutating func ingest(_ posts: [ContentPost], account: String, at now: Date, zone: TimeZone = .current) -> [RepostSurge] {
        let key = "\(account.lowercased())|\(MetricRecord.dayKey(for: now, in: zone))|\(zone.identifier)"
        if context != key { reset(); context = key }
        var events: [RepostSurge] = []
        var seen = Set<String>()
        rows = posts.filter { $0.isValid && seen.insert($0.id).inserted }.map { post in
            let count = DisplayCount(post.impressions)!
            var baseline = baselines[post.id]
            if baseline == nil || count.value < baseline!.count.value {
                baseline = Baseline(count: count, at: now)
                baselines[post.id] = baseline
            }
            if let event = detector.observe(post, at: now) { events.append(event) }
            let base = baseline!
            return RankedPost(post: post, increase: now > base.at ? count.value - base.count.value : nil,
                approximate: count.approximate || base.count.approximate, observedSince: base.at)
        }
        // Bounds state and avoids comparing a returning row across an unobserved gap.
        baselines = baselines.filter { seen.contains($0.key) }
        detector.retain(ids: seen)
        return events
    }

    func topFive(growth: Bool) -> [RankedPost] {
        Array(rows.filter { !growth || $0.increase != nil }.sorted {
            let a = growth ? ($0.increase ?? -1) : (DisplayCount($0.post.impressions)?.value ?? -1)
            let b = growth ? ($1.increase ?? -1) : (DisplayCount($1.post.impressions)?.value ?? -1)
            if a != b { return a > b }
            return $0.id > $1.id
        }.prefix(5))
    }
}
