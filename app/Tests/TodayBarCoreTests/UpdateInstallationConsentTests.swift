import XCTest
@testable import TodayBarCore

final class UpdateInstallationConsentTests: XCTestCase {
    func testBackgroundDownloadAndPresentationRequireInstallChoice() {
        var consent = UpdateInstallationConsent()
        XCTAssertFalse(consent.consumeInstallRequest())
        _ = consent.begin()
        XCTAssertFalse(consent.consumeInstallRequest())
    }

    func testExplicitInstallIsConsumedOnce() {
        var consent = UpdateInstallationConsent()
        let session = consent.begin()
        consent.record(install: true, for: session)
        XCTAssertTrue(consent.consumeInstallRequest())
        XCTAssertFalse(consent.consumeInstallRequest())
    }

    func testSkipOrDismissDoesNotInstall() {
        var consent = UpdateInstallationConsent()
        let session = consent.begin()
        consent.record(install: false, for: session)
        XCTAssertFalse(consent.consumeInstallRequest())
    }

    func testCancelRejectsLateInstallCallback() {
        var consent = UpdateInstallationConsent()
        let session = consent.begin()
        consent.record(install: true, for: session)
        consent.invalidate(session: session)
        consent.record(install: true, for: session)
        XCTAssertFalse(consent.consumeInstallRequest())
    }

    func testNewPresentationCannotInheritInstallChoice() {
        var consent = UpdateInstallationConsent()
        let old = consent.begin()
        consent.record(install: true, for: old)
        _ = consent.begin()
        consent.record(install: true, for: old)
        XCTAssertFalse(consent.consumeInstallRequest())
    }

    func testOldCancellationCannotRevokeCurrentChoice() {
        var consent = UpdateInstallationConsent()
        let old = consent.begin()
        let current = consent.begin()
        consent.record(install: true, for: current)
        consent.invalidate(session: old)
        XCTAssertTrue(consent.consumeInstallRequest())
    }

    func testErrorAndRelaunchDoNotPreserveConsent() {
        var consent = UpdateInstallationConsent()
        let session = consent.begin()
        consent.record(install: true, for: session)
        consent.invalidate()
        XCTAssertFalse(consent.consumeInstallRequest())
        var relaunched = UpdateInstallationConsent()
        XCTAssertFalse(relaunched.consumeInstallRequest())
    }
}
