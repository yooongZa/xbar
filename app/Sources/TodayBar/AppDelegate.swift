import AppKit
import SwiftUI
import WebKit
import QuartzCore

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var controller: BrowserController!
    private var statusItem: NSStatusItem!
    private var window: NSWindow!
    private let popover = NSPopover()
    private var postWindow: NSWindow?
    private var setupWindow: NSWindow?
    private let safari = SafariConnection()
    private var symbolTimer: Timer?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        controller = BrowserController()
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.font = .monospacedDigitSystemFont(ofSize: 12, weight: .medium)
        statusItem.button?.target = self
        statusItem.button?.action = #selector(togglePopover)
        popover.behavior = .transient
        popover.animates = !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        controller.onChange = { [weak self] in self?.updateStatus() }
        controller.content.onChange = { [weak self] in self?.updateStatus() }
        controller.content.onSurge = { [weak self] in self?.showSurgeEffect() }
        controller.onLoginNotice = { [weak self] in
            self?.showWindow()
            let alert = NSAlert()
            alert.messageText = L10n.text("X 아이디로 로그인해 주세요")
            alert.informativeText = L10n.text("Google 로그인은 이 앱의 내장 브라우저에서 지원하지 않습니다. X 화면의 이메일 또는 사용자 아이디 입력란을 이용해 주세요.")
            alert.addButton(withTitle: L10n.text("X 로그인 화면으로"))
            alert.addButton(withTitle: L10n.text("닫기"))
            if alert.runModal() == .alertFirstButtonReturn { self?.controller.openXLogin() }
        }
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1060, height: 840),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = L10n.text("𝕏-Bar · X 연결")
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.contentView = NSHostingView(rootView: DashboardView(controller: controller, hide: { [weak self] in self?.window.orderOut(nil) }))
        window.center()
        configureApplicationMenu()
        safari.onReceive = { [weak self] cookies in
            guard let self else { return false }
            self.postWindow?.close(); self.postWindow = nil
            let imported = await self.controller.importSafariSession(cookies)
            guard !Task.isCancelled else { return false }
            self.setupWindow?.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return imported
        }
        updateStatus()
        if !UserDefaults.standard.bool(forKey: "setupCompleted") { showSetup() }

    }

    private func configureApplicationMenu() {
        let main = NSMenu()
        let appItem = NSMenuItem()
        main.addItem(appItem)
        let app = NSMenu()
        appItem.submenu = app
        let panelItem = app.addItem(withTitle: L10n.text("메뉴바 패널 열기"), action: #selector(togglePopover), keyEquivalent: "m")
        panelItem.target = self
        panelItem.keyEquivalentModifierMask = [.command, .shift]
        app.addItem(withTitle: L10n.text("X 분석 화면 열기"), action: #selector(showWindow), keyEquivalent: "o").target = self
        app.addItem(withTitle: L10n.text("로그인 · 시작 안내"), action: #selector(showSetup), keyEquivalent: "").target = self
        app.addItem(.separator())
        app.addItem(withTitle: L10n.text("𝕏-Bar 종료"), action: #selector(quit), keyEquivalent: "q").target = self
        let editItem = NSMenuItem()
        main.addItem(editItem)
        editItem.title = L10n.text("편집")
        let edit = NSMenu(title: L10n.text("편집"))
        editItem.submenu = edit
        for (title, selector, key) in [(L10n.text("실행 취소"), Selector(("undo:")), "z"), (L10n.text("잘라내기"), #selector(NSText.cut(_:)), "x"),
                                      (L10n.text("복사"), #selector(NSText.copy(_:)), "c"), (L10n.text("붙여넣기"), #selector(NSText.paste(_:)), "v"),
                                      (L10n.text("모두 선택"), #selector(NSText.selectAll(_:)), "a")] {
            edit.addItem(withTitle: title, action: selector, keyEquivalent: key)
        }
        NSApp.mainMenu = main
    }

    private func updateStatus() {
        drawStatus()
        let animate = !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion &&
            (controller.showsSpinner || hasRainbow)
        if animate, symbolTimer == nil {
            symbolTimer = Timer.scheduledTimer(withTimeInterval: 1.0 / 12, repeats: true) { [weak self] _ in
                Task { @MainActor [weak self] in
                    guard let self else { return }
                    if self.controller.showsSpinner || self.hasRainbow { self.drawStatus() }
                    else { self.symbolTimer?.invalidate(); self.symbolTimer = nil; self.drawStatus() }
                }
            }
        } else if !animate { symbolTimer?.invalidate(); symbolTimer = nil }
    }
    private var hasRainbow: Bool {
        controller.hasFreshValue && controller.content.effectsEnabled &&
        controller.content.surges.contains { Date().timeIntervalSince($0.detectedAt) < 1800 }
    }
    private func drawStatus() {
        guard let button = statusItem.button else { return }
        let reduce = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        let phase = Date.timeIntervalSinceReferenceDate.truncatingRemainder(dividingBy: 2) / 2
        let style: StatusSymbol.Style = controller.showsSpinner ? .loading : (hasRainbow ? .rainbow : .plain)
        button.image = StatusSymbol.image(style: style, phase: phase, reduceMotion: reduce)
        button.imagePosition = .imageLeading
        button.title = controller.hasFreshValue ? " " + controller.display : ""
        button.contentTintColor = nil
        button.toolTip = "𝕏-Bar · \(controller.statusText)\n\(controller.message)"
        button.setAccessibilityLabel(controller.hasFreshValue ? L10n.format("accessibility.today", controller.display) : "𝕏-Bar \(controller.statusText)")
    }

    @objc private func togglePopover() {
        if popover.isShown { popover.performClose(nil); return }
        guard let button = statusItem.button else { return }
        controller.content.markRead()
        popover.contentViewController = NSHostingController(rootView: MenuPopoverView(controller: controller,
            content: controller.content, openPost: { [weak self] in self?.openPost($0) },
            openOverview: { [weak self] in self?.showWindow() },
            privacy: { [weak self] in self?.showPrivacy() },
            disconnect: { [weak self] in self?.disconnect() }, quit: { [weak self] in self?.quit() },
            setup: { [weak self] in self?.showSetup() }, follow: { [weak self] in self?.followCreator() }))
        popover.contentSize = NSSize(width: MenuPopoverView.width, height: MenuPopoverView.height)
        popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        popover.contentViewController?.view.window?.makeKey()
    }

    private func openPost(_ post: ContentPost) {
        guard controller.connected, controller.content.ledger.rows.contains(where: { $0.id == post.id }),
              let url = post.analyticsURL else { return }
        popover.performClose(nil)
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .default()
        let web = WKWebView(frame: NSRect(x: 0, y: 0, width: 1020, height: 800), configuration: config)
        web.navigationDelegate = controller
        web.uiDelegate = controller
        if postWindow == nil {
            let detail = NSWindow(contentRect: web.frame, styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
            detail.isReleasedWhenClosed = false
            detail.delegate = self
            detail.center()
            postWindow = detail
        }
        postWindow?.title = L10n.format("window.post", String(post.displayTitle.prefix(45)))
        (postWindow?.contentView as? WKWebView)?.stopLoading()
        postWindow?.contentView = web
        web.load(URLRequest(url: url))
        NSApp.activate(ignoringOtherApps: true)
        postWindow?.makeKeyAndOrderFront(nil)
    }

    private func showSurgeEffect() { updateStatus() }

    @objc func showWindow() {
        popover.performClose(nil)
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    @objc private func showSetup() {
        popover.performClose(nil)
        if setupWindow == nil {
            let setup = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 510, height: 560),
                styleMask: [.titled, .closable], backing: .buffered, defer: false)
            setup.title = L10n.text("𝕏-Bar 시작하기"); setup.isReleasedWhenClosed = false; setup.delegate = self
            setup.contentView = NSHostingView(rootView: SetupView(controller: controller, safari: safari,
                directLogin: { [weak self] in
                    guard let self else { return }
                    self.safari.cancel()
                    UserDefaults.standard.set("direct", forKey: "connectionMethod")
                    self.controller.connect(); self.showWindow()
                }, finish: { [weak self] in
                    guard let self, self.controller.hasVerifiedAccount else { return }
                    UserDefaults.standard.set(true, forKey: "setupCompleted")
                    self.safari.cancel(); self.setupWindow?.orderOut(nil); self.setupWindow = nil; self.window.orderOut(nil)
                    self.togglePopover()
                }, follow: { [weak self] in self?.followCreator() }, directAnalysis: { [weak self] in self?.showWindow() }))
            setup.center(); setupWindow = setup
        }
        NSApp.activate(ignoringOtherApps: true); setupWindow?.makeKeyAndOrderFront(nil)
    }

    private func followCreator() {
        // Opens the public profile; following remains the user's action on X.
        NSWorkspace.shared.open(URL(string: "https://x.com/yongZa_R")!)
    }

    @objc private func disconnect() {
        showWindow()
        let alert = NSAlert()
        alert.messageText = L10n.text("이 앱의 X 연결을 해제할까요?")
        alert.informativeText = L10n.text("𝕏-Bar에 저장된 웹 로그인 정보와 현재 노출 수를 삭제합니다. Safari와 Chrome의 로그인에는 영향이 없습니다.")
        alert.addButton(withTitle: L10n.text("연결 해제"))
        alert.addButton(withTitle: L10n.text("취소"))
        if alert.runModal() == .alertFirstButtonReturn {
            postWindow?.close(); postWindow = nil
            safari.cancel()
            controller.disconnect()
        }
    }

    @objc private func showPrivacy() {
        showWindow()
        let alert = NSAlert()
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.5.0"
        alert.messageText = "𝕏-Bar \(version)"
        alert.informativeText = L10n.text("X 분석 화면의 오늘 총노출, 게시물 미리보기·노출·좋아요·재게시 수를 읽습니다. Safari 연결을 선택하면 사용자 동의 후 X 로그인 쿠키 3종을 암호화해 앱으로 전달합니다. 웹 로그인 정보는 이 Mac의 앱 저장소에 보관하며, 읽은 콘텐츠와 비교 기록은 메모리에서만 처리합니다. 개발자 서버·광고·추적 SDK를 사용하지 않습니다. X 및 로그인 제공자와의 웹 통신에는 각 서비스 정책이 적용됩니다.\n\n‘연결 해제’로 앱 웹 데이터를 삭제할 수 있습니다. 앱을 종료하면 갱신이 멈추고 비교 기록은 초기화됩니다.\n\nX의 공식 앱이 아닙니다. 자동 추출에 대한 X 이용 허락과 App Store 심사는 아직 확인되지 않았습니다.")
        alert.addButton(withTitle: L10n.text("확인"))
        alert.runModal()
    }

    @objc private func quit() { NSApp.terminate(nil) }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if sender === setupWindow { safari.cancel(); sender.orderOut(nil); setupWindow = nil; return false }
        if sender === window { sender.orderOut(nil); return false }
        (sender.contentView as? WKWebView)?.stopLoading()
        postWindow = nil
        return true
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !UserDefaults.standard.bool(forKey: "setupCompleted") { showSetup() } else { showWindow() }
        return true
    }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        safari.cancel()
        guard controller.disconnecting else { return .terminateNow }
        // Let an in-flight cookie rollback/deletion finish before the process exits.
        Task { @MainActor in
            while self.controller.disconnecting { try? await Task.sleep(for: .milliseconds(50)) }
            sender.reply(toApplicationShouldTerminate: true)
        }
        return .terminateLater
    }
    func applicationWillTerminate(_ notification: Notification) { symbolTimer?.invalidate(); safari.cancel(); controller.stop() }
}
