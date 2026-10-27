# Windows 구현 검증 기록

최신 UI·안정성 개선 결과: [Windows 0.2 검증 기록](VALIDATION-0.2.md). 아래는 0.1.0.6의 검증 이력이다.

2026-10-27 · Windows 개발 후보 0.1.0.6 · 공개 배포 전

## 확인한 결과

| 항목 | 결과 |
| --- | --- |
| Windows 환경 | SIYONG, Windows 11 x64 10.0.26200, .NET SDK 10.0.107 |
| 핵심 로직 | 최신 카드 수정 후 macOS와 Windows 각각 45개 통과 |
| Windows 통합 | 실제 WebView2·WPF에서 로컬 시험 페이지를 사용한 31개 통과 |
| 실제 X 로그인 | 사용자가 앱에서 로그인. 재로그인 없이 오늘 노출수와 게시물 3개 수집 확인 |
| 설치·업데이트 | 서명된 MSIX 0.1.0.0 설치 및 0.1.0.2~0.1.0.6 업데이트 성공. 로그인·표시 방식 유지 확인 |
| Windows 위젯 | 추가 목록에 X-Bar 노출, 실제 추가 성공, 등록 개수 1·오류 없음, 실제 계정·노출수 표시 확인 |
| 표시 방식 | 등록된 상태에서 Windows 선택 시 바탕화면 숨김, Desktop 및 Both 선택 시 표시 확인 |
| 양쪽 연동 | 보드의 최근 2일 버튼으로 공통 설정 변경 및 해당 기간의 게시물 수집 확인 |
| 카드 크기·동작 | 실제 Small 302×148, Medium 302×306, Large 302×464에서 새로고침·설정 버튼이 카드 내부에 표시됨. 최근 7일의 5개 게시물, 설정 열기·게시물 분석 열기·수동 새로고침 확인 |
| 빌드·패키지 | 본앱·공급자 x64 게시 성공. MakeAppx 검증·개발 서명·실제 설치 통과 |
| 공개 배포 | 수행하지 않음. GitHub·Store 게시, 공개 서명·업데이트 피드 변경 없음 |

핵심 시험은 날짜·시간대·계정·기간 변경, RT 판정, 오래된 값 차단, 잘못된 링크, 카드 데이터 삽입, 파일 교체 경쟁, 사용자별 명령 전달을 검사했다. Windows 통합 시험은 격리된 JavaScript 실행, 숨긴 WebView2의 실제 DOM 읽기, ko/en/ja 숫자, 인증 입력 보호, 기간 변경 중 취소, 중복 갱신 병합, 계정 불일치 차단, 로그아웃 중 수집 취소와 프로필 정리, 표시 모드를 검사했다. 이번에 Windows 아이콘 형식의 지표 구분과 서로 다른 지표 값이 섞인 경우의 거부 시험을 추가했다.

자동 시험은 로컬 응답을 사용한다. 별도 실기기 시험에서 사용자가 로그인한 X 페이지의 수집과 보드 추가·표시·설정 전달을 확인했다. 계정의 비밀번호·인증 코드는 수집하거나 기록하지 않았다.

## 실기기에서 수정한 문제

- 패키지 하위 폴더에만 있던 `Microsoft.Windows.Widgets.winmd`를 Windows가 찾지 못했다. 실제 메타데이터 조회에서 패키지 루트는 `0x8000000F`, 공급자 폴더는 성공했다. 패키징 시 이 파일을 루트에도 배치하여 해결했다.
- 첫 위젯을 추가하기 전 `GetWidgetInfos()`가 null을 반환해 공급자 초기화가 실패했다. 빈 목록으로 처리한 뒤 패키지에 등록된 COM 클래스 호출이 `0x00000000`으로 성공했고, 보드에서 실제 위젯을 추가했다.
- Windows의 실제 X 페이지는 `icon-heart`, `icon-repost`를 사용했다. Windows 전용 게시물 추출기에 기존 아이콘과 새 형식을 함께 지원했다. 원래 macOS JavaScript는 변경하지 않았다.
- 보드의 중간 크기 카드에서 긴 게시물과 기본 간격 때문에 하단이 잘렸다. 한 줄 제목, 짧은 상태 행과 크기별 배치로 조정했다.
- 공급자 실패 진단에 예외 종류·오류 코드·메서드 이름을 기록한다. 페이지 내용·쿠키·계정 식별자는 진단에 넣지 않는다.

기존 구현 단계에서는 Chromium 격리 환경 초기화, Windows 파일 교체의 제한된 재시도, 취소된 읽기 컨트롤러 폐기, UI 명령 전달 시점의 수신 확인, 선택 상자 접근성 이름도 수정하고 시험했다.

## 명령과 증거

```sh
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj
```

```powershell
dotnet publish windows/XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/App
dotnet publish windows/XBar.WidgetProvider/XBar.WidgetProvider.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/Provider
dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -r win-x64
dotnet windows/XBar.WindowsTests/bin/Debug/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll windows/artifacts/tests/windows-results-20261027.json
.\windows\scripts\package.ps1 -Version 0.1.0.6
Add-AppxPackage .\windows\artifacts\XBar-0.1.0.6-x64.msix
```

Windows 작업 사본은 `C:\CodexWork\xbar-windows-20261026`이다. 실행 정책은 바꾸지 않았으며, 제한된 환경에서는 읽어 확인한 패키징 명령을 직접 실행했다. UI 검사는 로그인된 사용자 세션의 임시 예약 작업을 사용했다. IMgest 소스·설치본·설정과 이번 포팅에 관계없는 macOS 변경은 보존했다.

로그·실제 화면·패키지는 작업 사본의 `windows\artifacts`와 로컬 `outputs/windows`에 보관하며 Git에 포함하지 않는다. 주요 증거:

- `tests/windows-results-20261027.json`: Windows 통합 31개.
- `metadata-location-check.json`, `provider-and-content-retest.json`: 메타데이터 원인과 공급자 활성화 성공.
- `open-and-pick.json`, `add-native-widget.json`: 목록 노출 및 등록 1개.
- `registered-mode-switches-fixed.json`, `native-period-and-menu.json`: 실제 표시 방식 전환과 보드 기간 명령 전달.
- `native-three-sizes-final.json`, `native-Small.png`, `native-Medium.png`, `native-Large.png`: 실제 보드의 세 크기와 하단 버튼 표시.
- `native-actions-final.json`, `final-actions-and-key-cleanup.json`: 최근 7일 수집, 설정·분석 창·새로고침, 시험 뒤 Both·Today 복구.
- `update-retry-0-1-0-2.json`, `sign-update-0-1-0-6.json`: 같은 식별자 업데이트 및 로그인·설정 보존.

후보 MSIX SHA-256: `B994A65457E7FB94DEB2B06642C75113DC8221746C4AF8A99400342C2F99A629`.

## 시험 환경과 남은 검증

사용자의 명시적 동의로 시험용 인증서를 TrustedPeople에 등록하고 Developer Mode를 시험 동안 활성화했다. 시험 후 이번에 추가한 인증서 3개와 서명 개인 키를 제거했다. 원래 없던 `AllowDevelopmentWithoutDevLicense` 값을 제거하고 원래 레지스트리 키는 유지했다. `AllowAllTrustedApps`는 변경하지 않았다. `security-restored.json`과 `final-actions-and-key-cleanup.json`에서 복구를 확인했다. 복구 후 앱과 공급자를 종료하고 다시 실행해 로그인 유지, 실제 게시물 3개 수집, 보드 등록 1개·오류 없음도 확인했다(`after-security-restore.json`). 배포 스크립트가 신뢰 저장소나 개발자 모드를 자동 변경하지 않는다.

공개용 서명·Store 제출·자동 업데이트 배포, ARM64, 실제 절전 복귀·모니터 분리는 이번 검증 범위에 포함하지 않았다. 개발 후보 설치와 수동 MSIX 업데이트 성공을 공개 배포 또는 자동 업데이트 검증으로 간주하지 않는다.

최종 상태는 Both·Today이며, 바탕화면과 보드 양쪽에 실제 계정과 현재 수집 값이 표시된다(`final-visible-state.json`, `desktop-final.png`, `native-final.png`). 임시 예약 작업을 제거했고 시험용 .NET 프로세스는 0개다(`final-cleanup.json`). 사용자 앱과 로그인 데이터는 유지했다.
