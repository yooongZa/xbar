import XCTest
@testable import TodayBarCore

final class ImpressionSurgeTests: XCTestCase {
    let start = Date(timeIntervalSince1970: 1_792_650_000)
    func post(_ impressions: String, id: String = "2111348825839611989", rt: String = "0") -> ContentPost {
        ContentPost(id: id, title: "Test", impressions: impressions, likes: "0", reposts: rt)
    }

    func testImpressionBurstTriggersWithZeroReposts() {
        var detector = ImpressionDetector()
        XCTAssertTrue(detector.observe([post("100")], at: start).isEmpty)
        let events = detector.observe([post("1100")], at: start.addingTimeInterval(600))
        XCTAssertEqual(events.first?.delta, 1000)
        XCTAssertEqual(events.first?.minutes, 10)
        XCTAssertEqual(events.first?.approximate, false)
        var repostOnly = ImpressionDetector()
        _ = repostOnly.observe([post("100")], at: start)
        XCTAssertTrue(repostOnly.observe([post("100", rt: "500")], at: start.addingTimeInterval(600)).isEmpty)
    }

    func testFivefoldGrowthUsesRecentNormalHistory() {
        for delta in [1999, 2000] {
            var detector = ImpressionDetector()
            _ = detector.observe([post("10000")], at: start)
            XCTAssertTrue(detector.observe([post("10400")], at: start.addingTimeInterval(600)).isEmpty)
            let events = detector.observe([post(String(10400 + delta))], at: start.addingTimeInterval(1200))
            XCTAssertEqual(!events.isEmpty, delta == 2000)
        }
    }

    func testPeerReferenceIsIndependentOfPostOrder() {
        let ids = ["2111348825839611989", "2111348825839611988", "2111348825839611987"]
        for order in [[0, 1, 2], [2, 1, 0]] {
            var detector = ImpressionDetector()
            _ = detector.observe(order.map { post("10000", id: ids[$0]) }, at: start)
            let events = detector.observe(order.map { post($0 == 0 ? "13000" : "10500", id: ids[$0]) }, at: start.addingTimeInterval(600))
            XCTAssertEqual(events.map(\.id), [ids[0]])
        }
    }

    func testSustainedBurstKeepsNormalBaselineAndThirtyMinuteCooldown() {
        var detector = ImpressionDetector()
        let other = "2111348825839611988"
        _ = detector.observe([post("10000"), post("10000", id: other)], at: start)
        for tick in 1...4 {
            let events = detector.observe([post(String(10000 + tick * 8000)), post(String(10000 + tick * 200), id: other)], at: start.addingTimeInterval(Double(tick * 600)))
            XCTAssertEqual(events.count, tick == 1 || tick == 4 ? 1 : 0)
        }
    }

    func testRoundedImpressionsAllowLargeGrowthButIgnoreDisplayBoundary() {
        var detector = ImpressionDetector()
        _ = detector.observe([post("868K")], at: start)
        XCTAssertTrue(detector.observe([post("869K")], at: start.addingTimeInterval(600)).isEmpty)
        let event = detector.observe([post("879K")], at: start.addingTimeInterval(1200)).first
        XCTAssertEqual(event?.delta, 11000)
        XCTAssertEqual(event?.minutes, 20)
        XCTAssertEqual(event?.approximate, true)
        var millions = ImpressionDetector()
        _ = millions.observe([post("1.0M")], at: start)
        XCTAssertTrue(millions.observe([post("1.1M")], at: start.addingTimeInterval(600)).isEmpty)
        XCTAssertEqual(millions.observe([post("1.4M")], at: start.addingTimeInterval(1200)).count, 1)
    }

    func testCoarseCountsAccumulateAcrossConsecutiveCaptures() {
        var detector = ImpressionDetector()
        _ = detector.observe([post("1.0M")], at: start)
        XCTAssertTrue(detector.observe([post("1.1M")], at: start.addingTimeInterval(600)).isEmpty)
        XCTAssertTrue(detector.observe([post("1.2M")], at: start.addingTimeInterval(1200)).isEmpty)
        let event = detector.observe([post("1.3M")], at: start.addingTimeInterval(1800)).first
        XCTAssertEqual(event?.delta, 300000)
        XCTAssertEqual(event?.minutes, 30)
    }

    func testCoarseAccumulationResetsAfterGapsCorrectionsAndOneHour() {
        var gap = ImpressionDetector()
        _ = gap.observe([post("1.0M")], at: start)
        _ = gap.observe([post("1.1M")], at: start.addingTimeInterval(600))
        XCTAssertTrue(gap.observe([post("1.4M")], at: start.addingTimeInterval(1501)).isEmpty)
        var correction = ImpressionDetector()
        _ = correction.observe([post("1.0M")], at: start)
        _ = correction.observe([post("1.2M")], at: start.addingTimeInterval(600))
        _ = correction.observe([post("1.1M")], at: start.addingTimeInterval(1200))
        XCTAssertTrue(correction.observe([post("1.3M")], at: start.addingTimeInterval(1800)).isEmpty)
        var quiet = ImpressionDetector()
        for tick in 0...6 {
            XCTAssertTrue(quiet.observe([post("1.0M")], at: start.addingTimeInterval(Double(tick * 600))).isEmpty)
        }
        _ = quiet.observe([post("1.1M")], at: start.addingTimeInterval(4200))
        _ = quiet.observe([post("1.2M")], at: start.addingTimeInterval(4800))
        XCTAssertEqual(quiet.observe([post("1.3M")], at: start.addingTimeInterval(5400)).first?.minutes, 30)
    }

    func testManualRefreshKeepsTheFiveMinuteObservationWindow() {
        var detector = ImpressionDetector()
        _ = detector.observe([post("100")], at: start)
        XCTAssertTrue(detector.observe([post("1100")], at: start.addingTimeInterval(60)).isEmpty)
        XCTAssertTrue(detector.observe([post("1100")], at: start.addingTimeInterval(240)).isEmpty)
        XCTAssertEqual(detector.observe([post("1100")], at: start.addingTimeInterval(300)).first?.minutes, 5)
    }

    func testGrowthRateUsesElapsedTimeAndResetsAfterLongGaps() {
        for (seconds, delta, expected) in [(300.0, 500, true), (600.0, 999, false),
                                           (600.0, 1000, true), (900.0, 1499, false),
                                           (900.0, 1500, true), (901.0, 100000, false)] {
            var detector = ImpressionDetector()
            _ = detector.observe([post("10000")], at: start)
            let events = detector.observe([post(String(10000 + delta))], at: start.addingTimeInterval(seconds))
            XCTAssertEqual(!events.isEmpty, expected, "\(delta) impressions over \(seconds) seconds")
        }
    }

    func testCounterCorrectionAndClockRollbackStartNewBaselines() {
        var detector = ImpressionDetector()
        _ = detector.observe([post("10000")], at: start)
        XCTAssertTrue(detector.observe([post("100")], at: start.addingTimeInterval(600)).isEmpty)
        XCTAssertTrue(detector.observe([post("1100")], at: start.addingTimeInterval(599)).isEmpty)
        XCTAssertTrue(detector.observe([post("1150")], at: start.addingTimeInterval(1199)).isEmpty)
    }

    func testCooldownSurvivesLeavingListAndAllowsASecondObservationOnReturn() {
        var detector = ImpressionDetector()
        _ = detector.observe([post("10000")], at: start)
        XCTAssertEqual(detector.observe([post("20000")], at: start.addingTimeInterval(600)).count, 1)
        _ = detector.observe([], at: start.addingTimeInterval(660))
        XCTAssertTrue(detector.observe([post("50000")], at: start.addingTimeInterval(720)).isEmpty)
        XCTAssertTrue(detector.observe([post("60000")], at: start.addingTimeInterval(1020)).isEmpty)
        _ = detector.observe([], at: start.addingTimeInterval(1800))
        XCTAssertTrue(detector.observe([post("70000")], at: start.addingTimeInterval(2100)).isEmpty)
        XCTAssertEqual(detector.observe([post("80000")], at: start.addingTimeInterval(2400)).count, 1)
    }

    func testAccountAndDayChangesDiscardImpressionHistory() {
        var ledger = PostLedger()
        let zone = TimeZone(secondsFromGMT: 0)!
        _ = ledger.ingest([post("100")], account: "@one", at: start, zone: zone)
        XCTAssertEqual(ledger.ingest([post("1100")], account: "@one", at: start.addingTimeInterval(600), zone: zone).count, 1)
        XCTAssertTrue(ledger.ingest([post("100000")], account: "@two", at: start.addingTimeInterval(1200), zone: zone).isEmpty)
        XCTAssertTrue(ledger.ingest([post("200000")], account: "@two", at: start.addingTimeInterval(86400), zone: zone).isEmpty)
    }
}
