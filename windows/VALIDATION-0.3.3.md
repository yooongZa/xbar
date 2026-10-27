# Windows 0.3.3 자동 로그인 확인

2026-10-27, SIYONG의 Windows 11 실제 로그인 세션 2에서 검증했다. 최종 시험 설치본은 **LIMOD.XBar.Dev 0.3.3.3**이다. 공개 Windows 릴리스는 게시하지 않았다.

## 변경

- ‘로그인 완료’ 버튼을 제거했다. 로그인된 계정이 확인되면 창을 자동으로 닫고 위젯 지표를 갱신한다. 안내는 **“X에 로그인하면 이 창이 자동으로 닫힙니다.”**로 바꿨다.
- 동일한 계정과 탐색 상태를 연속 확인하고, 같은 앱 전용 프로필의 별도 읽기 화면에서 로그인 상태를 다시 검증한다. 검증을 마친 뒤 원래 창을 다시 확인하므로 그 사이의 입력·탐색·계정 변경을 반영한다.
- 실제 X는 로그인된 화면 위에 로그인 창을 표시하면서 아이디 입력란에 자동 초점을 주고 투명한 비밀번호 필드를 함께 둔다. 초기 개발 빌드에서는 이를 입력 중으로 판단해 창이 닫히지 않았다. 실기기에서 재현한 두 조건을 최종 구현과 시험에 반영했다.
- 실제 클릭·키 입력·입력 시작, 보이는 비밀번호·인증 코드, 초점이 있는 인증 입력란은 보호한다. 확인 과정은 입력값을 읽거나 로그인 화면을 조작하지 않는다. 사용자가 창을 닫거나 로그아웃·앱 종료를 하면 확인을 취소하며, 늦은 결과가 로그인을 복원하지 않는다.
- 중복 로그인 요청은 창 하나로 합친다. 일반 X 분석 화면의 열기·닫기, 기존 지표 수집 규칙, 설정, 자동 시작, 앱 전용 쿠키 저장은 유지한다. macOS 공통 JavaScript는 변경하지 않았다.

## 실기기 결과

1. 0.3.3.2 → **0.3.3.3** MSIX 교체 후 기존 X 로그인과 새 지표 수집을 확인했다. 설정·업데이트 기록의 SHA-256이 설치 전후 같았다.
2. 앱을 종료하고 앱의 `Connected` 표시만 잠시 해제했다. 실제 X 프로필·쿠키를 유지한 상태에서 `xbar://login`으로 자동 확인을 시작했다. 비밀번호나 인증 코드를 입력하지 않았다.
3. **18:00:18.673 KST**에 로그인 창과 안내를 관찰했고, 완료 버튼이 없음을 확인했다. **18:00:23.962 KST**에 자동으로 창이 사라지고 로그인 상태가 저장됐다. 관찰 간격은 약 **5.3초**다.
4. **18:00:32.008 KST**의 새 지표와 게시물 3개를 수집해 `Ready / PostsCurrent=true`가 됐다. 계정 설정 화면에서 로그인 상태와 분석·로그아웃 동작을 캡처했다.
5. 최종 확인에서도 로그인 창이 닫혀 있고 앱은 세션 2에서 실행 중이었다. 설정·업데이트 기록 해시가 그대로이며, **Windows 로그인 시 시작 On**을 유지했다. 이번 작업에서 Windows 재로그인은 반복하지 않았다.

시험용 인증서 신뢰와 개인키를 제거했다. Developer Mode는 미설정 상태를 유지했다. 최종 개발 서명 MSIX SHA-256:

```text
17a7cceeb871d01f8090d6c716e5a9cf3b26c6cdc61ef00a981ab9c9dc21fb94
```

## 시험과 재현

- 최종 소스 `7b4a248392bf1d7aabc34881412d4fd44528af16`: 실제 WebView2의 격리된 시험 프로필로 **자동 로그인 38/38** 통과. 자동 닫힘·수집 시작, 자동 초점·투명 필드, 비밀번호·OTP, 입력 도중의 백그라운드 확인, 계정 불일치, 오프라인, 창 닫기·로그아웃·종료 취소를 포함한다.
- Windows 수집·복구·화면 회귀 **66/66**은 `ffc445d`에서 통과했다. 이후 변경은 로그인 입력란의 표시 여부 판정이며 최종 38개 시험으로 다시 확인했다.
- 최종 소스의 [GitHub 검사](https://github.com/yooongZa/xbar/actions/runs/40477376073)가 성공했다. Core 76·화면 배치 4·업데이트 22·설정 22·Python 배포 도구 1개와 빌드·서명 전 MSIX 패키징·출처 검사를 통과했다. 산출물 업로드를 껐다.

```text
dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet publish XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true
dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64 -p:RestoreLockedMode=true
dotnet XBar.WindowsTests.dll <result.json> --login-only
dotnet XBar.WindowsTests.dll <result.json>
package.ps1 -Version 0.3.3.3
signtool verify /pa XBar-0.3.3.3-x64.msix
```

주요 변경 파일은 `XBar.App/Collector.cs`, `WebSession.cs`, `XBar.Core/L10n.cs`, `XBar.WindowsTests/AutoLoginTests.cs`, `Program.cs`, 빌드 스크립트·버전·README다. 일시적인 실계정 진단 코드는 제품과 일반 시험에 포함하지 않았다.

## 범위와 근거

실제 X 검증은 **저장된 로그인 세션을 자동으로 확인하는 경로**다. 실제 계정을 로그아웃하고 새 비밀번호·2단계 인증을 입력하는 과정은 반복하지 않았다. 새 로그인 완료 후 리디렉션과 입력 중 보호는 격리된 시험 응답으로 확인했다. 다른 Windows PC·ARM64와 공개 신뢰 서명·일반 사용자 설치 경로는 이번 검증 범위에 포함하지 않는다.

실기기 캡처와 설치·자동 닫힘 시각·최종 상태·인증서 정리 근거는 로컬 `outputs/windows/auto-login-20261027/`에 보관한다. 로그인 창 이미지는 페이지 로딩 전의 최초 안내 캡처다. 자동 닫힘·후속 수집의 근거는 관찰 시각과 상태를 기록한 JSON이며, 캡처만으로 판단하지 않았다.
