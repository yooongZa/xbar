import SwiftUI

struct SetupView: View {
    @ObservedObject var controller: BrowserController
    @ObservedObject var safari: SafariConnection
    let directLogin: () -> Void
    let finish: () -> Void
    let follow: () -> Void
    @State private var page = 0
    @State private var safariSelected = false
    private let accent = Color(red: 0.03, green: 0.57, blue: 0.50)
    private var verified: Bool { page == 2 && controller.hasVerifiedAccount && (!safariSelected || safari.received) }

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack {
                Text("𝕏-Bar").font(.system(size: 16, weight: .semibold))
                Spacer()
                Text(verified ? L10n.text("연결 완료") : "\(page + 1) / 3").foregroundStyle(.secondary).font(.system(size: 12))
            }
            if page == 0 {
                Spacer(minLength: 0)
                Text("𝕏 24.8K").font(.system(size: 44, weight: .semibold, design: .rounded)).monospacedDigit()
                    .padding(.horizontal, 22).padding(.vertical, 12).background(accent.opacity(0.09), in: RoundedRectangle(cornerRadius: 18))
                Text(L10n.text("오늘의 반응을,\n메뉴바에서.")).font(.system(size: 30, weight: .bold))
                Text(L10n.text("노출 · 좋아요 · 재게시를 한눈에.\n글을 누르면 상세 분석으로 이어집니다."))
                    .foregroundStyle(.secondary).lineSpacing(5)
                Spacer(minLength: 0)
                Button(L10n.text("시작하기")) { page = 1 }.buttonStyle(.borderedProminent).controlSize(.large)
            } else if page == 1 {
                Text(L10n.text("X 계정을 연결하세요")).font(.system(size: 27, weight: .bold))
                Text(L10n.text("Safari에서 로그인하면 저장된 암호와 익숙한 인증 수단을 사용할 수 있어 편합니다."))
                    .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                Button { safariSelected = true; page = 2; safari.begin() } label: {
                    choice("safari", L10n.text("Safari로 연결"), L10n.text("추천 · 기존 X 로그인 사용"))
                }.buttonStyle(.plain).disabled(controller.disconnecting)
                Button { safariSelected = false; page = 2; directLogin() } label: {
                    choice("macwindow", L10n.text("앱에서 직접 로그인"), L10n.text("𝕏-Bar 안에서 X 아이디로 로그인"))
                }.buttonStyle(.plain).disabled(controller.disconnecting)
                Text(L10n.text("사용자의 확인을 통해 𝕏 로그인 세션을 𝕏-Bar와 연동합니다."))
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                Text(L10n.text("X 분석 화면에 접근할 수 있는 계정이 필요합니다."))
                    .font(.system(size: 11)).foregroundStyle(.tertiary)
                Spacer(minLength: 0)
                Button(L10n.text("이전")) { page = 0 }.buttonStyle(.plain)
            } else if verified, let record = controller.record, let account = record.account {
                Spacer(minLength: 0)
                Image(systemName: "checkmark.circle.fill").font(.system(size: 38)).foregroundStyle(accent)
                Text(L10n.text("연결이 완료됐습니다")).font(.system(size: 27, weight: .bold))
                VStack(alignment: .leading, spacing: 8) {
                    Text(account).font(.system(size: 15, weight: .medium))
                    HStack(alignment: .firstTextBaseline) {
                        Text(record.display).font(.system(size: 36, weight: .semibold)).monospacedDigit()
                        Text(L10n.text("오늘 노출")).foregroundStyle(.secondary)
                    }
                }.padding(20).frame(maxWidth: .infinity, alignment: .leading).background(accent.opacity(0.08), in: RoundedRectangle(cornerRadius: 16))
                Text(L10n.text("계정명과 실제 오늘 노출 수를 확인했습니다.\n잠시 후 안내를 닫고 메뉴바를 엽니다."))
                    .foregroundStyle(.secondary).lineSpacing(4)
                Spacer(minLength: 0)
                Button(L10n.text("𝕏-Bar 시작"), action: finish).buttonStyle(.borderedProminent).controlSize(.large)
            } else if safariSelected {
                Text(safari.received ? L10n.text("오늘 노출을 확인하는 중") : L10n.text("Safari에서 연결하기")).font(.system(size: 27, weight: .bold))
                if safari.received {
                    ProgressView().controlSize(.small)
                    Text(controller.message).foregroundStyle(.secondary)
                    Text(L10n.text("계정명과 오늘 노출 수까지 확인해야 연결이 완료됩니다.")).font(.system(size: 12)).foregroundStyle(.secondary)
                    Button(L10n.text("X 분석 화면 확인"), action: directAnalysis)
                    Spacer()
                    Button(L10n.text("다시 연결")) { safari.begin() }
                } else {
                    step("1", L10n.text("확장 켜기"), L10n.text("Safari 설정에서 𝕏-Bar를 켜고 x.com 접근을 허용하세요."))
                    Button(safari.extensionEnabled ? L10n.text("Safari 설정 · 확장 켜짐") : L10n.text("Safari 설정 열기")) { safari.openPreferences() }
                    step("2", L10n.text("Safari에서 X 로그인"), L10n.text("이미 로그인했다면 그대로 사용할 수 있습니다."))
                    Button(L10n.text("Safari에서 X 열기")) { safari.openX() }
                    step("3", L10n.text("𝕏-Bar에 연결"), L10n.text("Safari 도구 막대의 𝕏-Bar에서 확인 후 연결을 누르세요."))
                    Text(safari.message).font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    if safari.waiting { ProgressView().controlSize(.small) }
                    Spacer(minLength: 0)
                    Button(L10n.text("다른 방법 선택")) { safari.cancel(); page = 1 }.buttonStyle(.plain)
                }
            } else {
                Text(L10n.text("앱에서 로그인하기")).font(.system(size: 27, weight: .bold))
                Text(L10n.text("열린 X 화면에서 이메일 또는 사용자 아이디로 로그인하세요. 이 경로에서는 Google 로그인을 지원하지 않습니다.")).foregroundStyle(.secondary)
                Button(L10n.text("X 로그인 화면 열기"), action: directLogin).buttonStyle(.borderedProminent)
                Text(controller.message).font(.system(size: 12)).foregroundStyle(.secondary)
                Spacer(minLength: 0)
                Button(L10n.text("다른 방법 선택")) { page = 1 }
            }
        }.padding(32).frame(width: 510, height: 560).tint(accent)
            .background(Color(nsColor: .windowBackgroundColor))
            .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in safari.checkExtension() }
            .task(id: verified) {
                guard verified else { return }
                do { try await Task.sleep(for: .seconds(3)) } catch { return }
                if verified, !Task.isCancelled { finish() }
            }
    }
    // Preserve the imported session: this opens the app's analysis, not a new login.
    var directAnalysis: () -> Void = {}
    private func choice(_ icon: String, _ title: String, _ detail: String) -> some View {
        HStack(spacing: 15) {
            Image(systemName: icon).font(.system(size: 27)).foregroundStyle(accent).frame(width: 38)
            VStack(alignment: .leading, spacing: 5) { Text(title).fontWeight(.semibold); Text(detail).font(.system(size: 12)).foregroundStyle(.secondary) }
            Spacer(); Image(systemName: "chevron.right").foregroundStyle(.secondary)
        }.padding(18).background(accent.opacity(0.07), in: RoundedRectangle(cornerRadius: 13))
    }
    private func step(_ number: String, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Text(number).font(.system(size: 12, weight: .bold)).frame(width: 24, height: 24).background(accent.opacity(0.12), in: Circle())
            VStack(alignment: .leading, spacing: 3) { Text(title).font(.system(size: 14, weight: .semibold)); Text(detail).font(.system(size: 11)).foregroundStyle(.secondary) }
        }
    }
}
