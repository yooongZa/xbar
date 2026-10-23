import AppKit
import WebKit
@MainActor enum AuditFixture {
 static var changing = false
 static var html: String {
  let updates = changing ? "<script>setInterval(()=>{const e=document.getElementById('likes');e.textContent=Number(e.textContent)+1},200)</script>" : ""
  return "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\"><button aria-label='Account menu'>@fixture</button><div><a style='display:block;width:500px;height:80px' aria-label='Synthetic post' href='/i/account_analytics/content/2111348825839611989'></a><div><svg width='12' height='12' data-icon='icon-bar-chart'></svg><span>10000</span></div><div><svg width='12' height='12' data-icon='icon-heart-stroke'></svg><span id='likes'>10</span></div><div><svg width='12' height='12' data-icon='icon-retweet-stroke'></svg><span>10</span></div></div>" + updates
 }
}
@MainActor final class Audit: NSObject, NSApplicationDelegate {
 func applicationDidFinishLaunching(_ note: Notification) {
  Task {
   var results: [[String: Any]] = []
   for changing in [false, true] {
    AuditFixture.changing = changing
    let monitor = ContentMonitor()
    monitor.resume()
    let start = Date()
    monitor.refreshIfNeeded(account: "@fixture", force: true)
    while monitor.busy { try? await Task.sleep(for: .milliseconds(100)) }
    results.append(["changingEvery200ms": changing, "rows": monitor.ledger.rows.count, "stale": monitor.stale, "message": monitor.message, "elapsed": Date().timeIntervalSince(start)])
    monitor.suspend()
   }
   print(String(data: try! JSONSerialization.data(withJSONObject: results,options:[.sortedKeys]),encoding:.utf8)!)
   exit(results.allSatisfy { $0["rows"] as? Int == 1 && $0["stale"] as? Bool == false } ? 0 : 1)
  }
 }
}
@main struct Entry {
 @MainActor static func main() {let app=NSApplication.shared;app.setActivationPolicy(.prohibited);let a=Audit();app.delegate=a;app.run()}
}
