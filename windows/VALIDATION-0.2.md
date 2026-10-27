# Windows 0.2 UI·안정성 검증

2026-10-27 · SIYONG Windows 11 x64 10.0.26200 · 개발 후보 0.2.0.1

## 변경

- 큰 오늘 노출 수치, 기간 탭, 게시물 ID별 행, 목록 전용 스크롤, 그룹별 설정, 시스템 테마 색상과 키보드 포커스. 게시물 접기와 원래 펼침 크기 저장.
- 두 위젯의 같은 범위 갱신에서는 검증된 값을 유효 시간 내에 유지. 다른 계정·기간·날짜·시간대의 값은 숨긴다. 마지막 확인 시각과 갱신 상태를 구분한다.
- 읽기 8초·탐색 45초·전체 수집 100초 제한, 실패 후 30~300초 재시도. 연속 기간 변경은 마지막 요청으로 이어진다.
- 렌더러 실패 시 읽기 창 재생성. 초기화 도중 종료하면 소유 창 목록을 먼저 분리하여 재진입으로 인한 예외를 방지한다.
- 실제 설치본에서 최초 WPF 화면이 비거나 일부만 그려지는 현상을 발견했다. 작은 WPF 창을 소프트웨어 렌더링으로 바꾸었다. X의 WebView2 렌더러는 별도로 유지한다.
- 고대비 선택 색상 쌍, 축약 상태의 로그인 버튼 공간, 자동 시작 상태 표시·사용자 선택을 보완했다.

수정 범위는 `windows/`와 이 작업의 문서다. macOS 앱 소스·데이터 규칙, 기존 X 로그인 프로필, 관계없는 미완료 변경을 보존했다.

## 시험

| 시험 | 결과 |
| --- | --- |
| 공통 로직 | macOS·Windows 각각 53개 통과 |
| Windows 통합 | 실제 WPF·WebView2 로컬 응답 시험 59개 통과 |
| 축약 화면 추가 검사 | 밝음·어두움에서 미연결 버튼 영역과 연결 후 높이 복구 4개 통과 |
| 확대 배치 | LayoutTransform으로 100·150·200%에서 컨트롤이 창 안에 배치됨 |
| 언어·테마 | ko/en/ja × light/dark 실제 WPF 렌더링. 고대비 선택 배경·문자 색상은 시스템 색상 쌍 검사 |
| 입력 유지 | 같은 범위의 갱신 전·중·후 행 객체, 포커스, 스크롤 유지 |
| 지연·취소 | 미완료 JavaScript Promise 시간 제한·취소, 전체 제한 시간 이후 자동 재시도 |
| 복구 | 오프라인·재연결, 절전 핸들러, 실제 WebView2 렌더러 종료 후 재생성 |
| 데이터 보호 | 계정 불일치 제거, 연속 기간 전환의 마지막 범위 확인, 로그아웃 중 취소, 초기화 중 종료 후 늦은 결과 거부 |
| 실제 0.2.0.0 업데이트 | 0.1.0.6에서 로그인·Both 유지, 실제 오늘 게시물 3개 수집, 앱 프로세스 1개 |
| 실제 조작 | 새 설정의 Desktop/Windows/Both 선택, F5 갱신 시작·완료 |
| Windows 기본 위젯 | 실제 302×148/306/464 크기에서 하단 버튼 포함, 게시물 0/3/5개. 7일 버튼으로 실제 5개 수집 |

통합 시험의 수집 데이터는 사용자 프로필과 분리한 로컬 응답이다. 실제 X 수집은 설치된 앱에서 별도로 확인했다. 로그인 비밀번호·인증 코드·쿠키 값은 읽거나 기록하지 않았다.

## 재현 명령

```sh
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj -c Release
```

```powershell
dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64
dotnet windows/XBar.WindowsTests/bin/Release/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll windows/artifacts/tests/windows-fluent-results.json
dotnet windows/XBar.WindowsTests/bin/Release/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll windows/artifacts/tests/windows-layout-results.json --layout-only
dotnet publish windows/XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/App
dotnet publish windows/XBar.WidgetProvider/XBar.WidgetProvider.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/Provider
# 읽어 확인한 package.ps1의 내용을 제한된 실행 정책 안에서 실행함
# package.ps1 -Version 0.2.0.1
```

실제 UI 시험은 로그인된 사용자의 임시 예약 작업으로 실행했다. SSH Session 0에서는 WPF·WebView2 UI 시험을 수행하지 않았다. 빌드·패키지·캡처 증거는 `outputs/windows` 및 Windows 작업 사본의 `windows/artifacts`에 보관한다.

## 최종 후보와 화면

최종 패키지: `XBar-0.2.0.1-x64.msix`. SHA-256: `85AC3F605C87E4C16AA332918DA44FE7C6C86C72BF8CEB7CE50A734BFAC52538`. 서명 검증과 실제 업데이트를 통과했고 로컬 복사본의 SHA-256도 일치한다. 0.2.0.0에서 확인된 최초 표시 문제를 보완한 버전이다.

- `fluent-install-final.json`: 0.2.0.1 설치·서명, 추가 화면 시험 4개.
- `fluent-desktop-first-frame.png`, `fluent-settings-final.png`: 설치 직후 크기 변경 없이 전체 UI가 표시됨.
- `fluent-final-actions.json`: Today·Both 복구, 실제 게시물 3개, Ctrl+, 및 보드 설정 열기, 기존 9개 설정 항목·창 위치/크기 일치, 서명 개인 키 제거.
- `fluent-native-sizes.json`, `fluent-native-Small/Medium/Large.png`: 동일 카드 코드의 실제 크기별 결과.
- `tests/windows-fluent-results.json`, `tests/windows-layout-results.json`: 59개 및 4개 시험의 개별 결과.

## 시험 후 복구

기존 승인 범위에서 만든 시험용 leaf certificate(말단 인증서) 1개를 TrustedPeople에 일시 등록했다. 최종 시험 후 이 항목과 사용자 서명 개인 키를 제거했다. Developer Mode와 AllowAllTrustedApps는 이번 작업에서 변경하지 않았으며 원래 없던 값이 계속 없는 것을 확인했다. 임시 예약 작업도 제거했다.

보안 설정 정리 후 0.2.0.1 앱과 공급자를 종료·재실행했다. 재로그인 없이 Today·Both·Ready, 실제 게시물 3개, 보드 등록 1개·오류 없음, 앱 프로세스 1개, 시험용 .NET 프로세스 0개를 확인했다. 창 크기 조작 없이 첫 화면을 다시 캡처했다.

증거: `fluent-security-restored.json`, `fluent-after-cleanup.json`, `fluent-desktop-restarted.png`, `fluent-cleanup-complete.json`. 화면은 `fluent-desktop-final.png`, `fluent-settings-final.png`, `fluent-native-final.png`에 보관한다. 이 패키지는 공개 배포용 인증서·업데이트 피드를 갖춘 릴리스가 아닌 개발 시험본이다.

## 판단 범위

macOS에 있던 주요 수집·표시 기능과 복구 규칙을 비교하여 구현하고, Windows 특유의 종료·초기 표시 문제를 추가로 수정했다. 이 결과로 모든 환경에서 macOS보다 안정적이라고 단정하지 않는다.

Windows 공개 자동 업데이트 피드·Store 배포·공개 서명은 아직 준비하지 않았다. 현재 업데이트는 개발 MSIX 시험이다. ARM64, 실제 OS 로그인 자동 시작, 실제 절전·복귀, 물리 모니터 분리, OS 전체 DPI·고대비 변경, 장기 연속 실행은 미검증이다. 실제 PC는 96 DPI이며 확대·복구 상황은 위에 명시한 방식으로 모사했다.
