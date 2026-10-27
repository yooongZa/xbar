# X-Bar Windows

Windows 11용 바탕화면 위젯과 Windows Widgets Board 위젯을 제공한다. 설정에서 바탕화면 / Windows 위젯 / 둘 다를 선택하며, 두 화면은 하나의 X 로그인과 수집 결과를 공유한다.

## 사용

1. X-Bar를 실행하고 **X에 연결**을 선택한다.
2. 열린 앱 내 X 화면에서 로그인한 뒤 **연결 확인**을 누른다. 실제 계정과 오늘 노출을 확인하면 로그인 창이 닫힌다.
3. 위젯의 `⋯ → 설정 → 위젯`에서 표시 방식을 선택한 뒤 **저장**한다.
4. Windows 위젯은 `Win+W → 위젯 추가 → X-Bar`로 추가한다. 추가 전에는 바탕화면 위젯을 유지한다.

GitHub 배포판의 **Windows Widgets Board 위젯은 Developer Mode(개발자 모드)가 필요하다.** [Microsoft 배포 안내](https://learn.microsoft.com/en-us/samples/microsoft/windowsappsdk-samples/widgets/)의 sideloading(직접 설치) 조건이다. Windows 설정에서 사용자가 직접 선택하며 앱이 이 설정을 변경하지 않는다. 바탕화면 위젯 자체에는 개발자 모드가 필요하지 않다. 이전 시험에서는 임시 활성화로 보드에 추가한 뒤 원래 설정으로 복구했다. 새 PC에서 공개 서명 패키지의 설치·보드 등록 경로는 아직 검증되지 않았다.

바탕화면 위젯의 제목을 끌어 이동하고 가장자리를 끌어 크기를 바꾼다. ‘항상 위에’, 첫 확인 이후 증가순, RT 급증 효과, 5·10·30분 갱신, 한국어·영어·일본어를 설정할 수 있다. 게시물 기간과 수동 갱신은 두 화면에 함께 반영된다.

설정은 **위젯 / 계정·데이터 / 앱 업데이트 / 시스템** 탭으로 나뉜다. 표시 방식·수집 주기·자동 업데이트 확인·자동 시작 등 선택한 값은 **저장**할 때 적용되며, 닫으면 취소된다. 계정 연결·분석 열기·업데이트 확인 버튼은 해당 작업을 바로 실행한다.

설정·분석 창을 닫아도 수집은 계속된다. `앱 종료`는 수집을 중단하며, 보드에는 마지막 확인 시각과 앱 실행 안내를 표시한다. 숨긴 위젯과 설정은 시작 메뉴의 X-Bar를 실행하면 다시 접근할 수 있다. 자동 시작은 `설정 → 시스템`에서 선택한 뒤 저장한다. Windows 정책으로 잠긴 경우 시작 앱 설정으로 이동할 수 있다. 기존 PC의 실제 재로그인 후 자동 실행과 X 로그인 유지는 [실기기 기록](VALIDATION-STARTUP.md)에서 확인할 수 있다.

0.3.1은 설정 저장 동작을 통일하고 연결 상태·업데이트 안내를 정리했다. Windows 보드에는 큰 요약 수치, 같은 폭의 기간 탭, 순위와 오른쪽 정렬 수치를 적용한다. 큰 크기는 게시물 5개와 좋아요·재게시 수를 표시한다. 실기기 설치·캡처와 검증 한계는 [0.3.1 검증 기록](VALIDATION-0.3.1.md)에 기록한다.

## 0.2 화면과 복구 동작

- Windows 테마·고대비 색상을 따르는 Fluent 화면. 작은 WPF 창은 첫 화면 표시의 호환성을 위해 소프트웨어 렌더링을 사용한다. 큰 요약 수치, 기간 탭, ID별 게시물 행, 목록 전용 스크롤, 설정 카드.
- `F5`로 새로고침, `Ctrl+,`로 설정, `Tab`과 방향키로 버튼·기간 선택. 게시물 접기 상태와 펼쳤을 때의 크기를 기억한다.
- 같은 계정·기간·날짜·시간대의 검증된 값만 제한 시간 내에 유지한다. 갱신 상태와 확인 시각을 구분하고, 범위가 바뀌면 이전 목록을 숨긴다.
- 웹 탐색 45초, 각 읽기 8초, 전체 수집 100초 제한. 실패 시 30~300초 간격으로 재시도하고, 연결·절전 복귀 시 다시 확인한다.
- 취소된 범위의 결과를 거부하고 마지막 기간 요청을 이어서 수행한다. 실패한 렌더러를 재생성하며, 초기화 도중 종료해도 정리 중 목록을 다시 순회하지 않는다.

화면과 복구 개선의 시험 범위·배포 한계는 [UX-STABILITY.md](UX-STABILITY.md)와 [VALIDATION-0.2.md](VALIDATION-0.2.md)에 기록한다.

## 구조

| 경로 | 역할 |
| --- | --- |
| `XBar.Core` | 지표·날짜·순위·급증 판정, 공통 데이터, 원자적 저장, 사용자별 IPC, 다국어, 카드 생성 |
| `XBar.App` | WPF 바탕화면 위젯·설정, WebView2 로그인과 단일 수집 주체 |
| `XBar.WidgetProvider` | Windows App SDK 위젯 공급자와 COM 등록 |
| `XBar.Core.Tests` | macOS와 같은 지표 규칙, 시간·계정 변경, 저장 경쟁, 카드·입력 검증 |
| `XBar.WindowsTests` | 실제 WebView2에서 로컬 시험 페이지를 읽고 격리·취소·로그아웃·화면을 검증 |
| `package` | 개발용 MSIX 매니페스트 |
| `scripts` | Windows 빌드·검증·패키징 |

계정·오늘 지표는 `app/Resources`의 JavaScript를 빌드에 포함한다. 게시물은 같은 검증 규칙을 유지한 `XBar.App/Scripts/ContentExtractor.js`를 사용하며, Windows의 X 화면에서 확인한 좋아요·재게시 아이콘 이름도 인식한다. macOS 소스는 이 포팅 작업에서 변경하지 않았다. 쿠키는 앱 전용 WebView2 저장소에만 두고 위젯 공급자에는 전달하지 않는다. 공개 API나 별도 서버를 사용하지 않는다.

수집·설정·데이터 저장은 본앱에서 수행한다. 위젯 공급자는 공통 데이터를 읽으며, 사용자 동작은 현재 사용자만 접근할 수 있는 Named pipe(명명된 파이프)로 본앱에 전달한다. 앱이 종료된 경우 명시적인 카드 동작으로 앱을 연다.

## 빌드와 시험

필요 환경은 Windows 11 x64, .NET SDK 10.0.107 호환 패치, Microsoft Edge WebView2 Runtime, MSIX 패키징용 Windows SDK다. 정확한 패키지 버전은 각 프로젝트 파일에 고정되어 있다.

저장소 루트에서 핵심 시험은 macOS에서도 실행할 수 있다.

```sh
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj
```

Windows의 로그인된 사용자 세션에서 실행한다.

```powershell
.\windows\scripts\build.ps1
.\windows\scripts\package.ps1
```

시스템의 스크립트 실행 정책 때문에 위 명령을 실행할 수 없다면 개발 환경 관리자에게 정책을 확인한다. 테스트·빌드 자체는 다음 .NET 명령으로도 실행할 수 있다.

```powershell
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj -c Release
dotnet publish windows/XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/App
dotnet publish windows/XBar.WidgetProvider/XBar.WidgetProvider.csproj -c Release -r win-x64 --self-contained true -o windows/artifacts/layout/Provider
dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64
dotnet windows/XBar.WindowsTests/bin/Release/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll windows/artifacts/tests/windows-results.json
```

WebView2 시험은 모든 요청에 시험 응답을 공급하며 실제 X 계정이나 쿠키를 사용하지 않는다. SSH의 Session 0에서는 WebView2 초기화가 실패할 수 있으므로 로그인된 사용자 세션에서 실행해야 한다.

`package.ps1`은 Windows 시험에서 캡처한 실제 위젯 화면을 추가 선택 화면의 이미지로 포함하고, 매니페스트를 검증한 **서명 전 MSIX**를 생성한다. 패키지 기본값은 `LIMOD.XBar.Dev`, `CN=XBar Development`, `0.3.1.0`이다. 공개 배포용 identity(식별자)·publisher(게시자)와 서명은 배포 경로 결정 후 별도로 준비한다. 스크립트가 인증서 신뢰나 Windows 개발자 모드를 자동 변경하지 않는다.

## 데이터와 업데이트

앱 데이터는 현재 사용자 `%LOCALAPPDATA%\XBar`를 기준으로 저장한다. MSIX로 설치했을 때는 Windows의 패키지별 파일 리디렉션이 적용될 수 있다. 설정과 마지막 수집 결과, 위젯 등록 상태를 저장하며 지표 이력을 누적하지 않는다. 진단 기록에는 예외 종류·오류 코드·실패 메서드 이름만 남긴다.

로그아웃은 진행 중 읽기를 취소하고 계정 지표를 먼저 지운 뒤, 모든 웹 페이지를 닫고 앱의 프로필 데이터를 제거한다. 삭제가 중단되면 다음 연결 전에 정리를 다시 완료한다. 외부 브라우저 로그인에는 접근하지 않는다.

설정에서 수동 업데이트 확인과 하루 한 번 자동 확인을 제공한다. 새 버전 설치를 선택하면 서명·해시를 확인하고 같은 식별자/게시자의 MSIX로 교체한 뒤 앱을 재시작한다. 로그인과 설정은 보존한다. 자동 설치는 하지 않는다. Windows 공개 업데이트 목록과 정식 서명은 공개 배포 시 준비하며, 자세한 절차와 검증 경계는 [업데이트 안내](UPDATES.md)를 따른다. ARM64 실기기 검증은 별도 작업이다.

업데이트 구현과 실기기 결과는 [0.3.0 검증 기록](VALIDATION-0.3.md), 기존 기능 확인 범위는 [Windows 구현 검증 기록](VALIDATION.md)에 기록한다. 공개 배포 준비는 [릴리스 안내](RELEASE.md)를 따른다.

[Code signing policy / 코드 서명 정책](CODE-SIGNING-POLICY.md) · [Windows privacy / 개인정보·로그아웃·제거 안내](PRIVACY.md) · [무료 서명 연동 준비](signpath/README.md)
