import Foundation

struct MetricRecord: Codable, Equatable, Sendable {
    let display: String
    let dayKey: String
    let timeZone: String
    let account: String?
    let capturedAt: Date

    func isCurrent(at date: Date, in zone: TimeZone) -> Bool {
        timeZone == zone.identifier && dayKey == Self.dayKey(for: date, in: zone)
    }

    static func dayKey(for date: Date, in zone: TimeZone = .current) -> String {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = zone
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter.string(from: date)
    }

    static func menuTitle(for display: String) -> String {
        guard isValidDisplay(display) else { return "𝕏" }
        return "𝕏 " + display
    }

    static func isValidDisplay(_ value: String) -> Bool {
        let pattern = #"\A(?:0|[1-9]\d{0,14}|[1-9]\d{0,2}(?:,\d{3})+)(?:\.\d{1,3})?(?: ?[KMBkmb천만억万億])?\z"#
        return value.range(of: pattern, options: .regularExpression) != nil && value.count <= 24
    }
}

struct PageReading: Codable, Sendable {
    let status: String
    var display: String?
    var dateLabel: String?
    var dayKey: String?
    var timeZone: String?
    var account: String?
    var action: String?
}

enum NavigationPolicy {
    static func isGoogle(_ url: URL) -> Bool {
        let host = url.host?.lowercased() ?? ""
        return host == "accounts.google.com" || host.hasSuffix(".accounts.google.com")
    }

    static func allows(_ url: URL) -> Bool {
        guard url.scheme == "https", let host = url.host?.lowercased(),
              url.user == nil, url.password == nil else { return false }
        let domains = ["x.com", "twitter.com", "accounts.google.com", "appleid.apple.com"]
        return domains.contains { host == $0 || host.hasSuffix("." + $0) }
    }

    static func isOverview(_ url: URL?) -> Bool {
        guard isAnalytics(url), let path = url?.path else { return false }
        return ["/i/account_analytics", "/i/account_analytics/", "/i/account_analytics/overview", "/i/account_analytics/overview/"].contains(path)
    }

    static func isAnalytics(_ url: URL?) -> Bool {
        guard let url, ["x.com", "www.x.com"].contains(url.host?.lowercased() ?? "") else { return false }
        return url.path == "/i/account_analytics" || url.path.hasPrefix("/i/account_analytics/")
    }
}
