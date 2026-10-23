// Offline integration test. No X requests or real credentials. Uses an ephemeral store.
import AppKit
import WebKit

@MainActor
final class Harness: NSObject, NSApplicationDelegate, WKNavigationDelegate {
    var view: WKWebView!
    var window: NSWindow!
    var loadContinuation: CheckedContinuation<Void, Never>?
    var results: [[String: Any]] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        Task { await run() }
    }

    func check(_ name: String, _ passed: Bool, _ detail: String = "") {
        results.append(["test": name, "passed": passed, "detail": detail])
    }

    func load(_ body: String, path: String = "https://x.com/i/account_analytics") async {
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .nonPersistent()
        let extractorURL = URL(fileURLWithPath: CommandLine.arguments[1])
        let context = try! String(contentsOf: extractorURL.deletingLastPathComponent().appendingPathComponent("PageContext.js"), encoding: .utf8)
        let source = context + "\n" + (try! String(contentsOf: extractorURL, encoding: .utf8))
        config.userContentController.addUserScript(WKUserScript(source: source, injectionTime: .atDocumentEnd, forMainFrameOnly: true, in: .defaultClient))
        view = WKWebView(frame: NSRect(x: 0, y: 0, width: 900, height: 700), configuration: config)
        view.navigationDelegate = self
        window.contentView = view
        await withCheckedContinuation { continuation in
            loadContinuation = continuation
            view.loadHTMLString("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\"><style>.sr-only{position:absolute;width:1px;height:1px;overflow:hidden}button{display:block}</style>" + body, baseURL: URL(string: path))
        }
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        loadContinuation?.resume()
        loadContinuation = nil
    }

    func js(_ code: String, world: WKContentWorld = .defaultClient) async throws -> String {
        try await withCheckedThrowingContinuation { continuation in
            view.evaluateJavaScript(code, in: nil, in: world) { result in
                switch result {
                case .success(let value): continuation.resume(returning: String(describing: value))
                case .failure(let error): continuation.resume(throwing: error)
                }
            }
        }
    }

    func read(_ method: String = "snapshot") async throws -> [String: Any] {
        let string = try await js("JSON.stringify(TodayBarExtractor.\(method)())")
        return try JSONSerialization.jsonObject(with: Data(string.utf8)) as! [String: Any]
    }

    func card(_ value: String = "16.3K", label: String = "노출수") -> String {
        "<button aria-haspopup='dialog'><div>\(label)</div><div><span class='sr-only'>\(value)</span><span aria-hidden='true'>0\(value)</span></div><div><span class='sr-only'>-75%</span><span aria-hidden='true'>-75%</span></div></button>"
    }

    func dateScript(_ expression: String = "`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`") -> String {
        "<script>const d=new Date();document.getElementById('date').textContent=\(expression);</script>"
    }

    var dateButton: String { "<button id='date' aria-label='Select date range' aria-haspopup='dialog'></button>" }
    var account: String { "<button aria-label='계정 메뉴'>테스트 @fixture</button>" }

    func run() async {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 900, height: 700), styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.orderOut(nil)
        do {
            await load(dateButton + card() + account + dateScript())
            var result = try await read()
            check("Korean accessible number excludes animation duplicate and change percent", result["display"] as? String == "16.3K" && result["status"] as? String == "ready", String(describing: result))
            check("Account label", result["account"] as? String == "@fixture")
            let pageGlobal = try await js("typeof TodayBarExtractor", world: .page)
            check("Extractor isolated from page world", pageGlobal == "undefined")
            check("Read with hidden native window", !window.isVisible && result["display"] as? String == "16.3K")

            await load(dateButton + card() + "<button aria-label='계정 메뉴'></button><a aria-label='프로필' href='/fixture'>프로필</a>" + dateScript())
            result = try await read()
            check("Account identity in narrow sidebar", result["account"] as? String == "@fixture")

            await load(dateButton + card("0") + dateScript())
            result = try await read()
            check("Zero is valid", result["display"] as? String == "0")
            await load(dateButton + card("12,345", label: "Impressions") + dateScript("new Intl.DateTimeFormat('en-US',{dateStyle:'medium'}).format(d)"))
            result = try await read()
            check("English date and metric", result["display"] as? String == "12,345")

            await load("<button id='date' aria-label='日付範囲を選択' aria-haspopup='dialog'></button>" + card("1.2万", label: "インプレッション") + "<button aria-label='アカウントメニュー'>@fixture</button>" + dateScript("new Intl.DateTimeFormat('ja-JP',{dateStyle:'long'}).format(d)"))
            result = try await read()
            check("Japanese date, account and abbreviated impressions", result["status"] as? String == "ready" && result["display"] as? String == "1.2万" && result["account"] as? String == "@fixture")
            await load("<button id='date' aria-label='日付範囲を選択' aria-haspopup='dialog'></button>" + card("1万%", label: "インプレッション") + dateScript("new Intl.DateTimeFormat('ja-JP',{dateStyle:'short'}).format(d)"))
            result = try await read()
            check("Japanese malformed metric rejected", result["status"] as? String == "unavailable")

            await load(dateButton + card("1.4M") + dateScript("`2026. 1. 1. – ${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`"))
            result = try await read()
            check("Range ending today rejected", result["status"] as? String == "wrong_day" && result["display"] == nil)
            await load(dateButton + card() + "<script>document.getElementById('date').textContent='2020. 1. 1.';</script>")
            result = try await read()
            check("Previous day rejected", result["status"] as? String == "wrong_day")

            await load(dateButton + card() + card("22K") + dateScript())
            result = try await read()
            check("Ambiguous cards rejected", result["status"] as? String == "unavailable")
            await load(dateButton + card("-75%") + dateScript())
            result = try await read()
            check("Percentage never used as count", result["status"] as? String == "unavailable")
            await load(dateButton + dateScript())
            result = try await read()
            check("Missing metric rejected", result["status"] as? String == "unavailable")
            await load(dateButton + card() + "<div role='progressbar'>Loading</div>" + dateScript())
            result = try await read()
            check("Loading metric rejected", result["status"] as? String == "loading")
            await load("<input autocomplete='current-password'>", path: "https://x.com/i/flow/login")
            result = try await read()
            check("Login form recognized without reading input value", result["status"] as? String == "login_required")
            await load("<h1>X 로그인</h1>", path: "https://x.com/i/jf/onboarding/web?mode=login")
            result = try await read()
            check("Current X onboarding login route recognized", result["status"] as? String == "login_required")
            let onboarding = "https://x.com/i/jf/onboarding/web?mode=login"
            await load(account + "<input id='username' autocomplete='username'>", path: onboarding)
            result = try await read()
            check("Signed-in account behind onboarding offers session recovery", result["status"] as? String == "login_session_detected" && result["display"] == nil)
            _ = try await js("document.getElementById('username').focus();'focused'", world: .page)
            result = try await read()
            check("Focused sign-in field is not interrupted", result["status"] as? String == "login_required")
            _ = try await js("const input=document.getElementById('username');input.dispatchEvent(new Event('input',{bubbles:true}));input.blur();'edited'", world: .page)
            result = try await read()
            check("Started sign-in remains protected after losing focus", result["status"] as? String == "login_required")
            await load(account + "<input type='password' autocomplete='current-password'>", path: onboarding)
            result = try await read()
            check("Password challenge behind known account is not skipped", result["status"] as? String == "login_required")
            await load(account + "<input autocomplete='one-time-code'>", path: onboarding)
            result = try await read()
            check("Verification challenge behind known account is not skipped", result["status"] as? String == "login_required")
            await load("<div style='display:none'>" + account + "</div>", path: onboarding)
            result = try await read()
            check("Hidden account markup cannot claim an active session", result["status"] as? String == "login_required" && result["account"] is NSNull)
            await load(account + dateButton + card() + "<input type='password' style='display:none'>" + dateScript())
            result = try await read()
            check("Hidden password markup does not misclassify valid analytics", result["status"] as? String == "ready")
            await load(dateButton + card() + dateScript(), path: "https://x.com.evil.example/i/account_analytics")
            result = try await read()
            check("Untrusted origin rejected", result["status"] as? String == "off_page")

            let calendar = """
            <button id='date' aria-label='Select date range' aria-haspopup='dialog' aria-controls='popup' aria-expanded='false' onclick="this.setAttribute('aria-expanded','true');document.getElementById('popup').style.display='block'">2020. 1. 1. – 2020. 1. 7.</button>
            <div id='popup' role='dialog' style='display:none'>
            <button aria-pressed='false' onclick="this.setAttribute('aria-pressed','true');document.getElementById('end').setAttribute('aria-pressed','false')"><span>시작하기</span><span>1월 1일</span></button>
            <button id='end' aria-pressed='true'><span>종료</span><span>1월 7일</span></button>
            <div role='grid'><div role='gridcell'><button id='day'>오늘</button></div></div></div>
            \(card("1.4M"))
            <script>
            const d=new Date();const day=document.getElementById('day');
            day.setAttribute('aria-label',new Intl.DateTimeFormat('ko-KR',{weekday:'long',year:'numeric',month:'long',day:'numeric'}).format(d));
            day.onclick=()=>{document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;setTimeout(()=>{document.querySelector('.sr-only').textContent='16.3K'},600)};
            </script>
            """
            await load(calendar)
            for _ in 0..<3 { _ = try await read("advance") }
            result = try await read()
            check("Calendar transition does not capture old range total", result["status"] as? String == "loading" && result["display"] == nil)
            try await Task.sleep(for: .milliseconds(4300))
            result = try await read()
            check("Calendar selects today using visible controls", result["status"] as? String == "ready" && result["display"] as? String == "16.3K")
            check("Still reads after hidden window delay", !window.isVisible && result["display"] as? String == "16.3K")
            await load(dateButton + card() + account + dateScript(), path: "https://x.com/i/account_analytics/content")
            result = try await read()
            check("Content page cannot supply aggregate", result["status"] as? String == "off_page")
            await load(dateButton + card() + account + dateScript(), path: "https://x.com/i/account_analytics/overview")
            result = try await read()
            check("Overview supplies aggregate", result["status"] as? String == "ready")
            await load(dateButton + card() + account + dateScript() + "<a href='/other' aria-label='Profile'>Profile</a>")
            result = try await read()
            check("Conflicting visible accounts not accepted", result["status"] as? String == "loading")
        } catch {
            check("Harness execution", false, String(describing: error))
        }
        let data = try! JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys])
        print(String(data: data, encoding: .utf8)!)
        let failed = results.contains { $0["passed"] as? Bool != true }
        exit(failed ? 1 : 0)
    }
}

let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let harness = Harness()
app.delegate = harness
app.run()
