import AppKit
import Combine
import Sparkle

/// One updater per process. Sparkle owns scheduling, signature checks and installation.
@MainActor
final class AppUpdater: NSObject, ObservableObject, @preconcurrency SPUStandardUserDriverDelegate {
    @Published private(set) var canCheckForUpdates = false
    @Published private(set) var automaticChecks = false
    @Published private(set) var availableVersion: String?
    @Published private(set) var startupError: String?
    private var updater: SPUUpdater!
    private var userDriver: AppUpdateUserDriver!
    private var started = false

    override init() {
        super.init()
        // Sparkle's standard UI and updater run on the main thread. The Objective-C
        // delegate protocol in 2.9.6 lacks this annotation; @preconcurrency keeps
        // the MainActor runtime check at that boundary instead of moving UI state off it.
        userDriver = AppUpdateUserDriver(hostBundle: .main, delegate: self)
        updater = SPUUpdater(hostBundle: .main, applicationBundle: .main,
            userDriver: userDriver, delegate: nil)
        updater.publisher(for: \.canCheckForUpdates)
            .assign(to: &$canCheckForUpdates)
        updater.publisher(for: \.automaticallyChecksForUpdates)
            .assign(to: &$automaticChecks)
    }

    func start() {
        guard !started else { return }
        do {
            try updater.start()
            started = true
            startupError = nil
        } catch {
            startupError = error.localizedDescription
        }
    }

    func setAutomaticChecks(_ enabled: Bool) {
        // Setting this only in response to a user action preserves saved preferences.
        updater.automaticallyChecksForUpdates = enabled
    }

    func checkForUpdates() {
        guard canCheckForUpdates else { return }
        NSApp.activate(ignoringOtherApps: true)
        updater.checkForUpdates()
    }

    var supportsGentleScheduledUpdateReminders: Bool { true }

    func standardUserDriverShouldHandleShowingScheduledUpdate(
        _ update: SUAppcastItem, andInImmediateFocus immediateFocus: Bool
    ) -> Bool {
        // A background menu-bar app announces availability in Settings first.
        false
    }

    func standardUserDriverWillHandleShowingUpdate(
        _ handleShowingUpdate: Bool, forUpdate update: SUAppcastItem, state: SPUUserUpdateState
    ) {
        if !handleShowingUpdate { availableVersion = update.displayVersionString }
    }

    func standardUserDriverDidReceiveUserAttention(forUpdate update: SUAppcastItem) {
        availableVersion = nil
    }

    func standardUserDriverWillFinishUpdateSession() {
        availableVersion = nil
    }

    func standardUserDriverWillShowModalAlert() {
        NSApp.activate(ignoringOtherApps: true)
    }
}

/// Keep Sparkle's signature verification, cancellation, progress and quit handling.
/// Like DiskOUT, one explicit Install choice continues through download to relaunch.
@MainActor
final class AppUpdateUserDriver: SPUStandardUserDriver {
    private var consent = UpdateInstallationConsent()

    override func showUpdateFound(with appcastItem: SUAppcastItem, state: SPUUserUpdateState,
                                  reply: @escaping (SPUUserUpdateChoice) -> Void) {
        let session = consent.begin()
        super.showUpdateFound(with: appcastItem, state: state) { [weak self] choice in
            self?.consent.record(install: choice == .install, for: session)
            reply(choice)
        }
    }

    override func showDownloadInitiated(cancellation: @escaping () -> Void) {
        let session = consent.session
        super.showDownloadInitiated { [weak self] in
            if let session { self?.consent.invalidate(session: session) }
            cancellation()
        }
    }

    override func showReady(toInstallAndRelaunch reply: @escaping (SPUUserUpdateChoice) -> Void) {
        if consent.consumeInstallRequest() { reply(.install) }
        else { super.showReady(toInstallAndRelaunch: reply) }
    }

    override func showUpdaterError(_ error: Error, acknowledgement: @escaping () -> Void) {
        consent.invalidate()
        super.showUpdaterError(error, acknowledgement: acknowledgement)
    }

    override func dismissUpdateInstallation() {
        consent.invalidate()
        super.dismissUpdateInstallation()
    }
}
