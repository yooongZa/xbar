import XCTest
@testable import TodayBarCore

final class MetricRecordTests: XCTestCase {
    func testMidnightAndTimeZoneChangeInvalidateToday() throws {
        let seoul = try XCTUnwrap(TimeZone(identifier: "Asia/Seoul"))
        let utc = try XCTUnwrap(TimeZone(identifier: "UTC"))
        let formatter = ISO8601DateFormatter()
        let before = try XCTUnwrap(formatter.date(from: "2026-10-22T14:59:59Z"))
        let after = try XCTUnwrap(formatter.date(from: "2026-10-22T15:00:00Z"))
        let record = MetricRecord(display: "12.3K", dayKey: "2026-10-22", timeZone: seoul.identifier,
                                  account: "@example", capturedAt: before)
        XCTAssertTrue(record.isCurrent(at: before, in: seoul))
        XCTAssertFalse(record.isCurrent(at: after, in: seoul))
        XCTAssertFalse(record.isCurrent(at: before, in: utc))
    }

    func testDisplayAcceptsZeroAndAbbreviationWithoutInventingPrecision() {
        for text in ["0", "16.3K", "12,345", "1.4M", "1.2만", "999"] {
            XCTAssertTrue(MetricRecord.isValidDisplay(text), text)
        }
        for text in ["", "-75%", "16.3K16.3K", "1,23", "NaN", "-1", "12\n34", "12\n", "1\nK", "1e20", "1234567890123456"] {
            XCTAssertFalse(MetricRecord.isValidDisplay(text), text)
        }
    }

    func testNavigationRejectsLookalikeAndUnsafeOrigins() throws {
        XCTAssertTrue(NavigationPolicy.isGoogle(try XCTUnwrap(URL(string: "https://accounts.google.com/v3/signin/accountchooser"))))
        XCTAssertFalse(NavigationPolicy.isGoogle(try XCTUnwrap(URL(string: "https://accounts.google.com.evil.example/"))))
        for text in ["https://x.com/i/account_analytics", "https://accounts.google.com/signin", "https://api.x.com/"] {
            XCTAssertTrue(NavigationPolicy.allows(try XCTUnwrap(URL(string: text))))
        }
        for text in ["https://x.com.evil.example/", "http://x.com/", "file:///tmp/test", "https://user:pass@x.com/", "https://evilx.com/"] {
            XCTAssertFalse(NavigationPolicy.allows(try XCTUnwrap(URL(string: text))))
        }
        XCTAssertFalse(NavigationPolicy.isAnalytics(URL(string: "https://x.com/i/flow/login")))
        XCTAssertFalse(NavigationPolicy.isAnalytics(URL(string: "https://example.com/i/account_analytics")))
        XCTAssertTrue(NavigationPolicy.isAnalytics(URL(string: "https://x.com/i/account_analytics")))
    }
}
