import SwiftUI

struct MenuPopoverView: View {
    static let width: CGFloat = 356
    static let height: CGFloat = 490
    @ObservedObject var controller: BrowserController
    @ObservedObject var content: ContentMonitor
    let openPost: (ContentPost) -> Void
    let openOverview: () -> Void
    let privacy: () -> Void
    let disconnect: () -> Void
    let quit: () -> Void
    var setup: () -> Void = {}
    var follow: () -> Void = {}
    @AppStorage("contentGrowthMode") private var growth = false
    @State private var showingSettings = false
    private let accent = Color(red: 0.05, green: 0.60, blue: 0.53)
    private var rows: [RankedPost] { content.ledger.topFive(growth: growth) }

    var body: some View {
        Group { if showingSettings { settings } else { overview } }
            .frame(width: Self.width, height: Self.height)
            .background(Color(nsColor: .windowBackgroundColor)).tint(accent)

    }
    private var overview: some View {
        VStack(spacing: 0) {
            VStack(alignment: .leading, spacing: 13) {
                HStack(spacing: 10) {
                    Text("𝕏-Bar").font(.system(size: 13, weight: .semibold))
                    Spacer()
                    Text(controller.record?.account ?? L10n.text("계정 연결")).font(.system(size: 10)).foregroundStyle(.secondary)
                    iconButton("gearshape", L10n.text("설정")) { showingSettings = true }
                }
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    if controller.hasFreshValue {
                        Text(controller.display).font(.system(size: 32, weight: .semibold, design: .rounded)).monospacedDigit()
                        Text(L10n.text("오늘 노출")).font(.system(size: 11)).foregroundStyle(.secondary)
                    } else if controller.connected {
                        ProgressView().controlSize(.small)
                        Text(controller.statusText).font(.system(size: 16, weight: .medium)).foregroundStyle(.secondary)
                    } else {
                        Button(L10n.text("X 계정 연결"), action: setup).buttonStyle(.borderedProminent)
                    }
                    Spacer()
                }.frame(height: 39, alignment: .leading)
            }.padding(.horizontal, 18).padding(.top, 14).padding(.bottom, 15)
            Divider().padding(.horizontal, 18)
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text(L10n.text("인기 콘텐츠")).font(.system(size: 11, weight: .semibold))
                    Spacer()
                    Text(growth ? L10n.text("증가 순") : L10n.text("노출 순")).font(.system(size: 10)).foregroundStyle(.tertiary)
                }
                HStack(spacing: 2) {
                    ForEach(ContentPeriod.allCases) { period in
                        Button { content.selectPeriod(period, account: controller.record?.account) } label: {
                            Text(period.title).font(.system(size: 11, weight: .medium))
                                .frame(maxWidth: .infinity).padding(.vertical, 6)
                                .contentShape(Rectangle())
                                .background(content.period == period ? accent : .clear, in: RoundedRectangle(cornerRadius: 6))
                                .foregroundStyle(content.period == period ? .white : Color.primary)
                        }.buttonStyle(.plain).accessibilityValue(content.period == period ? L10n.text("선택됨") : "")
                    }
                }.padding(2).background(Color.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 8))
                    .help(L10n.text("글을 게시한 날짜 기준 · 오늘을 포함한 기간"))
            }.padding(.horizontal, 18).padding(.top, 13).padding(.bottom, 9)
            if rows.isEmpty {
                VStack(spacing: 10) {
                    Image(systemName: content.busy ? "ellipsis" : "text.bubble").font(.system(size: 20)).foregroundStyle(.tertiary)
                    Text(growth && !content.ledger.rows.isEmpty ? L10n.text("다음 확인부터 증가량이 표시됩니다.") : content.message)
                        .font(.system(size: 11)).foregroundStyle(.secondary).multilineTextAlignment(.center)
                    if content.updatedAt != nil, !content.busy, !content.stale, content.ledger.rows.isEmpty {
                        if content.period == .today {
                            Text(L10n.text("새 게시물은 X 분석에 반영되기까지 시간이 걸릴 수 있습니다."))
                                .font(.system(size: 10)).foregroundStyle(.secondary).multilineTextAlignment(.center)
                        }
                        Text(L10n.text("상단의 오늘 노출에는 이전에 쓴 글도 포함됩니다."))
                            .font(.system(size: 10)).foregroundStyle(.secondary).multilineTextAlignment(.center)
                    }
                    if controller.connected, !controller.hasFreshValue, !controller.busy {
                        Button(L10n.text("연결 화면 확인"), action: openOverview).buttonStyle(.plain).foregroundStyle(accent)
                    }
                }.padding(24).frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    VStack(spacing: 2) {
                        ForEach(Array(rows.enumerated()), id: \.element.id) { index, row in
                            PostPreviewRow(rank: index + 1, row: row, growth: growth,
                                surge: content.surges.first { $0.id == row.id }, accent: accent) { openPost(row.post) }
                        }
                    }.padding(.horizontal, 9).padding(.vertical, 3)
                }.opacity(content.stale ? 0.6 : 1).frame(maxHeight: .infinity)
            }
            Divider().padding(.horizontal, 18)
            HStack(spacing: 6) {
                Circle().fill(controller.hasFreshValue && !content.stale ? accent : Color.secondary).frame(width: 4, height: 4)
                Text(controller.hasFreshValue
                     ? (content.busy ? L10n.text("게시물 확인 중") : content.stale ? L10n.text("게시물 확인 필요") : L10n.text("노출 ") + controller.statusText)
                     : controller.statusText)
                    .font(.system(size: 10)).foregroundStyle(.secondary)
                    .help(controller.updatedText + "\n" + content.message)
                Spacer()
                iconButton("arrow.up.right.square", L10n.text("X 분석 열기"), action: openOverview)
                iconButton("arrow.clockwise", L10n.text("새로고침")) {
                    controller.refresh()
                    content.refreshIfNeeded(account: controller.record?.account, force: true)
                }.disabled(!controller.connected || !controller.networkOnline || controller.busy || content.busy)
            }.padding(.horizontal, 18).padding(.vertical, 8)
        }
    }
    private var settings: some View {
        VStack(spacing: 0) {
            HStack(spacing: 8) {
                iconButton("chevron.left", L10n.text("콘텐츠로 돌아가기")) { showingSettings = false }
                Text(L10n.text("설정")).font(.system(size: 14, weight: .semibold)); Spacer()
                Text("𝕏-Bar").font(.system(size: 11)).foregroundStyle(.tertiary)
            }.padding(.horizontal, 12).padding(.vertical, 10)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    VStack(alignment: .leading, spacing: 9) {
                        sectionLabel(L10n.text("콘텐츠"))
                        Picker(L10n.text("정렬"), selection: $growth) {
                            Text(L10n.text("노출수")).tag(false); Text(L10n.text("관찰 후 증가")).tag(true)
                        }.pickerStyle(.segmented).labelsHidden()
                        DisclosureGroup(L10n.text("기간과 증가량 안내")) {
                            Text(L10n.text("기간은 글을 게시한 날짜 기준이며, 지표는 X에 표시된 전체 횟수입니다. 관찰 후 증가는 앱이 글을 처음 확인한 뒤 늘어난 노출 수입니다. 1,000 → 1,200이면 +200입니다. 두 번 이상 확인해야 표시되며 앱 재실행·날짜·계정·기간 변경 시 초기화됩니다.")).font(.system(size: 10)).foregroundStyle(.secondary).padding(.top, 4)
                        }.font(.system(size: 10)).foregroundStyle(.secondary)
                    }
                    VStack(alignment: .leading, spacing: 10) {
                        sectionLabel(L10n.text("갱신과 효과"))
                        Picker(L10n.text("오늘 노출 갱신"), selection: Binding(get: { controller.intervalMinutes }, set: { controller.setInterval($0) })) {
                            ForEach([5, 10, 30], id: \.self) { Text(L10n.format("interval.minutes", String($0))).tag($0) }
                        }
                        Toggle(L10n.text("RT 무지개 효과"), isOn: $content.effectsEnabled).toggleStyle(.switch).controlSize(.small)
                        DisclosureGroup(L10n.text("RT 감지 기준")) {
                            Text(L10n.text("5~15분 동안 재게시가 5회 이상 늘고, 10분 환산 증가량이 5회 이상이며 직전 속도의 3배 이상일 때 감지합니다. 최초 두 번은 기준 수집에 사용합니다. 같은 글은 30분에 한 번 알리며 축약된 RT 수는 제외합니다. 감지 후 최대 30분 동안 𝕏에 무지개가 표시됩니다. 동작 줄이기를 켜면 색을 고정합니다. 𝕏-Bar 자체 기준입니다.")).font(.system(size: 10)).foregroundStyle(.secondary).padding(.top, 4)
                        }.font(.system(size: 10)).foregroundStyle(.secondary)
                        Text(L10n.text("콘텐츠는 10분마다 확인합니다.")).font(.system(size: 10)).foregroundStyle(.tertiary)
                    }
                    VStack(alignment: .leading, spacing: 10) {
                        sectionLabel(L10n.text("계정"))
                        HStack {
                            Text(controller.record?.account ?? L10n.text("연결된 계정 없음")).foregroundStyle(.secondary)
                            Spacer()
                            Button(L10n.text("연결 관리"), action: setup).buttonStyle(.plain).foregroundStyle(accent)
                        }
                        if controller.connected { Button(L10n.text("연결 해제…"), action: disconnect).buttonStyle(.plain).foregroundStyle(.secondary) }
                    }
                    Divider()
                    VStack(alignment: .leading, spacing: 12) {
                        Button(L10n.text("제작자 @yongZa_R 팔로우 ↗"), action: follow).foregroundStyle(accent)
                        HStack {
                            Button(L10n.text("앱 정보 · 개인정보"), action: privacy)
                            Spacer()
                            Button(L10n.text("종료"), action: quit)
                        }.foregroundStyle(.secondary)
                    }.buttonStyle(.plain)
                }.font(.system(size: 11)).padding(18).frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }
    private func sectionLabel(_ title: String) -> some View {
        Text(title).font(.system(size: 10, weight: .semibold)).foregroundStyle(.secondary)
    }
    private func iconButton(_ image: String, _ label: String, action: @escaping () -> Void) -> some View {
        Button(action: action) { Image(systemName: image).font(.system(size: 12)).frame(width: 24, height: 24).contentShape(Rectangle()) }
            .buttonStyle(.plain).foregroundStyle(.secondary).help(label).accessibilityLabel(label)
    }
}

private struct PostPreviewRow: View {
    let rank: Int
    let row: RankedPost
    let growth: Bool
    let surge: RepostSurge?
    let accent: Color
    let action: () -> Void
    @State private var hovering = false
    var body: some View {
        Button(action: action) {
            HStack(alignment: .top, spacing: 9) {
                Text("\(rank)").font(.system(size: 10, weight: .medium)).foregroundStyle(.tertiary).frame(width: 12).padding(.top, 2)
                VStack(alignment: .leading, spacing: 5) {
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Text(row.post.displayTitle).font(.system(size: 12, weight: .medium))
                            .lineLimit(1).truncationMode(.tail)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Text(growth ? row.increaseText : row.post.impressions)
                            .font(.system(size: 13, weight: .semibold)).monospacedDigit()
                            .fixedSize(horizontal: true, vertical: false)
                            .foregroundStyle(growth ? accent : Color.primary)
                            .help(growth ? L10n.text("관찰 후 증가") : L10n.text("노출 ") + row.post.impressions)
                    }
                    HStack(spacing: 14) {
                        metric(row.post.likes, icon: "heart", label: L10n.text("좋아요"))
                        metric(row.post.reposts, icon: "arrow.2.squarepath", label: L10n.text("재게시"))
                        if growth { metric(row.post.impressions, icon: "chart.bar", label: L10n.text("노출")) }
                        Spacer(minLength: 0)
                        if surge != nil {
                            Label("RT", systemImage: "arrow.up.right").font(.system(size: 9, weight: .semibold)).foregroundStyle(accent)
                        }
                    }
                }
            }.padding(.horizontal, 9).padding(.vertical, 7).contentShape(Rectangle())
                .background(hovering ? Color.primary.opacity(0.05) : (surge != nil ? accent.opacity(0.06) : .clear), in: RoundedRectangle(cornerRadius: 9))
        }.buttonStyle(.plain).onHover { hovering = $0 }
            .help(row.post.displayTitle + (surge.map { "\n" + $0.label } ?? ""))
            .accessibilityLabel(L10n.format("post.accessibility", String(rank), row.post.displayTitle, row.post.impressions, row.post.likes, row.post.reposts) + (growth ? L10n.format("post.growth", row.increaseText) : ""))
    }
    private func metric(_ value: String, icon: String, label: String) -> some View {
        Label(value, systemImage: icon).font(.system(size: 10)).monospacedDigit()
            .foregroundStyle(.secondary).help(label + " " + value)
    }
}
