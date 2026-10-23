import Foundation

/// An Install choice belongs to one presentation and can be consumed only once.
/// Never persist it: cancellation, another version, or relaunch requires a new choice.
struct UpdateInstallationConsent {
    private(set) var session: UUID?
    private var requestedInstall = false

    mutating func begin() -> UUID {
        let token = UUID()
        session = token
        requestedInstall = false
        return token
    }

    mutating func record(install: Bool, for token: UUID) {
        guard session == token else { return }
        requestedInstall = install
    }

    mutating func invalidate(session token: UUID? = nil) {
        if let token, session != token { return }
        session = nil
        requestedInstall = false
    }

    mutating func consumeInstallRequest() -> Bool {
        let shouldInstall = session != nil && requestedInstall
        invalidate()
        return shouldInstall
    }
}
