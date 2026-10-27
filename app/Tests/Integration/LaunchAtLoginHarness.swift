import Foundation
import ServiceManagement

// Compile with Sources/TodayBar/LaunchAtLogin.swift. Uses isolated preferences
// and a fake service so these checks never change the Mac's real login items.
@MainActor
private final class FakeLoginItem: LoginItemService {
    var status: SMAppService.Status = .notRegistered
    var registrations = 0
    var removals = 0
    var failure: Error?
    var registrationStatus: SMAppService.Status = .enabled
    func register() throws {
        registrations += 1
        if let failure { throw failure }
        status = registrationStatus
    }
    func unregister() throws {
        removals += 1
        if let failure { throw failure }
        status = .notRegistered
    }
}

@main
struct LaunchAtLoginHarness {
    @MainActor
    static func main() {
        let suite = "com.limod.xbar.tests.login.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let service = FakeLoginItem()
        var settingsOpened = 0
        func makeModel() -> LaunchAtLogin {
            LaunchAtLogin(service: service, defaults: defaults, openSettings: { settingsOpened += 1 })
        }
        func check(_ message: String, _ passed: Bool) {
            precondition(passed, message)
            print("PASS \(message)")
        }
        defaults.set(30, forKey: "intervalMinutes")
        let model = makeModel()
        model.start()
        check("First launch enables login item", model.enabled && service.registrations == 1)
        makeModel().start()
        check("Relaunch does not duplicate registration", service.registrations == 1)
        model.setEnabled(false)
        makeModel().start()
        check("Opt-out survives relaunch", !model.enabled && service.status == .notRegistered && service.registrations == 1)
        model.setEnabled(true)
        check("Explicit re-enable works", model.enabled && service.registrations == 2)
        service.status = .requiresApproval
        model.refresh()
        makeModel().start()
        check("System revocation stays disabled", !model.enabled && model.requiresApproval && service.registrations == 2)
        model.setEnabled(true)
        check("Approval opens settings without re-registering", settingsOpened == 1 && service.registrations == 2 && !model.enabled)
        service.status = .enabled
        model.refresh()
        check("External approval refreshes the displayed state", model.enabled)
        service.status = .notRegistered
        makeModel().start()
        check("External removal survives relaunch", service.registrations == 2)
        service.failure = NSError(domain: "LoginItemTest", code: 1)
        model.setEnabled(true)
        check("Registration failure stays off and exposes error", !model.enabled && model.errorDescription != nil)
        service.failure = nil
        model.setEnabled(true)
        check("Manual retry clears error", model.enabled && model.errorDescription == nil)
        service.failure = NSError(domain: "LoginItemTest", code: 2)
        model.setEnabled(false)
        check("Unregister failure preserves actual enabled state", model.enabled && model.errorDescription != nil)
        service.failure = nil
        defaults.removeObject(forKey: "launchAtLoginInitialized")
        service.status = .requiresApproval
        let registrations = service.registrations
        makeModel().start()
        check("Existing approval requirement is preserved on first launch", service.registrations == registrations)
        defaults.removeObject(forKey: "launchAtLoginInitialized")
        service.status = .notFound
        makeModel().start()
        check("Previously unseen main app is registered", service.status == .enabled && service.registrations == registrations + 1)
        defaults.removeObject(forKey: "launchAtLoginInitialized")
        service.status = .notRegistered
        service.registrationStatus = .requiresApproval
        let pending = makeModel()
        pending.start()
        check("Pending approval is not shown as enabled", pending.requiresApproval && !pending.enabled)
        check("Unrelated preferences are preserved", defaults.integer(forKey: "intervalMinutes") == 30)
        print("All launch-at-login checks passed")
    }
}
