import AppKit
import Combine
import SwiftUI
import WebKit

/// A separately signed sandbox app uses the production updater without X credentials.
@MainActor
final class UpdaterLabDelegate: NSObject, NSApplicationDelegate {
    let updater = AppUpdater()
    var window: NSWindow!

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as! String
        let oldPreference = UserDefaults.standard.string(forKey: "updaterLabPreference")
        if version == "1" { UserDefaults.standard.set("preserve-me", forKey: "updaterLabPreference") }
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 520, height: 250),
            styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.title = "X-Bar Updater Lab · build \(version)"
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(rootView: UpdaterLabView(updater: updater, version: version,
            preference: oldPreference ?? "new"))
        window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        updater.start()
        let store = WKWebsiteDataStore.default().httpCookieStore
        if version == "1" {
            let cookie = HTTPCookie(properties: [.domain: "updater-lab.invalid", .path: "/",
                .name: "synthetic-session", .value: "preserve-synthetic", .expires: Date().addingTimeInterval(3600)])!
            store.setCookie(cookie) { self.record(version: version, preference: oldPreference ?? "new", cookie: "seeded") }
        } else {
            store.getAllCookies { cookies in
                let kept = cookies.contains { $0.name == "synthetic-session" && $0.value == "preserve-synthetic" }
                self.record(version: version, preference: oldPreference ?? "missing", cookie: kept ? "preserved" : "missing")
            }
        }
    }

    func record(version: String, preference: String, cookie: String) {
        window.contentView = NSHostingView(rootView: UpdaterLabView(updater: updater, version: version,
            preference: preference, cookie: cookie))
        let directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        try! FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let result: [String: Any] = ["version": version, "preference": preference, "cookie": cookie,
            "automaticChecks": updater.automaticChecks]
        try! JSONSerialization.data(withJSONObject: result, options: .prettyPrinted)
            .write(to: directory.appendingPathComponent("updater-lab-result.json"))
    }
}

struct UpdaterLabView: View {
    @ObservedObject var updater: AppUpdater
    let version: String
    let preference: String
    var cookie: String = "checking"
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("Updater Lab build \(version)").font(.title2)
            Text("Saved preference: \(preference)")
            Text("Synthetic login cookie: \(cookie)")
            Toggle("Automatically check for updates", isOn: Binding(
                get: { updater.automaticChecks }, set: { updater.setAutomaticChecks($0) }))
            Button("Check for Updates", action: updater.checkForUpdates).disabled(!updater.canCheckForUpdates)
            if let error = updater.startupError { Text(error) }
        }.padding(24).frame(maxWidth: .infinity, alignment: .leading)
    }
}

@main enum UpdaterLabMain {
    @MainActor static func main() {
        let app = NSApplication.shared
        let delegate = UpdaterLabDelegate()
        app.delegate = delegate
        withExtendedLifetime(delegate) { app.run() }
    }
}
