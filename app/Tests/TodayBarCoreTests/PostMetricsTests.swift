import XCTest
@testable import TodayBarCore

final class PostMetricsTests: XCTestCase {
    let start = Date(timeIntervalSince1970: 1_792_650_000)
    func post(_ id: String = "2111348825839611989", impressions: String = "1,000", rt: String = "10") -> ContentPost {
        ContentPost(id: id, title: "테스트 게시물", impressions: impressions, likes: "5", reposts: rt)
    }

    func testRoundedNumbersAreApproximateAndCannotTriggerRT() {
        XCTAssertEqual(DisplayCount("1.2만")?.value, 12000)
        XCTAssertEqual(DisplayCount("12,345")?.exactInteger, 12345)
        XCTAssertNil(DisplayCount("1K")?.exactInteger)
        XCTAssertNil(DisplayCount("NaN"))
        XCTAssertNil(DisplayCount("999999999999999B"))
        var detector = RepostDetector()
        for i in 0..<4 { XCTAssertNil(detector.observe(post(rt: "\(i+1)K"), at: start.addingTimeInterval(Double(i*600)))) }
    }

    func testAnalyticsLinkIsStrictAndUsesPostID() {
        XCTAssertEqual(post().analyticsURL?.absoluteString, "https://x.com/i/account_analytics/content/2111348825839611989")
        for id in ["../../home", "123?redirect=evil", "https://evil.example", "2111348825839611989\n"] {
            XCTAssertNil(post(id).analyticsURL)
        }
    }

    func testThreeSamplesRequiredAndThresholdIsRelativePlusAbsolute() {
        var detector = RepostDetector()
        XCTAssertNil(detector.observe(post(rt: "10"), at: start))
        XCTAssertNil(detector.observe(post(rt: "11"), at: start.addingTimeInterval(600)))
        let signal = detector.observe(post(rt: "17"), at: start.addingTimeInterval(1200))
        XCTAssertEqual(signal?.delta, 6)
        XCTAssertEqual(signal?.minutes, 10)
        XCTAssertNil(detector.observe(post(rt: "40"), at: start.addingTimeInterval(1800)), "cooldown")
    }

    func testHighSteadyRateAndTinyGrowthDoNotAlert() {
        var detector = RepostDetector()
        XCTAssertNil(detector.observe(post(rt: "10"), at: start))
        XCTAssertNil(detector.observe(post(rt: "20"), at: start.addingTimeInterval(600)))
        XCTAssertNil(detector.observe(post(rt: "35"), at: start.addingTimeInterval(1200)))
        var small = RepostDetector()
        _ = small.observe(post(rt: "0"), at: start)
        _ = small.observe(post(rt: "0"), at: start.addingTimeInterval(600))
        XCTAssertNil(small.observe(post(rt: "4"), at: start.addingTimeInterval(1200)))
    }

    func testManualRefreshDoesNotResetBurstWindow() {
        var detector = RepostDetector()
        _ = detector.observe(post(rt: "10"), at: start)
        _ = detector.observe(post(rt: "10"), at: start.addingTimeInterval(600))
        XCTAssertNil(detector.observe(post(rt: "12"), at: start.addingTimeInterval(660)))
        XCTAssertEqual(detector.observe(post(rt: "16"), at: start.addingTimeInterval(1200))?.delta, 6)
    }

    func testSleepAndDecreasingCountersResetDetection() {
        var detector = RepostDetector()
        _ = detector.observe(post(rt: "10"), at: start)
        _ = detector.observe(post(rt: "11"), at: start.addingTimeInterval(600))
        XCTAssertNil(detector.observe(post(rt: "100"), at: start.addingTimeInterval(3600)))
        XCTAssertNil(detector.observe(post(rt: "101"), at: start.addingTimeInterval(4200)))
        XCTAssertNil(detector.observe(post(rt: "5"), at: start.addingTimeInterval(4800)))
        XCTAssertNil(detector.observe(post(rt: "100"), at: start.addingTimeInterval(5400)))
    }

    func testLongRefreshIntervalDoesNotPretendTenMinutes() {
        var detector = RepostDetector()
        _ = detector.observe(post(rt: "10"), at: start)
        XCTAssertNil(detector.observe(post(rt: "30"), at: start.addingTimeInterval(1800)))
    }

    func testRankingSeparatesLifetimeAndObservedGrowth() {
        var ledger = PostLedger()
        let a = "2111348825839611989", b = "2111348825839611988"
        _ = ledger.ingest([post(a, impressions: "900K"), post(b, impressions: "100")], account: "@one", at: start)
        XCTAssertNil(ledger.topFive(growth: true).first?.increase)
        XCTAssertTrue(ledger.topFive(growth: true).isEmpty, "No fabricated rank before a second observation")
        _ = ledger.ingest([post(a, impressions: "900K"), post(b, impressions: "150")], account: "@one", at: start.addingTimeInterval(600))
        XCTAssertEqual(ledger.topFive(growth: false).first?.id, a)
        XCTAssertEqual(ledger.topFive(growth: true).first?.id, b)
        XCTAssertEqual(ledger.topFive(growth: true).first?.increaseText, "+50")
        XCTAssertTrue(ledger.topFive(growth: false).first?.approximate == true)
    }

    func testAccountDayAndTimeZoneResetAllBaselines() throws {
        var ledger = PostLedger()
        let utc = try XCTUnwrap(TimeZone(identifier: "UTC"))
        _ = ledger.ingest([post()], account: "@one", at: start, zone: utc)
        _ = ledger.ingest([post(impressions: "1100")], account: "@two", at: start.addingTimeInterval(600), zone: utc)
        XCTAssertNil(ledger.rows.first?.increase)
        _ = ledger.ingest([post(impressions: "1200")], account: "@two", at: start.addingTimeInterval(86400), zone: utc)
        XCTAssertNil(ledger.rows.first?.increase)
        _ = ledger.ingest([post(impressions: "1300")], account: "@two", at: start.addingTimeInterval(87000), zone: try XCTUnwrap(TimeZone(identifier: "Asia/Seoul")))
        XCTAssertNil(ledger.rows.first?.increase)
    }

    func testDuplicatesAndInvalidIDsCannotEnterRanking() {
        var ledger = PostLedger()
        _ = ledger.ingest([post(), post(), post("bad")], account: "@one", at: start)
        XCTAssertEqual(ledger.rows.count, 1)
    }

    func testCounterCorrectionDoesNotProduceNegativeGrowth() {
        var ledger = PostLedger()
        _ = ledger.ingest([post()], account: "@one", at: start)
        _ = ledger.ingest([post(impressions: "900")], account: "@one", at: start.addingTimeInterval(600))
        XCTAssertNil(ledger.rows.first?.increase)
    }
    func testPeriodBoundariesAndCalendarDays() throws {
        let utc = try XCTUnwrap(TimeZone(identifier: "UTC"))
        let formatter = ISO8601DateFormatter()
        let newYear = try XCTUnwrap(formatter.date(from: "2027-01-01T01:00:00Z"))
        XCTAssertEqual(ContentPeriod.today.range(at: newYear, zone: utc), ContentDateRange(from: "2027-01-01", to: "2027-01-01"))
        XCTAssertEqual(ContentPeriod.yesterday.range(at: newYear, zone: utc).from, "2026-12-31")
        XCTAssertEqual(ContentPeriod.week.range(at: newYear, zone: utc).from, "2026-12-26")
        let leap = try XCTUnwrap(formatter.date(from: "2028-03-01T01:00:00Z"))
        XCTAssertEqual(ContentPeriod.yesterday.range(at: leap, zone: utc).from, "2028-02-29")
        let dst = try XCTUnwrap(formatter.date(from: "2026-03-09T07:30:00Z"))
        let la = try XCTUnwrap(TimeZone(identifier: "America/Los_Angeles"))
        XCTAssertEqual(ContentPeriod.yesterday.range(at: dst, zone: la).from, "2026-03-08")
        let seoul = try XCTUnwrap(TimeZone(identifier: "Asia/Seoul"))
        let late = try XCTUnwrap(formatter.date(from: "2026-10-22T16:00:00Z"))
        XCTAssertEqual(ContentPeriod.today.range(at: late, zone: seoul).to, "2026-10-23")
        XCTAssertEqual(ContentPeriod.today.range(at: late, zone: utc).to, "2026-10-22")
        let url = ContentPeriod.week.range(at: newYear, zone: utc).url
        XCTAssertEqual(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == "from" }?.value, "2026-12-26")
    }

    func testDisplayTitlePreservesFullTextForLayoutAndAccessibility() {
        let title = "가나다라마바사아자차카타파하👨‍👩‍👧‍👦끝"
        let p = ContentPost(id: post().id, title: title, impressions: "10", likes: "0", reposts: "0")
        XCTAssertEqual(p.displayTitle, title)
        XCTAssertEqual(p.title, title)
        XCTAssertEqual(post().displayTitle, post().title)
        XCTAssertTrue(p.isValid)
        XCTAssertFalse(ContentPost(id: p.id, title: title, impressions: "10", likes: "--", reposts: "0").isValid)
    }

}
