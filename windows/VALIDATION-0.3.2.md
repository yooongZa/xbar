# Windows 0.3.2 로그인 UX 검증

2026-10-27, SIYONG의 실제 로그인 세션 2에서 검증했다. 최종 시험 설치본은 `LIMOD.XBar.Dev 0.3.2.1`이며 공개 Windows 릴리스는 게시하지 않았다.

## 수정

- 로그인된 계정의 ‘연결 관리’가 로그인 화면을 다시 열던 동작을 제거했다. 설정에는 **로그인됨 · 계정명 / X 분석 열기 / 로그아웃**을 표시한다. 위젯 메뉴도 로그인 상태에 맞는 동작을 제공한다.
- 첫 로그인과 만료된 로그인에는 **X에 로그인**을 표시한다. 안내는 **“X에 로그인한 뒤 ‘로그인 완료’를 눌러주세요.”**로 바꿨다. 한국어·영어·일본어를 함께 정리했다.
- 로그인된 상태의 기존 `xbar://login` 요청은 분석 화면으로 보낸다. 로그인 안내를 숨기고 확인된 지표와 수집 범위를 보존한다. 로그아웃 시 앱에 저장된 로그인 정보·지표가 삭제된다는 확인 문구를 명확히 했다.
- 실제 캡처 중 분석 화면이 첫 표시에서 비어 있다가 크기를 바꾸면 나타나는 문제를 재현했다. 창을 보여준 뒤 레이아웃과 내장 웹 화면의 크기·표시 상태를 동기화했다. [Microsoft의 HwndHost.UpdateWindowPos 설명](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndhost.updatewindowpos?view=windowsdesktop-10.0)에 맞춘 호출이다.

## 실기기 결과

- 0.3.1.1 → 0.3.2.0 → **0.3.2.1** MSIX 교체 후 X 로그인 유지와 새 지표 수집을 확인했다. `settings.json`과 `updates.json`의 SHA-256이 설치 전후 동일했다.
- 최종 설치본에서 로그인 버튼이 숨겨지고 분석·로그아웃만 표시됨을 확인했다. 실제 X 분석 화면의 첫 열기와 재열기 모두 창 크기를 바꾸지 않고 표시됐다. 로그인 완료 버튼·안내가 나타나지 않았다.
- 시험 인증서 신뢰와 개인키를 제거한 뒤 설정의 **앱 종료**로 PID 20252를 종료했다. 새 PID 11128로 재실행하고 16:54:45 KST의 새 수집과 게시물 5개를 확인했다. 이후 첫 분석 열기·기존 로그인 요청·재열기를 다시 검사했다.
- **Windows 로그인 시 시작 On**과 기존 위젯 설정을 보존했다. 이번 수정에서 Windows 로그아웃·재로그인은 반복하지 않았다. 이전 실제 재로그인 근거는 [자동 시작 검증](VALIDATION-STARTUP.md)에 있다.
- 실제 X 계정을 로그아웃하지 않았다. 첫 로그인·만료·로그아웃·로그인 완료 후 창 닫기는 격리된 시험 프로필과 로컬 응답으로 검사했다.

최종 개발 서명 MSIX SHA-256: `85bfa08af34a07f77d0636fac9302c9b8875204ff1314aa4cbc32d8ad62397ad`. Developer Mode는 변경하지 않았다. 공개 신뢰 서명과 새 일반 PC 설치·공개 다운로드에서 시작하는 업데이트 검증은 기존 배포 조건으로 남는다.

## 검사 및 재현

- Core 76, 계정·수집 등 Windows 회귀 66, 설정 22, 웹 화면 첫 표시·로그인·재열기 3개가 모두 통과했다. 최종 웹 화면 수정 후 Windows 회귀 66개를 다시 실행해 통과했다. Python 배포 도구 시험 1개도 통과했다.
- 웹 화면 검사는 내장 창의 실제 표시 상태와 크기를 확인하며 일반 Windows 빌드 스크립트에도 포함했다.
- 최종 소스: `8b252f9e0e0c27a8329786f6da76f29afc8fe1a8`. [GitHub 검사](https://github.com/yooongZa/xbar/actions/runs/40469969571)도 성공했다. Core 76·화면 배치 4·업데이트 22·설정 22개와 Python 1개, 서명 전 MSIX 패키징·출처 검사를 통과했다. 산출물 업로드는 껐다.

```text
dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64 -p:RestoreLockedMode=true
dotnet XBar.WindowsTests.dll <result.json>
dotnet XBar.WindowsTests.dll <result.json> --settings-only
dotnet XBar.WindowsTests.dll <result.json> --browser-only
python3 -m unittest discover -s windows/scripts -p 'test_*.py'
dotnet publish XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true
package.ps1 -Version 0.3.2.1
signtool verify /pa XBar-0.3.2.1-x64.msix
```

변경 파일: `XBar.Core/L10n.cs`, `XBar.App/SettingsWindow.cs`, `Widgets.cs`, `Collector.cs`, `WebSession.cs`, Windows 시험 코드·빌드 스크립트·버전·안내 문서. macOS 소스와 배포 경로는 수정하지 않았다.

실제 계정 화면·분석 화면·바탕화면 캡처와 설치·재실행 근거는 로컬 `outputs/windows/login-ux-20261027/`에 보관한다. 시험용 화면은 `tests/`와 구분한다. 한 대의 Windows 실기기 결과이며 다른 Windows 빌드·ARM64 검증을 포함하지 않는다.
