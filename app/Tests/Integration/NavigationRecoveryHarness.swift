// Exercises the production controller with real WebKit and offline X fixtures.
import AppKit
import WebKit

@MainActor enum RecoveryFixture {
    static var now = Date()
    static var loads = 0
    static var redirect = false
    static var stallReads = false
    static var stalledReadStarted = false
    static let account = "<button aria-label='Account menu'>@fixture</button>"
    static func page(_ body: String) -> String {
        "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\">" + account + body
    }
    static var responseHTML: String {
        loads += 1
        if redirect { return page("<script>history.replaceState(null,'','/notifications')</script>") }
        return page("""
        <button id='date' aria-label='Select date range'></button>
        <button aria-haspopup='dialog'>Impressions <span class='sr-only'>40.5K</span></button>
        <script>const d=new Date();document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;</script>
        """)
    }
    static var contentHTML: String { page("<main>No posts during the selected period.</main>") }
}

@MainActor final class RecoveryAudit: NSObject, NSApplicationDelegate {
    var results: [[String: Any]] = []
    func applicationDidFinishLaunching(_ note: Notification) { Task { await run() } }
    func check(_ name: String, _ passed: Bool) { results.append(["test": name, "passed": passed]) }
    func wait(_ condition: () -> Bool) async -> Bool {
        for _ in 0..<100 {
            if condition() { return true }
            try? await Task.sleep(for: .milliseconds(100))
        }
        return false
    }
    func js(_ controller: BrowserController, _ code: String) async throws {
        _ = try await controller.webView.evaluateJavaScript(code + ";true")
    }
    func advance(_ controller: BrowserController, _ seconds: Double) {
        RecoveryFixture.now = RecoveryFixture.now.addingTimeInterval(seconds)
        controller.auditTick()
    }
    func run() async {
        UserDefaults.standard.removeObject(forKey: "connected")
        let controller = BrowserController()
        let window = NSWindow(contentRect: controller.webView.frame, styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = controller.webView
        controller.networkChanged(online: true)
        controller.connect()
        check("Initial aggregate reading accepted", await wait { controller.hasFreshValue })
        check("Content reader also recovers", await wait { controller.content.updatedAt != nil })
        do {
            for visible in [false, true] {
                if visible { window.orderFront(nil) } else { window.orderOut(nil) }
                let before = RecoveryFixture.loads
                try await js(controller, "history.pushState(null,'','/notifications')")
                advance(controller, 15)
                check("Notifications recover automatically, visible=\(visible)", await wait {
                    RecoveryFixture.loads > before && controller.hasFreshValue && NavigationPolicy.isOverview(controller.webView.url)
                })
            }
            window.orderOut(nil)
            var before = RecoveryFixture.loads
            try await js(controller, "history.pushState(null,'','/notifications')")
            controller.networkChanged(online: false)
            check("Offline state stops spinner and hides stale count", !controller.showsSpinner && !controller.hasFreshValue)
            controller.networkChanged(online: true)
            check("Network restoration recovers from notifications", await wait { RecoveryFixture.loads > before && controller.hasFreshValue })
            before = RecoveryFixture.loads
            try await js(controller, "history.pushState(null,'','/notifications')")
            controller.sleepChanged(true)
            check("Sleep stops spinner", !controller.showsSpinner)
            controller.sleepChanged(false)
            check("Wake recovers from notifications", await wait { RecoveryFixture.loads > before && controller.hasFreshValue })
            before = RecoveryFixture.loads
            controller.webViewWebContentProcessDidTerminate(controller.webView)
            controller.auditTick()
            check("Terminated web process recovers without user refresh", await wait { RecoveryFixture.loads > before && controller.hasFreshValue })

            before = RecoveryFixture.loads
            RecoveryFixture.stallReads = true
            advance(controller, 15)
            check("Stalled callback reproduced", await wait { RecoveryFixture.stalledReadStarted })
            RecoveryFixture.stallReads = false
            advance(controller, 31)
            check("Stalled callback times out and recovers automatically", await wait { RecoveryFixture.loads > before && controller.hasFreshValue })

            // A real full navigation to a challenge must remain at that challenge.
            controller.webView.loadHTMLString(RecoveryFixture.page("<input autocomplete='one-time-code'>"), baseURL: URL(string: "https://x.com/account/access"))
            check("OTP challenge recognized", await wait { controller.requiresLogin && !controller.busy })
            before = RecoveryFixture.loads
            advance(controller, 600)
            check("OTP input is never reloaded on a timer", controller.webView.url?.path == "/account/access" && RecoveryFixture.loads == before && !controller.showsSpinner && controller.retryAt == nil)

            controller.webView.loadHTMLString(RecoveryFixture.page("<textarea id='draft'></textarea><script>document.getElementById('draft').focus()</script>"), baseURL: URL(string: "https://x.com/compose/post"))
            check("Draft page settles without navigation", await wait { !controller.requiresLogin && !controller.busy && !controller.webView.isLoading && controller.webView.url?.path == "/compose/post" })
            try await js(controller, "const draft=document.getElementById('draft');draft.dispatchEvent(new Event('input',{bubbles:true}));draft.blur()")
            before = RecoveryFixture.loads
            advance(controller, 15)
            check("Edited draft remains intact after blur", await wait { !controller.busy && RecoveryFixture.loads == before && controller.webView.url?.path == "/compose/post" && !controller.showsSpinner })

            // Redirect loops use 30/60/... second backoff and expose an idle state.
            RecoveryFixture.redirect = true
            controller.refresh()
            check("Redirect schedules an automatic retry", await wait { controller.retryAt != nil && !controller.busy })
            before = RecoveryFixture.loads
            check("Retry wait stops spinner", !controller.showsSpinner)
            advance(controller, 29)
            check("Redirect cannot cause a tight reload loop", RecoveryFixture.loads == before)
            advance(controller, 1)
            check("Retry resumes automatically at deadline", await wait { RecoveryFixture.loads == before + 1 && controller.retryAt != nil && !controller.busy })
            check("Second retry uses longer backoff", controller.retryAt == RecoveryFixture.now.addingTimeInterval(60))

            // Authentication can appear while a retry is pending (an SPA modal).
            try await js(controller, "const otp=document.createElement('input');otp.autocomplete='one-time-code';document.body.appendChild(otp)")
            before = RecoveryFixture.loads
            advance(controller, 60)
            check("Pending retry rechecks and preserves new OTP challenge", await wait { controller.requiresLogin && controller.retryAt == nil && !controller.busy && RecoveryFixture.loads == before && !controller.showsSpinner })
            RecoveryFixture.redirect = false
            controller.refresh()
            check("Explicit refresh recovers immediately and clears retry state", await wait { controller.hasFreshValue && controller.retryAt == nil })
        } catch { results.append(["test": "Harness execution", "passed": false, "error": String(describing: error)]) }
        controller.stop()
        window.contentView = nil; window.close()
        print(String(data: try! JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys]), encoding: .utf8)!)
        exit(results.allSatisfy { $0["passed"] as? Bool == true } ? 0 : 1)
    }
}

@main struct RecoveryEntry {
    @MainActor static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)
        let audit = RecoveryAudit(); app.delegate = audit; app.run()
    }
}
