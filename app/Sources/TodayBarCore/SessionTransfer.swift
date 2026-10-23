import Foundation
import CryptoKit

struct SessionCookie: Codable, Sendable {
    let name: String
    let value: String
    let domain: String
    let path: String
    let secure: Bool
    let httpOnly: Bool
    let expirationDate: Double?
}

enum TransferError: Error { case invalid, expired, unavailable }

struct TransferRequest: Codable, Sendable {
    let id: UUID
    let createdAt: Date
    let publicKey: Data
    func validate(now: Date = Date()) throws {
        guard (-5...60).contains(now.timeIntervalSince(createdAt)), publicKey.count == 32 else { throw TransferError.expired }
    }
}

struct PairingAvailability: Codable, Sendable {
    let instance: UUID
}

struct PairingIntent: Codable, Sendable {
    let id: UUID
    let instance: UUID
    let createdAt: Date
    func validate(instance expected: UUID, now: Date = Date()) throws {
        guard instance == expected, (-5...15).contains(now.timeIntervalSince(createdAt)) else { throw TransferError.expired }
    }
}

struct TransferEnvelope: Codable, Sendable {
    let id: UUID
    let senderKey: Data
    let ciphertext: Data
}

enum SessionTransfer {
    static let maximumBytes = 32768
    static func validate(_ cookies: [SessionCookie], now: Date = Date()) throws {
        guard (1...3).contains(cookies.count), Set(cookies.map(\.name)).count == cookies.count,
              cookies.contains(where: { $0.name == "auth_token" }) else { throw TransferError.invalid }
        for cookie in cookies {
            guard ["auth_token", "ct0", "twid"].contains(cookie.name),
                  ["x.com", ".x.com"].contains(cookie.domain), cookie.path == "/", cookie.secure,
                  !cookie.value.isEmpty, cookie.value.utf8.count <= 4096,
                  cookie.value.rangeOfCharacter(from: .controlCharacters) == nil,
                  cookie.expirationDate.map({ $0.isFinite && $0 > now.timeIntervalSince1970 }) ?? true else { throw TransferError.invalid }
        }
    }
    private static func key(_ secret: SharedSecret, id: UUID) -> SymmetricKey {
        secret.hkdfDerivedSymmetricKey(using: SHA256.self, salt: Data(id.uuidString.utf8),
            sharedInfo: Data("TodayBar Safari Session v1".utf8), outputByteCount: 32)
    }
    static func seal(_ cookies: [SessionCookie], for request: TransferRequest, now: Date = Date()) throws -> TransferEnvelope {
        try request.validate(now: now); try validate(cookies, now: now)
        let ephemeral = Curve25519.KeyAgreement.PrivateKey()
        let receiver = try Curve25519.KeyAgreement.PublicKey(rawRepresentation: request.publicKey)
        let secret = try ephemeral.sharedSecretFromKeyAgreement(with: receiver)
        let data = try JSONEncoder().encode(cookies)
        let box = try AES.GCM.seal(data, using: key(secret, id: request.id), authenticating: Data(request.id.uuidString.utf8))
        return TransferEnvelope(id: request.id, senderKey: ephemeral.publicKey.rawRepresentation, ciphertext: box.combined!)
    }
    static func open(_ envelope: TransferEnvelope, request: TransferRequest, privateKey: Curve25519.KeyAgreement.PrivateKey, now: Date = Date()) throws -> [SessionCookie] {
        try request.validate(now: now)
        guard envelope.id == request.id, envelope.ciphertext.count <= maximumBytes else { throw TransferError.invalid }
        let sender = try Curve25519.KeyAgreement.PublicKey(rawRepresentation: envelope.senderKey)
        let secret = try privateKey.sharedSecretFromKeyAgreement(with: sender)
        let data = try AES.GCM.open(AES.GCM.SealedBox(combined: envelope.ciphertext), using: key(secret, id: request.id), authenticating: Data(request.id.uuidString.utf8))
        let cookies = try JSONDecoder().decode([SessionCookie].self, from: data)
        try validate(cookies, now: now)
        return cookies
    }
}

/// Caller serializes replacements and stops page writers until this returns.
/// Nonthrowing storage callbacks must complete even when the caller is cancelled,
/// so the snapshot can be restored before the transaction lock is released.
@MainActor
enum CookieReplacement {
    static func replace<Cookie>(with incoming: [Cookie],
        read: @MainActor () async -> [Cookie], delete: @MainActor (Cookie) async -> Void, write: @MainActor (Cookie) async -> Void,
        isCurrent: @MainActor () -> Bool, commit: @MainActor () -> Void = {}) async -> Bool {
        func cancelled() -> Bool { Task.isCancelled || !isCurrent() }
        guard !cancelled() else { return false }
        let previous = await read()
        guard !cancelled() else { return false }
        var changed = false
        do {
            for cookie in previous {
                guard !cancelled() else { throw CancellationError() }
                changed = true
                await delete(cookie)
            }
            for cookie in incoming {
                guard !cancelled() else { throw CancellationError() }
                changed = true
                await write(cookie)
            }
            guard !cancelled() else { throw CancellationError() }
            commit() // No suspension between the last cancellation check and committing state.
            return true
        } catch {
            if changed {
                // Rollback deliberately ignores cancellation; never leave half a session.
                for cookie in await read() { await delete(cookie) }
                for cookie in previous { await write(cookie) }
            }
            return false
        }
    }
}
