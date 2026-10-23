import XCTest
import CryptoKit
@testable import TodayBarCore

final class SessionTransferTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_800_000_000)
    private func cookie(name: String = "auth_token", value: String = "synthetic-test-only", domain: String = ".x.com", path: String = "/", secure: Bool = true, expires: Double? = nil) -> SessionCookie {
        SessionCookie(name: name, value: value, domain: domain, path: path, secure: secure, httpOnly: true, expirationDate: expires)
    }
    func testAuthenticatedRoundTripAndCiphertextDoesNotContainCredential() throws {
        let key = Curve25519.KeyAgreement.PrivateKey()
        let request = TransferRequest(id: UUID(), createdAt: now, publicKey: key.publicKey.rawRepresentation)
        let envelope = try SessionTransfer.seal([cookie()], for: request, now: now)
        XCTAssertFalse(String(decoding: try JSONEncoder().encode(envelope), as: UTF8.self).contains("synthetic-test-only"))
        XCTAssertEqual(try SessionTransfer.open(envelope, request: request, privateKey: key, now: now).first?.value, "synthetic-test-only")
    }
    func testWrongKeyWrongRequestAndTamperingRejected() throws {
        let key = Curve25519.KeyAgreement.PrivateKey()
        let request = TransferRequest(id: UUID(), createdAt: now, publicKey: key.publicKey.rawRepresentation)
        let envelope = try SessionTransfer.seal([cookie()], for: request, now: now)
        XCTAssertThrowsError(try SessionTransfer.open(envelope, request: request, privateKey: .init(), now: now))
        let other = TransferRequest(id: UUID(), createdAt: now, publicKey: key.publicKey.rawRepresentation)
        XCTAssertThrowsError(try SessionTransfer.open(envelope, request: other, privateKey: key, now: now))
        var bytes = envelope.ciphertext; bytes[bytes.count - 1] ^= 1
        XCTAssertThrowsError(try SessionTransfer.open(.init(id: envelope.id, senderKey: envelope.senderKey, ciphertext: bytes), request: request, privateKey: key, now: now))
    }
    func testExpiryAndFutureRequestsRejected() throws {
        let key = Curve25519.KeyAgreement.PrivateKey()
        for offset in [-60.01, 6.0] {
            let request = TransferRequest(id: UUID(), createdAt: now.addingTimeInterval(offset), publicKey: key.publicKey.rawRepresentation)
            XCTAssertThrowsError(try SessionTransfer.seal([cookie()], for: request, now: now))
        }
    }
    func testCookiePolicyRejectsForeignAmbiguousExpiredAndMalformedCredentials() {
        let invalid = [[], [cookie(name: "ct0")], [cookie(), cookie()], [cookie(domain: "x.com.evil.test")], [cookie(path: "/other")], [cookie(secure: false)], [cookie(value: "bad\nvalue")], [cookie(value: String(repeating: "a", count: 4097))], [cookie(expires: now.timeIntervalSince1970 - 1)], [cookie(name: "unknown")]]
        for cookies in invalid { XCTAssertThrowsError(try SessionTransfer.validate(cookies, now: now)) }
    }
    func testMenuTitleKeepsScaleAndLeadingSymbol() {
        for (source, expected) in [("24.8K", "𝕏 24.8K"), ("1.2M", "𝕏 1.2M"), ("1.2만", "𝕏 1.2만"), ("999", "𝕏 999"), ("0", "𝕏 0"), ("—", "𝕏")] {
            XCTAssertEqual(MetricRecord.menuTitle(for: source), expected)
        }
    }
    func testRetryCreatesFreshRequestAndRejectsPreviousResponse() throws {
        let firstKey = Curve25519.KeyAgreement.PrivateKey()
        let first = TransferRequest(id: UUID(), createdAt: now, publicKey: firstKey.publicKey.rawRepresentation)
        let response = try SessionTransfer.seal([cookie()], for: first, now: now)
        XCTAssertNoThrow(try first.validate(now: now.addingTimeInterval(60)))
        XCTAssertThrowsError(try SessionTransfer.open(response, request: first, privateKey: firstKey, now: now.addingTimeInterval(61)))
        let retryKey = Curve25519.KeyAgreement.PrivateKey()
        let retry = TransferRequest(id: UUID(), createdAt: now.addingTimeInterval(90), publicKey: retryKey.publicKey.rawRepresentation)
        XCTAssertThrowsError(try SessionTransfer.open(response, request: retry, privateKey: retryKey, now: now.addingTimeInterval(90)))
        let next = try SessionTransfer.seal([cookie()], for: retry, now: now.addingTimeInterval(90))
        XCTAssertEqual(try SessionTransfer.open(next, request: retry, privateKey: retryKey, now: now.addingTimeInterval(91)).count, 1)
    }
    func testIntentIsBoundToCurrentGuideAndFreshButtonClick() throws {
        let instance = UUID()
        let intent = PairingIntent(id: UUID(), instance: instance, createdAt: now)
        XCTAssertNoThrow(try intent.validate(instance: instance, now: now))
        XCTAssertThrowsError(try intent.validate(instance: UUID(), now: now))
        XCTAssertThrowsError(try intent.validate(instance: instance, now: now.addingTimeInterval(16)))
        XCTAssertThrowsError(try intent.validate(instance: instance, now: now.addingTimeInterval(-6)))
    }
}
