# Windows 0.4.1 beta.1 공개 배포 검증

2026-10-28 KST. **바탕화면 위젯 전용·코드 서명 없는 EXE를 공개했고, 실제 Windows PC를 0.4.1.0으로 전환했다.** 공개 파일을 사용하는 앱 내 업데이트와 자동 재시작까지 확인했다.

- [공개 릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.1-beta.1)
- [Windows 설치 파일](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.1-beta.1/XBar-0.4.1.0-windows-x64-setup.exe)
- 빌드 소스: `442da0c4988bdb27f3e488100074e8e32a471f3f`. 해당 커밋을 `git archive`로 추출한 새 폴더에서 잠금 복원·빌드·패키징했다.
- [GitHub 검사 성공](https://github.com/yooongZa/xbar/actions/runs/40617674346). 공개 표준 실행기를 사용했고 산출물 저장 업로드는 비활성화했다.
- 업데이트 목록 게시 커밋: `5074616ae8330c4d1cbba0fb9c0f3550e143e158`.

## 변경과 문제 수정

Windows 보드 공급자를 배포에서 제외하고 사용자별 Inno Setup EXE 설치, HKCU 자동 시작 등록, EXE 업데이트 검증을 연결했다. 앱 전용 로그인 데이터·설정, 지표 수집과 로그인 자동 확인을 유지한다. 업데이트 목록의 Ed25519 서명·다운로드 파일 해시 검사는 계속 사용한다.

첫 실제 EXE 업데이트에서 종료 오류를 재현했다. Windows Restart Manager가 창을 닫은 뒤 `Collector.Dispose()`가 최종 상태를 발행하면서 닫힌 위젯을 다시 표시하려고 했다. `Program.StopRuntime()`이 종료 상태를 먼저 설정하고 화면 갱신을 차단하도록 수정했다. 수정 후 별도 프로필 및 실제 공개 업데이트에서 종료·교체·재시작을 검증했다. Windows의 [앱 재시작 등록](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-registerapplicationrestart)과 Inno Setup의 [재시작 처리](https://jrsoftware.org/ishelp/topic_setup_restartapplications.htm)를 사용하며 시스템 재부팅·강제 앱 종료 옵션은 사용하지 않는다.

배포 도구는 설치 파일과 포함 앱의 버전 불일치를 거부하고 일반 배포에 시험 설정이 포함되는 것을 차단한다. 준비 안내 문구도 앱 재시작을 설명하도록 고쳤다.

## 자동 검사

| 검사 | 통과 |
| --- | ---: |
| Core | 77 |
| 바탕화면 배포·자동 시작·EXE 검증 | 14 |
| 설정 | 22 |
| 업데이트 흐름·취소·실패 복구 | 22 |
| 웹 화면 표시 | 3 |
| 자동 로그인 확인 | 38 |
| 수집·복구·화면 회귀 | 66 |
| 화면 배치 | 4 |
| Python 배포 도구 | 2 |
| 합계 | **248** |

추가 실기기 시험에서 EXE 교체 후 별도 프로필의 합성 로그인 쿠키·설정 보존을 확인했다. 정상 서명된 시험 업데이트 목록에 대해 내려받은 EXE 한 바이트를 변조했을 때 검증 오류를 표시하고 설치·프로세스 교체를 수행하지 않았다. 배포 도구의 버전 불일치 거부도 확인했다.

## 실제 설치와 공개 업데이트

1. 기존 MSIX 0.3.3.3을 정상 종료했다. 사용자 데이터 4,054개 파일, 469,494,585바이트를 별도 폴더로 복사하고 모든 파일의 SHA-256을 대조했다. 백업 위치는 해당 PC의 `%LOCALAPPDATA%\XBarMigrationBackups\20261028-before-exe`다.
2. 최종 EXE 0.4.1.0을 일반 사용자 권한으로 설치했다. 설치 과정은 계정·설정 파일을 변경하지 않았으며, X 재로그인 없이 새 지표를 수집했다. 표시 모드를 Desktop으로 전환하고 자동 실행 On을 유지했다. 다른 기존 설정은 동일했다. 확인 후 이전 MSIX 패키지와 보드 등록을 제거했다.
3. 공개 업데이트 경로를 시험하기 위해 같은 소스의 0.4.0.0 시험본을 잠시 설치했다. 설정·자동 실행 등록과 로그인 유지, 새 지표 수집을 확인했다.
4. **11:01:15 KST** 앱의 ‘설치하고 재시작’으로 공개 업데이트를 시작했다. 고정 공개 목록과 GitHub 설치 파일만 사용했으며 앱에 `UpdaterLab.json`이 없음을 확인했다.
5. PID 18364 → 22184로 교체·자동 재시작됐고 최종 버전은 **0.4.1.0**이다. **11:02:27 KST**에 새 지표를 수집했으며 11:02:31에 Ready·로그인 유지·현재 게시물 수집을 확인했다. 설정 파일 해시, 자동 시작 등록, 업데이트 자동 확인·건너뛰기 설정이 유지됐고 설치 완료 기록이 정상 정리됐다.
6. 최종 앱에서 공개 업데이트를 다시 확인해 ‘현재 설치할 새 버전이 없습니다.’를 확인했다. 바탕화면 위젯만 실행 상태로 남겼다. 시험용 앱·합성 프로필·루프백 서버·예약 작업을 정리했다.

## 공개 파일

설치 파일과 소스 ZIP을 인증 없이 공개 URL에서 다시 내려받아 빌드 파일과 대조했다. 공개 업데이트 목록도 로컬 서명 목록과 바이트 단위로 일치했다. GitHub의 최신 정식 릴리스는 macOS `v0.5.4`를 유지한다. macOS 앱·appcast·웹 다운로드 파일은 변경하지 않았다.

| 파일 | SHA-256 |
| --- | --- |
| `XBar-0.4.1.0-windows-x64-setup.exe` (50,748,900바이트) | `6f7903eadd13816aa81bd88be91882fafbdf4d78923f9048b0c635f5757f694c` |
| `XBar-0.4.1-beta.1-source.zip` | `16d5c6afafca4cd36ba156be048fc6226a41379f838b113d5cb759b85cd8e19a` |

## 주요 파일과 실행 명령

주요 파일: `XBar.App/Program.cs`, `DesktopUpdateInstaller.cs`, `DesktopStartupPreference.cs`, `AppUpdater.cs`, `SettingsWindow.cs`, `XBar.Core/Updates.cs`, `Commands.cs`, `L10n.cs`, `package/desktop.iss`, `scripts/package-desktop.ps1`, `prepare-update.py`, 시험 코드, `.github/workflows/windows-build.yml`, 저장소 루트 `windows-updates.json`.

```text
git archive 442da0c
dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet publish XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true
dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64 -p:RestoreLockedMode=true
dotnet XBar.WindowsTests.dll <result.json> [--desktop-only|--settings-only|--updates-only|--browser-only|--login-only|--layout-only]
python3 -m unittest discover -s windows/scripts -p 'test_*.py'
package-desktop.ps1 -Version 0.4.1.0 -ISCC <verified-compiler>
prepare-update.py <installer> --desktop-version 0.4.1.0 --verification <report> ...
gh workflow run windows-build.yml --ref main -f upload_artifact=false
gh release create / upload / edit windows-v0.4.1-beta.1
```

실기기 캡처와 원본 시험 결과는 로컬 `outputs/windows/desktop-20261028/`에 보관하며 실제 계정 화면은 공개 릴리스에 포함하지 않았다.

## 남은 검증 범위

- 한 대의 Windows 11 x64 PC 결과다. 다른 일반 PC, ARM64, 장기 실행, 물리 절전·복귀, 전원 종료 후 부팅은 별도 검증 대상이다.
- 새 EXE의 자동 시작 등록 유지와 해당 실행 명령의 동작은 확인했다. Windows 로그아웃·재로그인은 이번에 반복하지 않았다. 이전 MSIX의 실제 재로그인 결과는 [별도 기록](VALIDATION-STARTUP.md)에 있다.
- 실제 X에서는 저장된 로그인 세션을 사용했다. 새 비밀번호·2단계 인증 입력을 포함한 경로는 격리된 자동 시험으로 확인했다.
- 설치 EXE는 사용자 결정에 따라 코드 서명이 없다. 다른 PC의 SmartScreen·조직 보안 정책에 따른 경고나 실행 차단 가능성은 남는다.
