// Offline integration test. No X requests or real credentials. Uses an ephemeral store.
import AppKit
import WebKit

@MainActor
final class ContentHarness: NSObject, NSApplicationDelegate, WKNavigationDelegate {
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

    func load(_ body: String, path: String? = nil) async {
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
            view.loadHTMLString("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\"><style>.sr-only{position:absolute;width:1px;height:1px;overflow:hidden}button{display:block}</style>" + body, baseURL: URL(string: path ?? scope()))
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
        let string = try await js("JSON.stringify(TodayBarContentExtractor.\(method)())")
        return try JSONSerialization.jsonObject(with: Data(string.utf8)) as! [String: Any]
    }


    func day(_ offset: Int = 0) -> String {
        let date = Calendar.current.date(byAdding: .day, value: offset, to: Date())!
        let format = DateFormatter(); format.locale = Locale(identifier: "en_US_POSIX")
        format.calendar = Calendar(identifier: .gregorian); format.dateFormat = "yyyy-MM-dd"
        return format.string(from: date)
    }
    func scope(_ offset: Int = 0) -> String {
        "https://x.com/i/account_analytics/content?type=posts&sort=impressions&dir=desc&from=\(day(offset))&to=\(day())"
    }

    func row(_ id: String = "2111348825839611989", title: String = "테스트 본문", count: String = "12.3K", rt: String = "26", likes: String = "42", hidden: Bool = false) -> String {
        """
        <div><a style='display:block;width:500px;height:80px' aria-label='\(title)' href='/i/account_analytics/content/\(id)'></a>
        <div><svg data-icon='icon-bar-chart' width='12' height='12'></svg><span>\(count)</span></div>
        <div><svg data-icon='icon-heart-stroke' width='12' height='12'></svg><span>\(likes)</span></div>
        <div><svg data-icon='icon-retweet-stroke' width='12' height='12'></svg><span>\(rt)</span></div>
        \(hidden ? "<div style='display:none'><svg data-icon='icon-bar-chart'></svg><span>999K</span></div>" : "")</div>
        """
    }
    let account = "<button aria-label='계정 메뉴'>테스트 @fixture</button>"
    func posts(_ result: [String: Any]) -> [[String: Any]] { result["posts"] as? [[String: Any]] ?? [] }
    func run() async {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 900, height: 700), styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false; window.orderOut(nil)
        do {
            await load("<button aria-label='アカウントメニュー'>@fixture</button>" + row(title: "日本語の投稿", count: "1.2万"))
            var japanese = try await read()
            check("Japanese account and count", japanese["status"] as? String == "ready" && posts(japanese).first?["impressions"] as? String == "1.2万")
            await load("<a href='/fixture' aria-label='プロフィール'>プロフィール</a><main>選択した期間に投稿はありません。</main>")
            japanese = try await read()
            check("Japanese empty period", japanese["status"] as? String == "empty" && japanese["account"] as? String == "@fixture")
            await load(account + "<main>No posts during the selected period.</main>")
            japanese = try await read()
            check("English empty period", japanese["status"] as? String == "empty")

            await load(account + row(hidden: true))
            var result = try await read()
            check("Hidden window reads visible metrics only", !window.isVisible && posts(result).first?["impressions"] as? String == "12.3K")
            check("Reads real post identity and repost metric", posts(result).first?["id"] as? String == "2111348825839611989" && posts(result).first?["reposts"] as? String == "26")
            check("Reads likes", posts(result).first?["likes"] as? String == "42")
            check("Verified account", result["account"] as? String == "@fixture")
            check("Isolated world", try await js("typeof TodayBarContentExtractor", world: .page) == "undefined")
            await load("<a aria-label='프로필' href='/fixture'>프로필</a>" + row(count: "0", rt: "0"))
            result = try await read()
            check("Narrow account and zero counts", result["account"] as? String == "@fixture" && posts(result).first?["impressions"] as? String == "0")
            await load(account + row() + row())
            result = try await read(); check("Duplicate post links coalesced", posts(result).count == 1)
            await load(account + row(count: "-30%"))
            result = try await read(); check("Malformed metric fails closed", result["status"] as? String == "unrecognized" && posts(result).isEmpty)
            await load(account + row() + "<div role='progressbar'>loading</div>")
            result = try await read(); check("Loading list not accepted", result["status"] as? String == "loading")
            await load(account + row(), path: "https://x.com/i/account_analytics/content?type=posts&sort=impressions&dir=desc&days=7&from=2026-10-22")
            result = try await read(); check("Ambiguous date query rejected", result["status"] as? String == "wrong_scope")
            await load(account + row(), path: "https://x.com/i/account_analytics/content?type=posts&sort=date&dir=desc&days=7")
            result = try await read(); check("Wrong sort rejected", result["status"] as? String == "wrong_scope")
            for offset in [0, -1, -6] {
                await load(account + row(), path: scope(offset))
                result = try await read()
                check("Supported date range \(offset)", result["status"] as? String == "ready" && (result["range"] as? [String: String]) == ["from": day(offset), "to": day()])
            }
            await load(account + row(), path: "https://x.com/i/account_analytics/content?type=posts&sort=impressions&dir=desc&days=7")
            result = try await read()
            check("Canonical seven-day URL keeps exact scope", result["status"] as? String == "ready" && (result["range"] as? [String: String]) == ["from": day(-6), "to": day()])
            await load(account + row(), path: scope(-2))
            result = try await read(); check("Unsupported period rejected", result["status"] as? String == "wrong_scope")
            await load(account + row(), path: scope() + "&from=" + day(-1))
            result = try await read(); check("Duplicate date parameters rejected", result["status"] as? String == "wrong_scope")
            await load(account + row(likes: "0"))
            result = try await read(); check("Zero likes preserved", posts(result).first?["likes"] as? String == "0")
            await load(account + row().replacingOccurrences(of: "icon-heart-stroke", with: "unknown"))
            result = try await read(); check("Missing likes never becomes zero", result["status"] as? String == "unrecognized")
            await load(account + row(likes: "--"))
            result = try await read(); check("Malformed likes rejected", result["status"] as? String == "unrecognized")
            await load(account + row(), path: "https://evil.example/i/account_analytics/content")
            result = try await read(); check("Wrong origin rejected", result["status"] as? String == "off_page")
            await load("<input autocomplete='current-password'>", path: "https://x.com/i/flow/login")
            result = try await read(); check("Expired login recognized", result["status"] as? String == "login_required")
            await load(row()); result = try await read(); check("Missing account rejected", result["status"] as? String == "loading")
            await load(account + row(title: "사진 https://t.co/ABC")); result = try await read()
            check("Title preview strips tracking link", posts(result).first?["title"] as? String == "사진")
            await load(account + row(title: "&amp;lt;색연필&amp;gt;").replacingOccurrences(of: "</a>", with: "</a><div dir='auto'>&lt;색연필&gt;</div>"))
            result = try await read()
            check("Uses rendered text over encoded accessibility preview", posts(result).first?["title"] as? String == "<색연필>")
            await load(account + "<main><div>선택한 기간 동안 게시물이 없습니다.</div></main>")
            result = try await read(); check("Explicit empty result accepted", result["status"] as? String == "empty" && posts(result).isEmpty)
            await load(account + "<main></main>")
            result = try await read(); check("Blank page is not an empty result", result["status"] as? String == "unavailable")

            await load(account + row() + "<input style='display:none' autocomplete='current-password'>")
            result = try await read()
            check("Regression: hidden password must not expire a valid content session", result["status"] as? String == "ready", String(describing: result))
            await load("<button style='display:none' aria-label='계정 메뉴'>@oldAccount</button><button aria-label='계정 메뉴'>@fixture</button>" + row())
            result = try await read()
            check("Regression: hidden account must not override visible account", result["account"] as? String == "@fixture", String(describing: result))
            await load(account + row() + "<input type='text' autocomplete='one-time-code'>")
            result = try await read()
            check("Regression: visible OTP must block content acceptance", result["status"] as? String == "login_required", String(describing: result))
            await load(account + row() + "<button aria-label='Select date range'>2026. 1. 1.</button>")
            result = try await read()
            check("Regression: conflicting rendered date must not be accepted from URL alone", result["status"] as? String != "ready", String(describing: result))

            await load(account + row(title: String(repeating: "a", count: 499) + "😀"))
            do {
                result = try await read()
                check("Regression: Unicode title at 500-unit boundary remains decodable", result["status"] as? String == "ready", String(describing: result["status"]))
            } catch {
                check("Regression: Unicode title at 500-unit boundary remains decodable", false, String(describing: error))
            }
            await load(account + "<a href='/other' aria-label='Profile'>Profile</a>" + row())
            result = try await read()
            check("Conflicting visible identities rejected", result["status"] as? String == "loading" && result["account"] is NSNull)
            for locale in ["ko-KR", "en-US", "ja-JP"] {
                for offset in [0, -1, -6] {
                    let dates = "<button id='range' aria-label='Select date range'></button><script>const a=new Date(),b=new Date();a.setDate(a.getDate()+\(offset));document.getElementById('range').textContent=new Intl.DateTimeFormat('\(locale)',{dateStyle:'medium'}).formatRange(a,b)</script>"
                    await load(account + dates + row(), path: scope(offset))
                    result = try await read()
                    check("Rendered range \(locale) \(offset) accepted", result["status"] as? String == "ready", String(describing: result))
                }
            }
            await load(account + "<button aria-label='Select date range'><svg width='12' height='12'></svg></button>" + row(), path: "https://x.com/i/account_analytics/content?type=posts&sort=impressions&dir=desc&days=7")
            result = try await read()
            check("Canonical 7D icon-only date button accepted", result["status"] as? String == "ready")
            await load(account + row() + "<span>Updating...</span>")
            result = try await read()
            check("Updating overlay rejects old list during transition", result["status"] as? String == "loading")
        } catch { check("Harness execution", false, String(describing: error)) }
        let data = try! JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys])
        print(String(data: data, encoding: .utf8)!)
        exit(results.contains { $0["passed"] as? Bool != true } ? 1 : 0)
    }
}
let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let harness = ContentHarness()
app.delegate = harness
app.run()
