# Windows 최초 로그인 수정 검증

2026-10-28 KST, Windows 11 x64 실기기에서 확인. `main`의 `5a5e391`에 로그인 관련 변경을 적용한 시험 빌드를 사용했다. 설치된 0.4.2 앱과 분리한 빈 WebView2 프로필에서 실행했다.

## 변경

- Google 버튼과 직접 Google 인증 주소 이동을 감지해 한국어·영어·일본어로 X 이메일/사용자 아이디 로그인 방법을 안내한다. 입력하던 X 화면과 내용을 유지한다. 앱 내 Google OAuth 직접 로그인은 지원하지 않는다.
- Apple의 새 창 요청에 같은 WebView2 환경의 새 창을 연결한다. `window.opener`와 `postMessage` 인증 결과 전달 경로를 보존한다. 인증 창이 열려 있거나 생성 중이면 로그인 완료 판정을 보류한다.
- 인증 취소, 원래 X 창 닫기, 로그아웃, 앱 종료에서 소유한 인증 창을 정리한다. 초기화 도중 닫혀도 뒤늦게 창이 나타나지 않도록 검사한다.
- 미연결 또는 재로그인이 필요한 사용자는 설정의 계정·데이터 탭을 먼저 연다. 계정 연결 목적과 X Analytics 접근 조건, 로그인 버튼을 함께 표시한다. 이미 연결된 사용자의 위젯 탭 진입은 유지한다.

Google 처리 방향은 Mac 앱 내부 로그인과 같다. Mac의 별도 Safari 확장 연결은 Windows에 이식하지 않았다. Google의 [OAuth 정책](https://developers.google.com/identity/protocols/oauth2/policies)은 개발자가 제어하는 내장 브라우저 인증을 금지한다. Apple 팝업 구현은 Microsoft의 [NewWindowRequested 지침](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2newwindowrequestedeventargs?view=webview2-1.0.4255-prerelease)을 따랐다.

## 검증

- Release 빌드: 경고 0개, 오류 0개.
- Core: 84/84 통과.
- 최초 로그인·팝업: 51/51 통과. Google 안내/입력 보존, Apple opener 통신/결과 전달, 앱 프로필 공유, 조기 완료 방지, 취소/로그아웃/종료 정리를 포함한다.
- 설정: 25/25 통과. 최초 계정 탭, 연결 후 안내 숨김, 기존 사용자 기본 탭, 세 언어·두 테마의 표시와 최소 창 크기를 포함한다.
- 브라우저 표시: 3/3 통과.
- 수집 복구: 25/25 통과.
- Windows 전체 회귀: 66/66 통과. 수집, 계정 전환, 취소, 로그아웃, 종료와 기존 자동 로그인 동작을 포함한다.
- 최종 소스의 자동 검사 합계: 254/254 통과. 시험 실행 파일과 실제 페이지 검사 실행 파일의 `XBar.dll` 해시가 최종 빌드와 동일하다.
- 실제 X·Apple 페이지: 7/7 검사 통과. Google 안내, 원래 X 페이지 보존, Apple 팝업 연결, 취소 후 복귀, 미연결 유지 확인.
- 실제 Apple 페이지의 이메일/전화번호 입력 폼이 화면에 표시됨을 캡처로 확인했다. DOM에 암호 요소도 있지만, 실제 암호 입력 단계까지 진행하지 않았다.

## 실행 명령

```text
dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64 --no-self-contained -p:RestoreLockedMode=true
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -r win-x64 -p:RestoreLockedMode=true
dotnet XBar.WindowsTests.dll <결과.json> --login-only
dotnet XBar.WindowsTests.dll <결과.json> --settings-only
dotnet XBar.WindowsTests.dll <결과.json> --browser-only
dotnet XBar.WindowsTests.dll <결과.json> --recovery-only
dotnet XBar.WindowsTests.dll <결과.json>
git diff --check
```

Windows UI 검사는 로그인된 데스크톱 세션에서 실행했다. 실제 페이지 검증용 실행 파일은 `outputs/windows/login-fix-20261028/live-harness/`에 있고, 캡처·DOM 결과는 `outputs/windows/login-fix-20261028/live/`에 있다.

자동 검사 JSON과 Core TRX는 `outputs/windows/login-fix-20261028/tests/`에 보관했다. `production-preserved.json`에서 기존 0.4.2 프로세스 유지, 설정 파일 해시 일치, 연결 상태 유지, 수집 상태 `Ready`를 확인했다.
이번 검증에서 만든 임시 프로필 13개와 실행용 예약 작업을 정리했다. 결과와 캡처는 보존했다.

## 범위와 남은 확인

실계정의 비밀번호·2단계 인증은 입력하지 않았다. 따라서 Apple 인증 최종 완료와 신규 실계정의 지표 수집은 사용자 확인이 남는다. 팝업의 결과 전달과 자동 로그인 완료 로직은 격리된 테스트 페이지로 검증했다.

제품 코드만 수정했으며 설치본 교체, 버전 증가, GitHub 릴리스 게시, 업데이트 피드 변경은 하지 않았다.
