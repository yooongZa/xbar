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
    let roundingUnit: Double

    init?(_ text: String) {
        guard MetricRecord.isValidDisplay(text) else { return nil }
        let compact = text.replacingOccurrences(of: ",", with: "").replacingOccurrences(of: " ", with: "")
        let multipliers: [Character: Double] = ["K": 1e3, "k": 1e3, "M": 1e6, "m": 1e6,
            "B": 1e9, "b": 1e9, "천": 1e3, "만": 1e4, "억": 1e8, "万": 1e4, "億": 1e8]
        let multiplier = compact.last.flatMap { multipliers[$0] }
        let numeric = multiplier == nil ? compact : String(compact.dropLast())
        guard let number = Double(numeric),
              number.isFinite, number * (multiplier ?? 1) <= 9e15 else { return nil }
        value = number * (multiplier ?? 1)
        approximate = multiplier != nil || compact.contains(".")
        let decimals = numeric.split(separator: ".").dropFirst().first?.count ?? 0
        roundingUnit = approximate ? (multiplier ?? 1) * pow(10, -Double(decimals)) : 0
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

struct ImpressionSurge: Identifiable, Equatable, Sendable {
    let post: ContentPost
    let delta: Int
    let minutes: Int
    let detectedAt: Date
    let approximate: Bool
    var id: String { post.id }
    var label: String { L10n.format("impressions.surge", String(minutes), "\(approximate ? "≈" : "")\(delta.formatted())") }
}

/// Compares each post's recent impression growth with normal growth observed in this scope.
struct ImpressionDetector: Sendable {
    private struct Sample: Sendable {
        let count: DisplayCount
        let at: Date
        var latestValue: Double
        var latestAt: Date

        init(count: DisplayCount, at: Date) {
            self.count = count
            self.at = at
            latestValue = count.value
            latestAt = at
        }
    }
    private struct Observation: Sendable {
        let post: ContentPost
        let delta: Double
        let rate: Double
        let minimumRate: Double
        let elapsed: TimeInterval
        let approximate: Bool
    }
    private struct NormalRate: Sendable {
        let rate: Double
        let at: Date
    }
    private var samples: [String: Sample] = [:]
    private var history: [NormalRate] = []
    private var lastAlerts: [String: Date] = [:]

    mutating func observe(_ posts: [ContentPost], at now: Date) -> [ImpressionSurge] {
        lastAlerts = lastAlerts.filter { (0..<1800).contains(now.timeIntervalSince($0.value)) }
        history = history.filter { (0..<3600).contains(now.timeIntervalSince($0.at)) }
        var observations: [Observation] = []
        var seen = Set<String>()
        for post in posts where post.isValid && seen.insert(post.id).inserted {
            guard let count = DisplayCount(post.impressions) else { continue }
            guard let old = samples[post.id] else {
                samples[post.id] = Sample(count: count, at: now)
                continue
            }
            let elapsed = now.timeIntervalSince(old.at)
            let gap = now.timeIntervalSince(old.latestAt)
            let approximate = count.approximate || old.count.approximate
            // Sleep, clock changes and corrected counts start a new observation window.
            guard gap > 0, gap <= 900, elapsed <= 3600,
                  elapsed <= 900 || approximate, count.value >= old.latestValue else {
                samples[post.id] = Sample(count: count, at: now)
                continue
            }
            var continued = old
            continued.latestValue = count.value
            continued.latestAt = now
            guard elapsed >= 300 else { samples[post.id] = continued; continue }
            let delta = count.value - old.count.value
            // Reserve one display unit at each endpoint for rounding or truncation.
            let minimumDelta = max(0, delta - count.roundingUnit - old.count.roundingUnit)
            // Keep coarse counts across consecutive captures until growth is measurable.
            // Otherwise 1.0M -> 1.1M -> 1.2M would be discarded on every capture.
            if approximate && minimumDelta == 0 && elapsed < 3600 {
                samples[post.id] = continued
                continue
            }
            observations.append(Observation(post: post, delta: delta, rate: delta / elapsed * 600,
                minimumRate: minimumDelta / elapsed * 600, elapsed: elapsed,
                approximate: approximate))
            samples[post.id] = Sample(count: count, at: now)
        }
        samples = samples.filter { seen.contains($0.key) }
        let normalRate = Self.median(history.map(\.rate))
        var events: [ImpressionSurge] = []
        for observation in observations {
            // Before history exists, other posts in this capture establish the reference.
            let peers = observations.filter { $0.post.id != observation.post.id && $0.minimumRate > 0 }.map(\.rate)
            let reference = normalRate ?? Self.median(peers) ?? 0
            let qualifies = observation.minimumRate >= max(1000, reference * 5)
            if qualifies {
                if lastAlerts[observation.post.id] == nil {
                    lastAlerts[observation.post.id] = now
                    events.append(ImpressionSurge(post: observation.post, delta: Int(observation.delta),
                        minutes: Int((observation.elapsed / 60).rounded()), detectedAt: now,
                        approximate: observation.approximate))
                }
            } else if observation.minimumRate > 0 {
                // A detected burst must not immediately raise the definition of normal.
                history.append(NormalRate(rate: observation.rate, at: now))
            }
        }
        // Twenty posts, observed at most once per five minutes over one hour.
        history = Array(history.suffix(240))
        return events
    }

    private static func median(_ values: [Double]) -> Double? {
        let sorted = values.filter { $0 > 0 }.sorted()
        guard !sorted.isEmpty else { return nil }
        let middle = sorted.count / 2
        return sorted.count.isMultiple(of: 2) ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle]
    }
}

struct PostLedger: Sendable {
    private struct Baseline: Sendable { let count: DisplayCount; let at: Date }
    private var baselines: [String: Baseline] = [:]
    private var detector = ImpressionDetector()
    private(set) var context: String?
    private(set) var rows: [RankedPost] = []

    mutating func reset() { self = PostLedger() }

    mutating func ingest(_ posts: [ContentPost], account: String, at now: Date, zone: TimeZone = .current) -> [ImpressionSurge] {
        let key = "\(account.lowercased())|\(MetricRecord.dayKey(for: now, in: zone))|\(zone.identifier)"
        if context != key { reset(); context = key }
        var seen = Set<String>()
        rows = posts.filter { $0.isValid && seen.insert($0.id).inserted }.map { post in
            let count = DisplayCount(post.impressions)!
            var baseline = baselines[post.id]
            if baseline == nil || count.value < baseline!.count.value {
                baseline = Baseline(count: count, at: now)
                baselines[post.id] = baseline
            }
            let base = baseline!
            return RankedPost(post: post, increase: now > base.at ? count.value - base.count.value : nil,
                approximate: count.approximate || base.count.approximate, observedSince: base.at)
        }
        // Bounds state and avoids comparing a returning row across an unobserved gap.
        baselines = baselines.filter { seen.contains($0.key) }
        return detector.observe(rows.map(\.post), at: now)
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
