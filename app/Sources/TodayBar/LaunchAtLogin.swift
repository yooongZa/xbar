import Combine
import Foundation
import ServiceManagement

@MainActor
protocol LoginItemService {
    var status: SMAppService.Status { get }
    func register() throws
    func unregister() throws
}

extension SMAppService: LoginItemService {}

/// macOS owns the actual setting; the saved flag only prevents automatic re-enabling.
@MainActor
final class LaunchAtLogin: ObservableObject {
    @Published private(set) var status: SMAppService.Status = .notRegistered
    @Published private(set) var errorDescription: String?
    private let service: any LoginItemService
    private let defaults: UserDefaults
    private let openSettings: () -> Void
    private let initializedKey = "launchAtLoginInitialized"

    init(service: any LoginItemService = SMAppService.mainApp,
         defaults: UserDefaults = .standard,
         openSettings: @escaping () -> Void = { SMAppService.openSystemSettingsLoginItems() }) {
        self.service = service
        self.defaults = defaults
        self.openSettings = openSettings
        refresh()
    }

    var enabled: Bool { status == .enabled }
    var requiresApproval: Bool { status == .requiresApproval }

    func start() {
        refresh()
        guard !defaults.bool(forKey: initializedKey) else { return }
        // Attempt the default once, including across app updates. Subsequent launches
        // must preserve changes made in either this app or System Settings.
        defaults.set(true, forKey: initializedKey)
        // A previously unseen main app can report notFound until its first registration.
        if status == .notRegistered || status == .notFound { setEnabled(true) }
    }

    func refresh() {
        let current = service.status
        if status != current { errorDescription = nil }
        status = current
    }

    func setEnabled(_ enabled: Bool) {
        defaults.set(true, forKey: initializedKey)
        refresh()
        errorDescription = nil
        do {
            if enabled {
                if requiresApproval { openSystemSettings(); return }
                if status != .enabled { try service.register() }
            } else if status != .notRegistered {
                try service.unregister()
            }
            refresh()
        } catch {
            refresh()
            errorDescription = error.localizedDescription
        }
    }

    func openSystemSettings() { openSettings() }
}
