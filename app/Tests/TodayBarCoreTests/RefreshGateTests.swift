import XCTest
@testable import TodayBarCore

final class RefreshGateTests: XCTestCase {
    private let now = ISO8601DateFormatter().date(from: "2026-10-22T12:00:00Z")!
    private let zone = TimeZone(identifier: "Asia/Seoul")!
    func testRetryBackoffPreventsRedirectLoopsAndResetsAfterRecovery() {
        var retry = RefreshRetry()
        var time = now
        for delay: Double in [30, 60, 120, 240, 300, 300] {
            XCTAssertTrue(retry.begin(at: time))
            XCTAssertEqual(retry.nextAttempt, time.addingTimeInterval(delay))
            XCTAssertFalse(retry.begin(at: time))
            XCTAssertFalse(retry.begin(at: time.addingTimeInterval(delay - 0.1)))
            time = retry.nextAttempt
        }
        retry.reset()
        XCTAssertTrue(retry.begin(at: time))
        XCTAssertEqual(retry.nextAttempt, time.addingTimeInterval(30))
    }
    func testOldLoadAndReadCannotRestoreValueAfterDisconnectOrSleep() {
        var gate = RefreshGate()
        let old = gate.generation
        XCTAssertFalse(gate.accept(old))
        XCTAssertTrue(gate.didLoad(old))
        XCTAssertTrue(gate.accept(old))
        gate.invalidate()
        XCTAssertFalse(gate.verified)
        XCTAssertFalse(gate.canRead(old))
        XCTAssertFalse(gate.didLoad(old))
        XCTAssertFalse(gate.accept(old))
        XCTAssertFalse(gate.accept(gate.generation))
        XCTAssertTrue(gate.didLoad(gate.generation))
        XCTAssertTrue(gate.accept(gate.generation))
    }
    func testFreshnessRequiresCurrentDateAndSuccessfulOnlineVerification() {
        var gate = RefreshGate()
        let record = MetricRecord(display: "24.8K", dayKey: "2026-10-22", timeZone: zone.identifier, account: "@example", capturedAt: now)
        func visible(_ elapsed: Double = 0, online: Bool = true, sleeping: Bool = false, busy: Bool = false, stale: Bool = false, inZone: TimeZone? = nil) -> Bool {
            gate.isFresh(record, now: now.addingTimeInterval(elapsed), zone: inZone ?? zone, intervalMinutes: 10, online: online, sleeping: sleeping, busy: busy, stale: stale)
        }
        XCTAssertFalse(visible())
        XCTAssertTrue(gate.didLoad(gate.generation)); XCTAssertTrue(gate.accept(gate.generation))
        XCTAssertTrue(visible()); XCTAssertTrue(visible(630))
        XCTAssertFalse(visible(631)); XCTAssertFalse(visible(-1))
        XCTAssertFalse(visible(online: false)); XCTAssertFalse(visible(sleeping: true))
        XCTAssertFalse(visible(busy: true)); XCTAssertFalse(visible(stale: true))
        XCTAssertFalse(visible(inZone: TimeZone(identifier: "UTC")))
        XCTAssertFalse(visible(86400))
        gate.invalidate()
        XCTAssertFalse(visible())
    }
}
