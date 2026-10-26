import AppKit
import Combine
import WebKit
import OSLog
import Network

@MainActor
final class BrowserController: NSObject, ObservableObject, WKNavigationDelegate, WKUIDelegate, NSWindowDelegate {
    static let analyticsURL = URL(string: "https://x.com/i/account_analytics")!
    static let loginURL = URL(string: "https://x.com/i/jf/onboarding/web?redirect_after_login=%2Fi%2Faccount_analytics&mode=login")!
    @Published private(set) var webView: WKWebView
    let content = ContentMonitor()
    @Published private(set) var record: MetricRecord?
    @Published private(set) var message = L10n.text("X에 연결하면 오늘 노출 수가 메뉴바에 표시됩니다.")
    @Published private(set) var connected = false
    @Published private(set) var busy = false
    @Published private(set) var stale = false
    @Published private(set) var disconnecting = false
    @Published private(set) var requiresLogin = false
    @Published private(set) var retryAt: Date?
    @Published var intervalMinutes = UserDefaults.standard.integer(forKey: "refreshMinutes") == 0 ? 10 : UserDefaults.standard.integer(forKey: "refreshMinutes")
    var onChange: (() -> Void)?
    var onLoginNotice: (() -> Void)?
    private var timer: Timer?
    private var scanTask: Task<Void, Never>?
    private var freshness = RefreshGate()
    private var retry = RefreshRetry()
    private var generation: Int { freshness.generation }
    @Published private(set) var networkOnline = false
    @Published private(set) var sleeping = false
    private let pathMonitor = NWPathMonitor()
    private var workspaceObservers: [NSObjectProtocol] = []
    private var activeNavigation: WKNavigation?
    private var navigationGeneration = -1
    private var sessionOperation = UUID()
    private var attemptedLoginRecovery = false
    private var lastReload = Date.distantPast
    private var lastScan = Date.distantPast
    private var dayKey = MetricRecord.dayKey(for: Date())
    private var timeZone = TimeZone.current.identifier
    private var popupWindows: [NSWindow] = []
    private let logger = Logger(subsystem: "com.limod.xbar", category: "Navigation")

    private static func makeWebView(dataStore: WKWebsiteDataStore = .default()) -> WKWebView {
        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = dataStore
        for name in ["PageContext", "Extractor"] {
            if let url = Bundle.main.url(forResource: name, withExtension: "js"),
               let source = try? String(contentsOf: url, encoding: .utf8) {
                configuration.userContentController.addUserScript(WKUserScript(source: source,
                    injectionTime: .atDocumentEnd, forMainFrameOnly: true, in: .defaultClient))
            }
        }
        return WKWebView(frame: NSRect(x: 0, y: 0, width: 1000, height: 650), configuration: configuration)
    }

    override init() {
        webView = Self.makeWebView()
        super.init()
        content.onAccountMismatch = { [weak self] in
            guard let self else { return }
            self.invalidateScan(clearRecord: true)
            self.message = L10n.text("X 계정이 바뀌었거나 로그인이 만료되어 다시 확인합니다.")
            if NavigationPolicy.canAutomaticallyRefresh(self.webView.url), !self.requiresLogin { self.refresh(automatically: true) }
            self.onChange?()
        }
        webView.navigationDelegate = self
        webView.uiDelegate = self
        webView.allowsBackForwardNavigationGestures = true
        timer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in self?.tick() }
        }
        pathMonitor.pathUpdateHandler = { [weak self] path in
            let online = path.status == .satisfied
            Task { @MainActor [weak self] in self?.networkChanged(online: online) }
        }
        pathMonitor.start(queue: DispatchQueue(label: "dev.xbar.reachability"))
        let center = NSWorkspace.shared.notificationCenter
        workspaceObservers.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: nil) { [weak self] _ in
            Task { @MainActor [weak self] in self?.sleepChanged(true) }
        })
        workspaceObservers.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: nil) { [weak self] _ in
            Task { @MainActor [weak self] in self?.sleepChanged(false) }
        })
        if UserDefaults.standard.bool(forKey: "connected") { connect() }
    }

    var hasFreshValue: Bool {
        freshness.isFresh(record, now: Date(), zone: .current, intervalMinutes: intervalMinutes,
                          online: networkOnline, sleeping: sleeping, busy: busy, stale: stale)
    }
    var hasVerifiedAccount: Bool {
        hasFreshValue && record?.account?.range(of: #"\A@[A-Za-z0-9_]{1,15}\z"#, options: .regularExpression) != nil
    }
    var display: String { hasFreshValue ? record!.display : "—" }
    var showsSpinner: Bool { connected && busy && networkOnline && !sleeping && !requiresLogin }
    var statusText: String {
        if !connected { return L10n.text("계정 연결 필요") }
        if sleeping { return L10n.text("잠자기 후 다시 확인합니다") }
        if !networkOnline { return L10n.text("인터넷 연결 대기") }
        if requiresLogin { return L10n.text("X 로그인 필요") }
        if !hasFreshValue {
            if busy { return L10n.text("최신 노출 확인 중") }
            return L10n.text(retryAt == nil ? "X 분석 확인 필요" : "자동 재시도 대기")
        }
        return record.map { L10n.format("checked.time", L10n.time($0.capturedAt)) } ?? ""
    }

    func networkChanged(online: Bool) {
        guard networkOnline != online else { return }
        networkOnline = online
        invalidateForEnvironment()
        message = online ? L10n.text("연결이 복구되어 오늘 값을 다시 확인합니다…") : L10n.text("인터넷 연결을 기다리고 있습니다. 오래된 수치는 숨겼습니다.")
        if online { retry.reset() }
        if online, connected, !sleeping, !disconnecting, !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { refresh(automatically: true) }
        onChange?()
    }
    func sleepChanged(_ value: Bool) {
        sleeping = value
        invalidateForEnvironment()
        message = value ? L10n.text("잠자기에서 돌아오면 다시 확인합니다.") : L10n.text("잠자기에서 돌아와 오늘 값을 다시 확인합니다…")
        if !value { retry.reset() }
        if !value, networkOnline, connected, !disconnecting, !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { refresh(automatically: true) }
        onChange?()
    }
    private func invalidateForEnvironment() {
        invalidateScan(clearRecord: false); activeNavigation = nil
        webView.stopLoading(); stale = true; content.suspend()
    }
    var menuTitle: String { MetricRecord.menuTitle(for: display) }
    var updatedText: String {
        guard let record else { return L10n.text("아직 읽은 값이 없습니다") }
        return L10n.format("checked.zone", L10n.time(record.capturedAt), record.timeZone)
    }

    func connect() {
        guard !disconnecting else { return }
        connected = true
        requiresLogin = false
        attemptedLoginRecovery = false
        UserDefaults.standard.set(true, forKey: "connected")
        message = L10n.text("오늘 노출 수를 확인하고 있습니다…")
        refresh()
    }

    func importSafariSession(_ cookies: [SessionCookie]) async -> Bool {
        guard !disconnecting, !Task.isCancelled else { return false }
        do { try SessionTransfer.validate(cookies) } catch { message = L10n.text("Safari 로그인 정보를 확인할 수 없습니다."); onChange?(); return false }
        // Validate/construct everything before touching the user's existing session.
        let incoming = cookies.compactMap { cookie -> HTTPCookie? in
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: cookie.name, .value: cookie.value, .domain: cookie.domain,
                .path: "/", .secure: "TRUE", HTTPCookiePropertyKey("HttpOnly"): cookie.httpOnly ? "TRUE" : "FALSE"
            ]
            if let expiry = cookie.expirationDate { properties[.expires] = Date(timeIntervalSince1970: expiry) }
            return HTTPCookie(properties: properties)
        }
        guard incoming.count == cookies.count, !Task.isCancelled else { return false }
        let operation = UUID(); sessionOperation = operation
        let wasConnected = connected, neededLogin = requiresLogin
        let oldMessage = message
        disconnecting = true
        invalidateScan(clearRecord: false)
        stale = record != nil
        content.suspend(); webView.stopLoading()
        for popup in popupWindows { popup.close() }
        popupWindows.removeAll()
        // Keep the old view available for cancellation; page scripts must stop writing cookies.
        let previousView = webView
        previousView.navigationDelegate = nil; previousView.uiDelegate = nil
        previousView.loadHTMLString("", baseURL: nil)
        activeNavigation = nil; navigationGeneration = -1
        message = L10n.text("Safari 계정을 연결하고 있습니다…")
        onChange?()
        let dataStore = previousView.configuration.websiteDataStore
        let store = dataStore.httpCookieStore
        let imported = await CookieReplacement.replace(with: incoming, read: {
            await store.allCookies().filter { ["x.com", ".x.com", "twitter.com", ".twitter.com"].contains($0.domain) }
        }, delete: { await store.deleteCookie($0) }, write: { await store.setCookie($0) },
           isCurrent: { self.sessionOperation == operation }, commit: {
            self.disconnecting = false
            self.webView = Self.makeWebView(dataStore: dataStore)
            self.webView.navigationDelegate = self; self.webView.uiDelegate = self
            self.webView.allowsBackForwardNavigationGestures = true
            self.record = nil
            UserDefaults.standard.set("safari", forKey: "connectionMethod")
            self.connect()
        })
        guard operation == sessionOperation else { return false }
        if !imported {
            disconnecting = false
            webView = Self.makeWebView(dataStore: dataStore)
            webView.navigationDelegate = self; webView.uiDelegate = self
            webView.allowsBackForwardNavigationGestures = true
            connected = wasConnected; requiresLogin = neededLogin; message = oldMessage
            // The old cookie snapshot is restored. Revalidate without forcing sign-in.
            if wasConnected { refresh() }
            onChange?()
            return false
        }
        return true
    }

    func refresh(automatically: Bool = false) {
        guard connected, !disconnecting else { return }
        guard networkOnline, !sleeping else { invalidateForEnvironment(); onChange?(); return }
        if !automatically { retry.reset() }
        guard retry.begin(at: Date()) else { scheduleRetry(); return }
        invalidateScan(clearRecord: false)
        retryAt = nil
        requiresLogin = false
        busy = true
        stale = record != nil
        message = L10n.text("오늘 노출 수를 확인하고 있습니다…")
        lastReload = Date()
        if NavigationPolicy.isOverview(webView.url) { activeNavigation = webView.reloadFromOrigin() }
        else { activeNavigation = webView.load(URLRequest(url: Self.analyticsURL, cachePolicy: .reloadIgnoringLocalCacheData)) }
        onChange?()
    }

    func openXLogin() {
        guard connected, !disconnecting else { return }
        invalidateScan(clearRecord: true)
        requiresLogin = true
        attemptedLoginRecovery = true // An explicit sign-in action must not be redirected automatically.
        for window in popupWindows { window.close() }
        popupWindows.removeAll()
        message = L10n.text("아래 X 화면의 이메일 또는 사용자 아이디로 로그인해 주세요. Google 로그인은 지원하지 않습니다.")
        webView.load(URLRequest(url: Self.loginURL))
        onChange?()
    }

    private func unsupportedGoogleLogin() {
        message = L10n.text("Google 로그인은 지원하지 않습니다. X 화면의 이메일 또는 사용자 아이디로 로그인해 주세요.")
        onChange?()
        onLoginNotice?()
    }

    func setInterval(_ minutes: Int) {
        guard [5, 10, 30].contains(minutes) else { return }
        intervalMinutes = minutes
        UserDefaults.standard.set(minutes, forKey: "refreshMinutes")
        onChange?()
    }

    func disconnect() {
        guard !disconnecting else { return }
        disconnecting = true
        connected = false
        requiresLogin = false
        content.suspend()
        invalidateScan(clearRecord: true)
        webView.stopLoading()
        for window in popupWindows { window.close() }
        popupWindows.removeAll()
        UserDefaults.standard.removeObject(forKey: "connected")
        message = L10n.text("이 앱의 X 로그인 정보를 지우고 있습니다…")
        webView.loadHTMLString("", baseURL: nil)
        WKWebsiteDataStore.default().removeData(ofTypes: WKWebsiteDataStore.allWebsiteDataTypes(), modifiedSince: .distantPast) { [weak self] in
            Task { @MainActor [weak self] in
                guard let self else { return }
                self.disconnecting = false
                self.message = L10n.text("연결을 해제했습니다. 이 앱의 웹 로그인 정보와 노출 수를 지웠습니다.")
                self.onChange?()
            }
        }
        onChange?()
    }

    func stop() {
        connected = false; disconnecting = false; sessionOperation = UUID()
        pathMonitor.cancel()
        for observer in workspaceObservers { NSWorkspace.shared.notificationCenter.removeObserver(observer) }
        workspaceObservers.removeAll()
        content.reset()
        timer?.invalidate()
        timer = nil
        invalidateScan(clearRecord: true)
        webView.stopLoading()
        for window in popupWindows { window.close() }
        popupWindows.removeAll()
    }

    private func invalidateScan(clearRecord: Bool) {
        freshness.invalidate()
        scanTask?.cancel()
        scanTask = nil
        busy = false
        retryAt = nil
        if clearRecord { record = nil; stale = false }
    }

    private func scheduleRetry() {
        busy = false
        retryAt = max(Date(), retry.nextAttempt)
        onChange?()
    }

    private func tick() {
        let now = Date()
        let currentDay = MetricRecord.dayKey(for: now)
        let currentZone = TimeZone.current.identifier
        if currentDay != dayKey || currentZone != timeZone {
            dayKey = currentDay
            timeZone = currentZone
            content.reset()
            invalidateScan(clearRecord: true)
            message = L10n.text("날짜가 바뀌어 오늘 값을 다시 확인합니다.")
            if connected, !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { refresh(automatically: true) }
            onChange?()
        }
        if connected, hasFreshValue {
            content.refreshIfNeeded(account: record?.account)
        }
        guard connected, !disconnecting, networkOnline, !sleeping else { return }
        if webView.isLoading, now.timeIntervalSince(lastReload) > 45 {
            invalidateForEnvironment(); message = L10n.text("응답을 기다리고 있습니다. 잠시 후 다시 확인합니다."); scheduleRetry()
        }
        if scanTask != nil, now.timeIntervalSince(lastScan) > 30 {
            // Cancellation alone cannot stop a stalled WebKit callback. Invalidate
            // its generation so a late result cannot restore an old reading.
            invalidateForEnvironment()
            message = L10n.text("페이지를 읽지 못했습니다. 자동으로 다시 시도합니다.")
            scheduleRetry()
        }
        guard !webView.isLoading, scanTask == nil else { return }
        if let retryAt {
            if now >= retryAt, !requiresLogin {
                // A loaded page may have gained a sign-in form or draft while
                // waiting. Recheck its current context before navigating away.
                if freshness.loaded { scan() }
                else if NavigationPolicy.canAutomaticallyRefresh(webView.url) { refresh(automatically: true) }
            }
            return
        }
        if !freshness.loaded {
            if now.timeIntervalSince(lastReload) >= 30, !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { refresh(automatically: true) }
            return
        }
        // Never refresh a password / verification form on a timer.
        if NavigationPolicy.isAnalytics(webView.url),
           now.timeIntervalSince(lastReload) >= Double(intervalMinutes * 60) {
            refresh(automatically: true)
        } else if now.timeIntervalSince(lastScan) >= 15 {
            scan()
        }
    }

    private func read(advance: Bool) async throws -> PageReading {
        let method = advance ? "advance" : "snapshot"
        let script = "JSON.stringify(globalThis.TodayBarExtractor ? TodayBarExtractor.\(method)() : {status:'loading'})"
        let string: String = try await withCheckedThrowingContinuation { continuation in
            webView.evaluateJavaScript(script, in: nil, in: .defaultClient) { result in
                switch result {
                case .success(let value):
                    if let text = value as? String { continuation.resume(returning: text) }
                    else { continuation.resume(throwing: CocoaError(.coderInvalidValue)) }
                case .failure(let error): continuation.resume(throwing: error)
                }
            }
        }
        guard let data = string.data(using: .utf8) else { throw CocoaError(.coderInvalidValue) }
        return try JSONDecoder().decode(PageReading.self, from: data)
    }

    private func scan() {
        guard connected, networkOnline, !sleeping, freshness.loaded, scanTask == nil, !webView.isLoading else { return }
        let token = generation
        lastScan = Date()
        scanTask = Task { [weak self] in
            guard let self else { return }
            defer { if token == self.generation { self.scanTask = nil; self.busy = false; self.onChange?() } }
            var previous: PageReading?
            for _ in 0..<14 {
                guard !Task.isCancelled, self.freshness.canRead(token), self.connected, self.networkOnline, !self.sleeping else { return }
                do {
                    let reading = try await self.read(advance: true)
                    guard !Task.isCancelled, self.freshness.canRead(token), self.connected, self.networkOnline, !self.sleeping else { return }
                    if let reason = reading.action {
                        self.logger.info("Extraction stage: \(reason, privacy: .public)")
                    }
                    if !["login_required", "login_session_detected"].contains(reading.status) {
                        self.requiresLogin = false
                    }
                    if let old = self.record, old.account != reading.account {
                        if reading.account != nil { self.content.reset() }
                        self.record = nil
                        self.stale = false
                    }
                    if self.retryAt != nil, NavigationPolicy.isOverview(self.webView.url),
                       !["login_required", "login_session_detected"].contains(reading.status) {
                        if reading.canRecover == false {
                            self.retryAt = nil
                            self.message = L10n.text("X 화면을 확인해 주세요. 입력 중인 내용은 유지합니다.")
                            return
                        }
                        self.refresh(automatically: true)
                        return
                    }
                    switch reading.status {
                    case "ready":
                        guard let account = reading.account,
                              account.range(of: #"\A@[A-Za-z0-9_]{1,15}\z"#, options: .regularExpression) != nil,
                              let value = reading.display, MetricRecord.isValidDisplay(value),
                              reading.dayKey == MetricRecord.dayKey(for: Date()),
                              let zoneName = reading.timeZone, let zone = TimeZone(identifier: zoneName),
                              zone.secondsFromGMT() == TimeZone.current.secondsFromGMT() else {
                            self.record = nil
                            self.message = L10n.text("날짜와 시간대를 확인할 수 없습니다. X에서 오늘 범위를 선택해 주세요.")
                            return
                        }
                        // Require two matching samples, after the calendar's settling period.
                        if previous?.status == "ready", previous?.display == value,
                           previous?.dayKey == reading.dayKey, previous?.account == reading.account {
                            if self.record?.display != value || self.record?.dayKey != reading.dayKey ||
                               self.record?.account != reading.account || self.stale {
                                self.record = MetricRecord(display: value, dayKey: reading.dayKey!,
                                    timeZone: TimeZone.current.identifier, account: reading.account, capturedAt: Date())
                                self.logger.notice("Accepted today reading; window visible: \(self.webView.window?.isVisible == true, privacy: .public)")
                            }
                            guard self.freshness.accept(token) else { return }
                            self.retry.reset(); self.retryAt = nil
                            self.stale = false
                            self.attemptedLoginRecovery = false
                            self.content.resume()
                            self.message = L10n.text("X의 오늘 노출 수입니다. 축약된 숫자는 화면 표시 그대로 보여줍니다.")
                            self.content.refreshIfNeeded(account: reading.account)
                            return
                        }
                    case "selecting", "loading":
                        self.busy = true
                        self.message = L10n.text("X에서 오늘 범위를 선택하고 값을 기다리고 있습니다…")
                    case "login_required", "login_session_detected":
                        // X can show its onboarding sheet over an already signed-in home page.
                        // Confirm that session through analytics once, without touching credentials.
                        if reading.status == "login_session_detected", !self.attemptedLoginRecovery {
                            self.attemptedLoginRecovery = true
                            self.refresh()
                            return
                        }
                        self.requiresLogin = true
                        self.retryAt = nil
                        self.content.reset()
                        self.record = nil
                        self.stale = false
                        self.message = L10n.text("X 화면에서 로그인해 주세요. 인증 절차는 X 화면에서 직접 진행합니다.")
                        return
                    case "off_page":
                        if NavigationPolicy.canRecoverAnalytics(self.webView.url, reading: reading) {
                            self.message = L10n.text("분석 화면으로 자동 복귀합니다.")
                            self.refresh(automatically: true)
                            return
                        }
                        if NavigationPolicy.isAnalytics(self.webView.url) {
                            self.busy = false
                            self.message = L10n.text("다른 분석 화면을 보고 있습니다. 새로고침하면 오늘 요약으로 돌아갑니다.")
                            if self.webView.window?.isVisible != true { self.refresh(automatically: true) }
                            return
                        }
                        self.record = nil
                        self.stale = false
                        self.retryAt = nil
                        self.message = L10n.text("X 화면을 확인해 주세요. 입력 중인 내용은 유지합니다.")
                        return
                    case "wrong_day", "manual_date":
                        self.record = nil
                        self.stale = false
                        self.message = L10n.text("X 날짜 선택에서 시작일과 종료일을 모두 오늘로 지정해 주세요.")
                        return
                    case "unavailable":
                        self.record = nil
                        self.stale = false
                        self.busy = true
                        self.message = L10n.text("분석 화면이 준비되기를 기다리고 있습니다…")
                    default:
                        self.record = nil
                        self.stale = false
                        self.message = L10n.text("노출 카드를 찾지 못했습니다. 로그인·분석 접근 권한·페이지 로딩을 확인해 주세요.")
                        return
                    }
                    previous = reading
                    try await Task.sleep(for: .seconds(1))
                } catch {
                    guard !Task.isCancelled, token == self.generation else { return }
                    self.busy = false
                    self.freshness.invalidate(); self.scanTask = nil
                    self.stale = true
                    self.message = L10n.text("페이지를 읽지 못했습니다. 자동으로 다시 시도합니다.")
                    if !self.requiresLogin, NavigationPolicy.canAutomaticallyRefresh(self.webView.url) { self.scheduleRetry() }
                    return
                }
            }
            self.record = nil
            self.message = L10n.text("오늘 값을 아직 확인하지 못했습니다. X 날짜 범위를 확인해 주세요.")
            if !self.requiresLogin, NavigationPolicy.canAutomaticallyRefresh(self.webView.url) { self.scheduleRetry() }
        }
    }

    func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
        guard webView === self.webView else { return }
        guard connected, networkOnline, !sleeping else { return }
        invalidateScan(clearRecord: false)
        activeNavigation = navigation; navigationGeneration = generation
        lastReload = Date()
        busy = connected; stale = true
        onChange?()
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        guard webView === self.webView, let navigation, navigation === activeNavigation,
              networkOnline, !sleeping, freshness.didLoad(navigationGeneration) else { return }
        busy = false
        scan()
    }

    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        if navigation === activeNavigation { navigationFailed(webView, error) }
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        if navigation === activeNavigation { navigationFailed(webView, error) }
    }

    private func navigationFailed(_ view: WKWebView, _ error: Error) {
        guard view === webView, (error as NSError).code != NSURLErrorCancelled, connected else { return }
        invalidateForEnvironment()
        message = L10n.text("X 페이지를 열지 못했습니다. 네트워크를 확인하고 다시 시도해 주세요.")
        if !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { scheduleRetry() }
        onChange?()
    }

    func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
        guard webView === self.webView else { return }
        invalidateForEnvironment()
        message = L10n.text("웹 화면이 종료되어 자동으로 다시 연결합니다.")
        if !requiresLogin, NavigationPolicy.canAutomaticallyRefresh(webView.url) { scheduleRetry() }
        onChange?()
    }

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction,
                 decisionHandler: @escaping @MainActor (WKNavigationActionPolicy) -> Void) {
        guard let url = navigationAction.request.url else { decisionHandler(.cancel); return }
        if url.absoluteString == "about:blank" || (url.scheme == "about" && navigationAction.targetFrame?.isMainFrame == false) {
            decisionHandler(.allow)
            return
        }
        if NavigationPolicy.isGoogle(url), navigationAction.targetFrame?.isMainFrame != false {
            decisionHandler(.cancel)
            unsupportedGoogleLogin()
            return
        }
        guard NavigationPolicy.allows(url), !navigationAction.shouldPerformDownload else {
            decisionHandler(.cancel)
            let origin = url.host ?? url.scheme ?? "unknown"
            logger.notice("Blocked navigation origin: \(origin, privacy: .public)")
            message = L10n.format("login.open.failed", origin)
            onChange?()
            return
        }
        decisionHandler(.allow)
    }

    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                 for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        guard let url = navigationAction.request.url, NavigationPolicy.allows(url) else { return nil }
        if NavigationPolicy.isGoogle(url) { unsupportedGoogleLogin(); return nil }
        return makeLoginPopup(configuration: configuration)
    }

    func makeLoginPopup(configuration: WKWebViewConfiguration) -> WKWebView {
        let popup = WKWebView(frame: NSRect(x: 0, y: 0, width: 600, height: 740), configuration: configuration)
        popup.navigationDelegate = self
        popup.uiDelegate = self
        let window = NSWindow(contentRect: popup.frame, styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = L10n.text("X 로그인")
        window.contentView = popup
        window.delegate = self
        window.isReleasedWhenClosed = false
        window.center()
        window.makeKeyAndOrderFront(nil)
        popupWindows.append(window)
        return popup
    }

    func windowWillClose(_ notification: Notification) {
        guard let window = notification.object as? NSWindow,
              popupWindows.contains(where: { $0 === window }) else { return }
        if let popup = window.contentView as? WKWebView {
            popup.stopLoading(); popup.navigationDelegate = nil; popup.uiDelegate = nil
        }
        window.contentView = nil
        window.delegate = nil
        popupWindows.removeAll { $0 === window }
    }

    func webViewDidClose(_ webView: WKWebView) {
        popupWindows.first(where: { $0.contentView === webView })?.close()
        popupWindows.removeAll(where: { $0.contentView === webView })
    }
}
