import Foundation

/// Reserve the next retry before starting I/O so redirects and failed loads cannot
/// form a tight reload loop. A verified reading or an explicit refresh resets it.
struct RefreshRetry: Sendable {
    private(set) var nextAttempt = Date.distantPast
    private var attempts = 0

    mutating func begin(at now: Date) -> Bool {
        guard now >= nextAttempt else { return false }
        nextAttempt = now.addingTimeInterval(min(30 * pow(2, Double(min(attempts, 4))), 300))
        attempts += 1
        return true
    }

    mutating func reset() { self = Self() }
}

/// A successful page load is required after every invalidation. Old callbacks and
/// repeated reads of a cached DOM cannot make an invalidated value current again.
struct RefreshGate: Sendable {
    private(set) var generation = 0
    private(set) var loaded = false
    private(set) var verified = false
    mutating func invalidate() { generation += 1; loaded = false; verified = false }
    mutating func didLoad(_ token: Int) -> Bool {
        guard token == generation else { return false }
        loaded = true
        return true
    }
    func canRead(_ token: Int) -> Bool { token == generation && loaded }
    mutating func accept(_ token: Int) -> Bool {
        guard canRead(token) else { return false }
        verified = true
        return true
    }
    func isFresh(_ record: MetricRecord?, now: Date, zone: TimeZone,
                 intervalMinutes: Int, online: Bool, sleeping: Bool, busy: Bool, stale: Bool) -> Bool {
        guard verified, online, !sleeping, !busy, !stale, let record,
              record.isCurrent(at: now, in: zone), MetricRecord.isValidDisplay(record.display) else { return false }
        return (0...Double(intervalMinutes * 60 + 30)).contains(now.timeIntervalSince(record.capturedAt))
    }
}
