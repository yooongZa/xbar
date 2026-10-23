import AppKit
import WebKit
@MainActor
final class Audit: NSObject, NSApplicationDelegate {
 func applicationDidFinishLaunching(_ note: Notification) {
  Task {
   UserDefaults.standard.removeObject(forKey: "connected")
   let controller = BrowserController()
   let cookie = SessionCookie(name: "auth_token", value: "synthetic-audit-only", domain: ".x.com", path: "/", secure: true, httpOnly: true, expirationDate: Date().addingTimeInterval(600).timeIntervalSince1970)
   let task = Task { await controller.importSafariSession([cookie]) }
   task.cancel()
   let result = await task.value
   let cookies = await controller.webView.configuration.websiteDataStore.httpCookieStore.allCookies()
   let output: [String: Any] = ["cancelled": task.isCancelled, "importReturned": result, "connectedAfterCancellation": controller.connected, "cookieCount": cookies.count, "networkOnline": controller.networkOnline, "nonPersistent": !controller.webView.configuration.websiteDataStore.isPersistent]
   print(String(data: try! JSONSerialization.data(withJSONObject: output, options: [.sortedKeys]), encoding: .utf8)!)
   let passed = !result && !controller.connected && cookies.isEmpty
   var popupResults: [Bool] = []
   for domClose in [false, true] {
    let config = WKWebViewConfiguration(); config.websiteDataStore = .nonPersistent()
    var popup: WKWebView? = controller.makeLoginPopup(configuration: config)
    weak var releasedPopup = popup
    let window = popup!.window!
    if domClose { controller.webViewDidClose(popup!) } else { window.performClose(nil) }
    popupResults.append(window.contentView == nil && window.delegate == nil)
    popup = nil
    try? await Task.sleep(for: .milliseconds(100))
    popupResults.append(releasedPopup == nil)
   }
   print("popupCleanup=\(popupResults)")
   controller.stop()
   exit(passed && popupResults.allSatisfy { $0 } ? 0 : 1)
  }
 }
}
@main
struct Entry {
 @MainActor static func main() {
  let app = NSApplication.shared
  app.setActivationPolicy(.prohibited)
  let audit = Audit()
  app.delegate = audit
  app.run()
 }
}
