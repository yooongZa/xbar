import AppKit
import Combine
import CryptoKit
import SafariServices

@MainActor
final class SafariConnection: ObservableObject {
    static let extensionID = "com.limod.xbar.SafariExtension"
    @Published private(set) var message = L10n.text("Safari에서 준비를 마친 뒤 확장의 ‘𝕏-Bar에 연결’을 눌러 주세요.")
    @Published private(set) var waiting = false
    @Published private(set) var received = false
    @Published private(set) var extensionEnabled = false
    var onReceive: (([SessionCookie]) async -> Bool)?
    private var instance: UUID?
    private var revision = 0
    private var pending: TransferRequest?
    private var privateKey: Curve25519.KeyAgreement.PrivateKey?
    private var polling: Task<Void, Never>?

    // Preparation has no countdown and creates no transfer request or private key.
    func begin() {
        cancel(); received = false
        let session = UUID(); instance = session
        let token = revision
        do {
            try TransferFiles.write(PairingAvailability(instance: session), at: TransferFiles.availabilityURL())
            message = L10n.text("준비 시간에는 제한이 없습니다. Safari 확장에서 연결을 눌러 주세요.")
            checkExtension()
            polling = Task { [weak self] in
                while !Task.isCancelled {
                    do { try await Task.sleep(for: .seconds(1)) } catch { return }
                    guard let self, self.revision == token, self.instance == session else { return }
                    do {
                        let intentURL = try TransferFiles.intentURL()
                        if FileManager.default.fileExists(atPath: intentURL.path) {
                            let intent = try TransferFiles.read(PairingIntent.self, at: intentURL)
                            try intent.validate(instance: session)
                            TransferFiles.clearHandshake()
                            let key = Curve25519.KeyAgreement.PrivateKey()
                            let request = TransferRequest(id: intent.id, createdAt: Date(), publicKey: key.publicKey.rawRepresentation)
                            self.privateKey = key; self.pending = request; self.waiting = true
                            try TransferFiles.write(request, at: TransferFiles.requestURL())
                            self.message = L10n.text("X 로그인 세션을 안전하게 연결하고 있습니다…")
                        }
                        guard let current = self.pending else { continue }
                        if (try? current.validate()) == nil {
                            self.clearRequest()
                            self.message = L10n.text("연결 요청이 만료됐습니다. Safari 로그인은 유지됩니다. 확장에서 ‘다시 연결’을 눌러 주세요.")
                            continue
                        }
                        let url = try TransferFiles.responseURL(current.id)
                        guard FileManager.default.fileExists(atPath: url.path) else { continue }
                        guard let key = self.privateKey else { throw TransferError.invalid }
                        let envelope = try TransferFiles.read(TransferEnvelope.self, at: url)
                        let cookies = try SessionTransfer.open(envelope, request: current, privateKey: key)
                        self.clearRequest()
                        self.message = L10n.text("계정명과 오늘 노출 수를 확인하고 있습니다…")
                        let imported = await self.onReceive?(cookies) ?? false
                        guard self.revision == token, !Task.isCancelled else { return }
                        self.received = imported
                        if imported {
                            self.instance = nil; TransferFiles.cleanup(); return
                        }
                        self.message = L10n.text("연결을 완료하지 못했습니다. Safari 확장에서 ‘다시 연결’을 눌러 주세요.")
                    } catch {
                        self.clearRequest()
                        self.message = L10n.text("연결 요청을 확인할 수 없습니다. 로그인은 유지됩니다. 확장에서 ‘다시 연결’을 눌러 주세요.")
                    }
                }
            }
        } catch { message = L10n.text("연결 준비에 실패했습니다. 앱을 다시 실행하고 Safari 연결을 선택해 주세요.") }
    }
    private func clearRequest() {
        pending = nil; privateKey = nil; waiting = false; TransferFiles.clearHandshake()
    }
    func cancel() {
        revision += 1; polling?.cancel(); polling = nil; instance = nil
        pending = nil; privateKey = nil; waiting = false; TransferFiles.cleanup()
    }
    func checkExtension() {
        SafariStatusProbe.check(Self.extensionID) { [weak self] enabled in
            Task { @MainActor [weak self] in self?.extensionEnabled = enabled }
        }
    }
    func openPreferences() {
        SafariStatusProbe.preferences(Self.extensionID) { [weak self] success in
            Task { @MainActor [weak self] in
                if !success { self?.message = L10n.text("Safari 설정 → 확장에서 𝕏-Bar를 켜 주세요.") }
                self?.checkExtension()
            }
        }
    }
    func openX() {
        guard let safari = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.Safari") else {
            message = L10n.text("이 Mac에서 Safari를 찾을 수 없습니다."); return
        }
        NSWorkspace.shared.open([BrowserController.analyticsURL], withApplicationAt: safari,
            configuration: NSWorkspace.OpenConfiguration(), completionHandler: nil)
    }
}
