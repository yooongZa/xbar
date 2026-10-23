import SwiftUI
import WebKit

struct BrowserView: NSViewRepresentable {
    let webView: WKWebView
    func makeNSView(context: Context) -> WKWebView { webView }
    func updateNSView(_ nsView: WKWebView, context: Context) {}
}

struct DashboardView: View {
    @ObservedObject var controller: BrowserController
    var hide: () -> Void
    private let accent = Color(red: 0.24, green: 0.42, blue: 0.94)

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: 24) {
                VStack(alignment: .leading, spacing: 8) {
                    HStack(spacing: 7) {
                        Image(systemName: "chart.bar.xaxis").foregroundStyle(accent)
                        Text("𝕏-Bar").font(.system(size: 12, weight: .bold, design: .rounded)).tracking(1.6)
                    }
                    HStack(alignment: .firstTextBaseline, spacing: 10) {
                        Text(controller.display).font(.system(size: 38, weight: .semibold, design: .rounded)).monospacedDigit()
                        Text(L10n.text("오늘 노출")).font(.system(size: 13)).foregroundStyle(.secondary)
                    }
                    Text(controller.record?.account ?? L10n.text("내 X 계정")).font(.system(size: 12)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 8)
                VStack(alignment: .trailing, spacing: 10) {
                    HStack(spacing: 8) {
                        if controller.busy { ProgressView().controlSize(.small) }
                        Text(controller.updatedText).font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                    HStack {
                        if controller.connected {
                            if controller.requiresLogin {
                                Button(L10n.text("X 로그인")) { controller.openXLogin() }.disabled(controller.disconnecting)
                            }
                            Button(L10n.text("새로고침"), systemImage: "arrow.clockwise") { controller.refresh() }
                                .disabled(controller.busy || controller.disconnecting)
                            Button(L10n.text("창 숨기기"), systemImage: "menubar.rectangle") { hide() }
                        } else {
                            Button(L10n.text("X에 연결"), systemImage: "arrow.up.right") { controller.connect() }
                                .buttonStyle(.borderedProminent).tint(accent).disabled(controller.disconnecting)
                        }
                    }
                }
            }
            .padding(.horizontal, 26).padding(.vertical, 20)
            HStack(spacing: 8) {
                Image(systemName: controller.stale ? "clock" : "info.circle").foregroundStyle(accent)
                Text(controller.message).font(.system(size: 12))
                Spacer()
            }
            .padding(.horizontal, 26).padding(.vertical, 12).background(accent.opacity(0.07))
            Divider()
            if controller.connected {
                BrowserView(webView: controller.webView).id(ObjectIdentifier(controller.webView))
            } else {
                VStack(spacing: 20) {
                    Image(systemName: "menubar.rectangle").font(.system(size: 55, weight: .ultraLight)).foregroundStyle(accent)
                    Text(L10n.text("오늘의 반응을, 메뉴바에서.")).font(.system(size: 24, weight: .semibold))
                    Text(L10n.text("1. X 이메일 또는 사용자 아이디로 로그인\n2. 분석 화면의 오늘 노출 수 확인\n3. 창을 숨기고 메뉴바에서 확인"))
                        .font(.system(size: 14)).lineSpacing(9).foregroundStyle(.secondary)
                    Button(L10n.text("X에 연결")) { controller.connect() }.buttonStyle(.borderedProminent).tint(accent)
                        .controlSize(.large).disabled(controller.disconnecting)
                    Text(L10n.text("웹 로그인 정보는 이 Mac의 앱 저장소에 보관됩니다.\n노출 수는 메모리에서만 처리하며 개발자 서버로 전송하지 않습니다."))
                        .font(.system(size: 11)).lineSpacing(4).multilineTextAlignment(.center).foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            Divider()
            HStack {
                Circle().fill(controller.connected ? accent : Color.secondary).frame(width: 5, height: 5)
                Text(controller.connected ? L10n.text("창을 숨겨도 앱이 실행 중이면 주기적으로 갱신합니다.") : L10n.text("X 분석 화면 접근 권한이 필요합니다."))
                Spacer()
                Text(L10n.format("interval.today", String(controller.intervalMinutes)))
            }.font(.system(size: 10)).foregroundStyle(.secondary).padding(.horizontal, 20).padding(.vertical, 10)
        }
        .frame(minWidth: 820, minHeight: 650)
    }
}
