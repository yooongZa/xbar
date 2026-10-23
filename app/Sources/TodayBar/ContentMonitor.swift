import AppKit
import Combine
import WebKit
import OSLog

@MainActor
final class ContentMonitor: NSObject, ObservableObject, WKNavigationDelegate {
    @Published private(set) var period = ContentPeriod(rawValue: UserDefaults.standard.string(forKey: "contentPeriod") ?? "") ?? .today
    @Published private(set) var ledger = PostLedger()
    @Published private(set) var busy = false
    @Published private(set) var message = L10n.text("연결 후 콘텐츠를 확인합니다.")
    @Published private(set) var updatedAt: Date?
    @Published private(set) var stale = false
    @Published private(set) var surges: [RepostSurge] = []
    @Published private(set) var unread = false
    @Published var effectsEnabled = UserDefaults.standard.object(forKey: "rtEffects") as? Bool ?? true {
        didSet {
            UserDefaults.standard.set(effectsEnabled, forKey: "rtEffects")
            if !effectsEnabled { unread = false; onChange?() }
        }
    }
    var onChange: (() -> Void)?
    var onSurge: (() -> Void)?
    var onAccountMismatch: (() -> Void)?
    private var paused = true
    private var contextKey = ""
    private var lastAttempt = Date.distantPast
    private var task: Task<Void, Never>?
    private var generation = 0
    private var reader: WKWebView?
    private let logger = Logger(subsystem: "com.limod.xbar", category: "Content")

    func selectPeriod(_ selected: ContentPeriod, account: String?) {
        guard period != selected else { return }
        // Cancel before changing scope; an old web callback must never populate the new selection.
        reset()
        period = selected
        UserDefaults.standard.set(selected.rawValue, forKey: "contentPeriod")
        refreshIfNeeded(account: account, force: true)
    }

    func resume() { paused = false }
    func suspend() {
        paused = true
        reset()
        stale = true
        message = L10n.text("연결이 복구되면 콘텐츠를 다시 확인합니다.")
        onChange?()
    }

    func refreshIfNeeded(account expected: String?, force: Bool = false) {
        guard !paused else { return }
        let active = surges.filter { Date().timeIntervalSince($0.detectedAt) < 1800 }
        if active != surges { surges = active; if active.isEmpty { unread = false; onChange?() } }
        guard let expected, expected.range(of: #"\A@[A-Za-z0-9_]{1,15}\z"#, options: .regularExpression) != nil else { return }
        let range = period.range(at: Date())
        let key = "\(expected.lowercased())|\(range.key)|\(TimeZone.current.identifier)"
        if contextKey != key { reset(); contextKey = key }
        guard task == nil, force || Date().timeIntervalSince(lastAttempt) >= 600 else { return }
        lastAttempt = Date()
        busy = true
        stale = updatedAt != nil
        message = L10n.text("게시물 목록을 확인하는 중…")
        let token = generation
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .default()
        guard let sourceURL = Bundle.main.url(forResource: "ContentExtractor", withExtension: "js"),
              let contextURL = Bundle.main.url(forResource: "PageContext", withExtension: "js"),
              let contextSource = try? String(contentsOf: contextURL, encoding: .utf8),
              let extractorSource = try? String(contentsOf: sourceURL, encoding: .utf8) else {
            busy = false; message = L10n.text("콘텐츠 읽기 도구를 불러오지 못했습니다."); return
        }
        config.userContentController.addUserScript(WKUserScript(source: contextSource + "\n" + extractorSource, injectionTime: .atDocumentEnd,
            forMainFrameOnly: true, in: .defaultClient))
        // A temporary reader keeps the user's analysis window and the aggregate reader independent.
        let view = WKWebView(frame: NSRect(x: 0, y: 0, width: 1200, height: 1600), configuration: config)
        view.navigationDelegate = self
        reader = view
        view.load(URLRequest(url: range.url, cachePolicy: .reloadIgnoringLocalCacheData))
        task = Task { [weak self] in
            guard let self else { return }
            defer {
                if token == self.generation {
                    view.stopLoading(); view.navigationDelegate = nil
                    self.reader = nil; self.task = nil; self.busy = false; self.onChange?()
                }
            }
            var previous: ContentReading?
            for _ in 0..<30 {
                do {
                    try await Task.sleep(for: .seconds(1))
                    guard token == self.generation, !Task.isCancelled else { return }
                    if view.isLoading { continue }
                    let reading = try await self.read(view)
                    guard token == self.generation, !Task.isCancelled else { return }
                    if ["login_required", "login_session_detected"].contains(reading.status) || (reading.account != nil && reading.account?.lowercased() != expected.lowercased()) {
                        self.reset()
                        self.message = L10n.text("계정 연결을 다시 확인해 주세요.")
                        self.onAccountMismatch?()
                        return
                    }
                    guard ["ready", "empty"].contains(reading.status), reading.account?.lowercased() == expected.lowercased(),
                          reading.dayKey == MetricRecord.dayKey(for: Date()), reading.range == range,
                          range == self.period.range(at: Date()),
                          let posts = reading.posts, (reading.status == "empty") == posts.isEmpty, posts.count <= 20,
                          posts.allSatisfy(\.isValid), Set(posts.map(\.id)).count == posts.count else {
                        previous = nil; continue
                    }
                    if reading.hasSameScopeAndPosts(as: previous) {
                        let now = Date()
                        let events = self.ledger.ingest(posts, account: expected, at: now)
                        self.surges = self.surges.filter { now.timeIntervalSince($0.detectedAt) < 1800 }
                        for event in events {
                            self.surges.removeAll { $0.id == event.id }
                            self.surges.append(event)
                        }
                        self.updatedAt = now; self.stale = false
                        self.message = posts.isEmpty
                            ? (self.period == .today ? L10n.text("X 분석에 오늘 게시물이 아직 표시되지 않습니다.") : L10n.format("posts.empty.period", self.period.title))
                            : L10n.text("게시물 목록을 확인했습니다.")
                        if posts.isEmpty { self.surges = []; self.unread = false }
                        self.logger.notice("Accepted content reading: \(posts.count, privacy: .public) posts; temporary reader released after capture")
                        if !events.isEmpty && self.effectsEnabled { self.unread = true; self.onSurge?() }
                        return
                    }
                    previous = reading
                } catch {
                    if Task.isCancelled || token != self.generation { return }
                    previous = nil
                }
            }
            self.message = self.ledger.rows.isEmpty
                ? L10n.text("게시물 목록을 확인하지 못했습니다. 잠시 후 새로고침해 주세요.")
                : L10n.text("게시물 목록을 갱신하지 못했습니다. 마지막으로 확인한 목록을 표시합니다.")
            self.stale = true
        }
        onChange?()
    }

    private func read(_ view: WKWebView) async throws -> ContentReading {
        let text: String = try await withCheckedThrowingContinuation { continuation in
            view.evaluateJavaScript("JSON.stringify(globalThis.TodayBarContentExtractor?.snapshot() || {status:'loading'})",
                in: nil, in: .defaultClient) { result in
                switch result {
                case .success(let value):
                    if let value = value as? String { continuation.resume(returning: value) }
                    else { continuation.resume(throwing: CocoaError(.coderInvalidValue)) }
                case .failure(let error): continuation.resume(throwing: error)
                }
            }
        }
        return try JSONDecoder().decode(ContentReading.self, from: Data(text.utf8))
    }

    func markRead() { unread = false; onChange?() }
    func reset() {
        generation += 1; task?.cancel(); task = nil
        reader?.stopLoading(); reader?.navigationDelegate = nil; reader = nil
        ledger.reset(); surges = []; unread = false; updatedAt = nil; busy = false; stale = false
        contextKey = ""; lastAttempt = .distantPast
        message = L10n.text("연결 후 콘텐츠를 확인합니다.")
        onChange?()
    }

    func webView(_ webView: WKWebView, decidePolicyFor action: WKNavigationAction,
                 decisionHandler: @escaping @MainActor (WKNavigationActionPolicy) -> Void) {
        guard let url = action.request.url, url.scheme == "https", ["x.com", "www.x.com"].contains(url.host ?? ""),
              !action.shouldPerformDownload else { decisionHandler(.cancel); return }
        decisionHandler(.allow)
    }
}
