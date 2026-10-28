# X-Bar Windows

Windows 11 x64용 **바탕화면 위젯**이다. 0.4부터 Windows Widgets Board 공급자를 배포에서 제외하고, 코드 서명 없는 사용자 계정용 EXE 설치 프로그램을 사용한다. 관리자 권한·인증서 등록·개발자 모드 없이 설치하도록 구성했다.

2026-10-28 공개: **[0.4.2 beta.1 릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.2-beta.1)** · [Windows 설치 파일](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.2-beta.1/XBar-0.4.2.0-windows-x64-setup.exe) · [검증 기록](VALIDATION-0.4.2.md).

설치 파일에 Windows 코드 서명이 없어 SmartScreen 경고가 표시되거나 PC 보안 정책에 따라 실행이 차단될 수 있다. 앱은 Windows 보안 설정을 변경하지 않는다. 배포 파일은 [GitHub Releases](https://github.com/yooongZa/xbar/releases)의 Windows 릴리스에서 확인한다. 현재 공개 상태와 검증 결과는 해당 릴리스에 기록한다.

## 사용

1. `XBar-<version>-windows-x64-setup.exe`를 실행해 설치한다. 설치 위치는 사용자 폴더의 `AppData\Local\Programs\X-Bar`다.
2. X-Bar에서 **X에 로그인**을 선택한다. X 로그인이 확인되면 별도 완료 버튼 없이 창이 닫히고 지표를 수집한다. X 분석 화면에 접근할 수 있는 계정과 Microsoft Edge WebView2 Runtime이 필요하다.
3. 위젯 제목을 끌어 이동하고 가장자리로 크기를 조정한다. `⋯ → 설정`에서 항상 위에, 효과, 데이터 확인 주기, 언어, Windows 로그인 시 시작을 선택한 뒤 **저장**한다.
4. 설정의 **앱 업데이트 → 업데이트 확인**에서 새 버전을 확인한다. 하루 한 번 자동 확인도 제공한다. **설치하고 재시작**을 선택하면 업데이트한다.

한국어·영어·일본어, 밝은/어두운 테마와 고대비, 게시물 접기, 오늘·최근 2일·최근 7일, 좋아요·재게시·노출과 첫 확인 이후 증가를 지원한다. 설정을 닫으면 저장 전 선택은 취소된다. 창을 닫아도 수집은 계속되며 **앱 종료**로 끝낸다.

## 데이터와 업데이트

로그인과 지표는 앱 전용 WebView2 프로필 및 현재 사용자 `%LOCALAPPDATA%\XBar`에 저장한다. 원격 지표 서버를 운영하지 않는다. 로그아웃은 앱 전용 로그인 정보를 지우며, 설치·업데이트는 해당 폴더를 유지한다. 제거 후에도 데이터 폴더를 보존하므로 데이터를 지우려면 먼저 앱에서 로그아웃한다.

설치 EXE에는 Authenticode(Windows 코드 서명)를 붙이지 않는다. 업데이트 목록은 기존 고정 키로 검증하고, 파일 해시·길이·제품·버전과 다운로드 출처를 검사한다. 이 검증은 코드 서명 인증서 구매 없이 수행하며 Windows의 게시자 신뢰 판정과는 별개다. [업데이트 동작](UPDATES.md), [배포 절차](RELEASE.md), [코드 서명 정책](CODE-SIGNING-POLICY.md)을 참고한다.

## 빌드와 시험

Windows 11 x64, .NET SDK 10.0.107 호환 패치, WebView2 Runtime, Inno Setup 6.7.3을 사용한다. 공개 저장소의 [Windows workflow](../.github/workflows/windows-build.yml)도 잠금 복원과 같은 시험·설치 파일 검사를 수행한다. 산출물 업로드는 기본 꺼짐이다.

```powershell
.\windows\scripts\build.ps1
.\windows\scripts\package-desktop.ps1 -Version 0.4.2.0 -ISCC 'C:\path\to\ISCC.exe'
```

WebView2 시험은 별도 프로필과 로컬 응답을 사용하며 Windows에 로그인된 사용자 세션에서 실행한다. `--desktop-only`는 바탕화면 전용 설정, 사용자별 자동 실행, EXE 업데이트 검증을 검사한다. `--recovery-only`는 파일 저장 실패 후 로그아웃·재로그인 복구, 노출 5·10·30분과 게시물 10분의 독립적인 수집, 취소·계정 변경을 검사한다. `--updates-only`에는 통신 실패 후 자동 재시도와 성공 후 하루 주기 복귀 검사가 포함된다. 소스에 남아 있는 이전 MSIX·보드 구현은 0.4 설치 파일에 포함하지 않는다.

이전 검증 기록: [0.3.3 자동 로그인](VALIDATION-0.3.3.md), [0.3.2 로그인 UX](VALIDATION-0.3.2.md), [0.3.1 화면 개선](VALIDATION-0.3.1.md), [기존 MSIX 자동 시작](VALIDATION-STARTUP.md). 이전 MSIX의 시험 결과와 새 EXE 배포 결과를 구분한다.

[Windows 개인정보 안내](PRIVACY.md) · [GPL-3.0-only](../LICENSE)
