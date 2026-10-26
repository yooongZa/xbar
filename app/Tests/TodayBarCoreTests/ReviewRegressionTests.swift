import XCTest
@testable import TodayBarCore

final class ReviewRegressionTests: XCTestCase {
    func testRecoveryNeedsTrustedOriginVerifiedAccountAndSafeCurrentContext() throws {
        var reading = PageReading(status: "off_page", account: "@fixture", canRecover: true)
        for path in ["/notifications", "/notifications/mentions", "/home", "/fixture", "/i/account_analytics/content"] {
            XCTAssertTrue(NavigationPolicy.canRecoverAnalytics(URL(string: "https://x.com" + path), reading: reading))
        }
        for url in ["http://x.com/notifications", "https://x.com.evil.example/notifications", "https://user:pass@x.com/notifications", "https://x.com:444/notifications"] {
            XCTAssertFalse(NavigationPolicy.canRecoverAnalytics(URL(string: url), reading: reading))
        }
        reading.canRecover = false
        XCTAssertFalse(NavigationPolicy.canRecoverAnalytics(URL(string: "https://x.com/notifications"), reading: reading))
        reading.canRecover = true; reading.account = nil
        XCTAssertFalse(NavigationPolicy.canRecoverAnalytics(URL(string: "https://x.com/notifications"), reading: reading))
        XCTAssertTrue(NavigationPolicy.canAutomaticallyRefresh(nil))
        for path in ["/notifications", "/home", "/i/account_analytics"] {
            XCTAssertTrue(NavigationPolicy.canAutomaticallyRefresh(URL(string: "https://x.com" + path)))
        }
        for path in ["/i/flow/login", "/account/access", "/i/jf/onboarding/web", "/settings/password", "/compose/post"] {
            XCTAssertFalse(NavigationPolicy.canAutomaticallyRefresh(URL(string: "https://x.com" + path)))
        }
    }
    func testAggregateOnlyReloadsOverview() {
        for path in ["", "/", "/overview", "/overview/"] {
            XCTAssertTrue(NavigationPolicy.isOverview(URL(string: "https://x.com/i/account_analytics" + path)))
        }
        for path in ["/content", "/content/2111348825839611989", "/audience"] {
            let url = URL(string: "https://x.com/i/account_analytics" + path)
            XCTAssertTrue(NavigationPolicy.isAnalytics(url))
            XCTAssertFalse(NavigationPolicy.isOverview(url))
        }
        XCTAssertFalse(NavigationPolicy.isOverview(URL(string: "https://evil.example/i/account_analytics")))
    }

    func testStableScopeAllowsCountersAndRankToChangeButNotIdentityOrDate() throws {
        let post = ContentPost(id: "2111348825839611989", title: "Test", impressions: "100", likes: "1", reposts: "1")
        let changed = ContentPost(id: post.id, title: "Test", impressions: "110", likes: "2", reposts: "3")
        let range = ContentDateRange(from: "2026-10-23", to: "2026-10-23")
        let before = ContentReading(status: "ready", account: "@fixture", dayKey: range.to, range: range, posts: [post])
        let after = ContentReading(status: "ready", account: "@fixture", dayKey: range.to, range: range, posts: [changed])
        XCTAssertTrue(after.hasSameScopeAndPosts(as: before))
        XCTAssertFalse(after.hasSameScopeAndPosts(as: nil))
        XCTAssertFalse(after.hasSameScopeAndPosts(as: ContentReading(status: "ready", account: "@other", dayKey: range.to, range: range, posts: [post])))
        XCTAssertFalse(after.hasSameScopeAndPosts(as: ContentReading(status: "ready", account: "@fixture", dayKey: "2026-10-22", range: range, posts: [post])))
        XCTAssertFalse(after.hasSameScopeAndPosts(as: ContentReading(status: "ready", account: "@fixture", dayKey: range.to, range: ContentDateRange(from: "2026-10-22", to: range.to), posts: [post])))
        XCTAssertFalse(after.hasSameScopeAndPosts(as: ContentReading(status: "empty", account: "@fixture", dayKey: range.to, range: range, posts: [])))
    }

    func testNewLeaderAndRemovedRowsRecomputeTopFive() {
        var ledger = PostLedger()
        let now = Date()
        func post(_ i: Int, _ n: Int) -> ContentPost {
            ContentPost(id: String(2111348825839611980 + i), title: "Post \(i)", impressions: String(n), likes: "0", reposts: "0")
        }
        let original = (1...6).map { post($0, 100 - $0) }
        _ = ledger.ingest(original, account: "@fixture", at: now)
        XCTAssertEqual(ledger.topFive(growth: false).first?.post.title, "Post 1")
        _ = ledger.ingest(Array(original.prefix(5)) + [post(6, 9999)], account: "@fixture", at: now.addingTimeInterval(600))
        XCTAssertEqual(ledger.topFive(growth: false).first?.post.title, "Post 6")
        XCTAssertEqual(ledger.topFive(growth: true).first?.post.title, "Post 6")
        _ = ledger.ingest([post(6, 10000)], account: "@fixture", at: now.addingTimeInterval(1200))
        XCTAssertEqual(ledger.topFive(growth: false).count, 1)
    }

    func testRTCooldownSurvivesLeavingListAndExpiresAfterThirtyMinutes() {
        var detector = RepostDetector()
        let start = Date(timeIntervalSince1970: 1_792_650_000)
        func observe(_ rt: Int, _ seconds: Double) -> RepostSurge? {
            detector.observe(ContentPost(id: "2111348825839611989", title: "Test", impressions: "100", likes: "0", reposts: String(rt)), at: start.addingTimeInterval(seconds))
        }
        XCTAssertNil(observe(10, 0)); XCTAssertNil(observe(11, 600))
        XCTAssertNotNil(observe(17, 1200))
        detector.retain(ids: [])
        XCTAssertNil(observe(18, 1260)); XCTAssertNil(observe(19, 1560))
        XCTAssertNil(observe(30, 1860), "11 minutes later must remain in cooldown")
        detector.retain(ids: [])
        XCTAssertNil(observe(31, 2460)); XCTAssertNil(observe(32, 2760))
        XCTAssertNotNil(observe(40, 3060), "31 minutes later can alert again after rebuilding samples")
    }

    @MainActor
    func testCancellationRestoresCookieSnapshotAtEveryStorageBoundary() async {
        // Initial read, each delete, and each write, including final write/commit boundary.
        for cancelAt in 1...5 {
            var cookies = ["old-auth", "old-csrf"]
            var calls = 0, committed = false
            let task = Task { @MainActor in
                func boundary() {
                    calls += 1
                    if calls == cancelAt { withUnsafeCurrentTask { $0?.cancel() } }
                }
                return await CookieReplacement.replace(with: ["new-auth", "new-csrf"], read: {
                    boundary(); return cookies
                }, delete: { value in boundary(); cookies.removeAll { $0 == value } },
                write: { value in boundary(); cookies.append(value) }, isCurrent: { true }, commit: { committed = true })
            }
            let result = await task.value
            XCTAssertFalse(result, "boundary \(cancelAt)")
            XCTAssertFalse(committed)
            XCTAssertEqual(cookies, ["old-auth", "old-csrf"], "boundary \(cancelAt)")
        }
    }

    @MainActor
    func testPreCancelledImportNeverReadsOrWrites() async {
        var touched = false
        let task = Task { @MainActor in
            await CookieReplacement.replace(with: ["new"], read: { touched = true; return ["old"] },
                delete: { _ in touched = true }, write: { _ in touched = true }, isCurrent: { true })
        }
        task.cancel()
        let result = await task.value
        XCTAssertFalse(result); XCTAssertFalse(touched)
    }

    @MainActor
    func testSuccessfulImportCommitsOnceAndInvalidatedOperationRollsBack() async {
        for invalidated in [false, true] {
            var cookies = ["old"]
            var current = true, commits = 0
            let result = await CookieReplacement.replace(with: ["new"], read: { cookies },
                delete: { value in cookies.removeAll { $0 == value } },
                write: { value in cookies.append(value); if invalidated { current = false } },
                isCurrent: { current }, commit: { commits += 1 })
            XCTAssertEqual(result, !invalidated)
            XCTAssertEqual(cookies, invalidated ? ["old"] : ["new"])
            XCTAssertEqual(commits, invalidated ? 0 : 1)
        }
    }
}
