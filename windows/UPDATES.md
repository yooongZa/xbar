# Windows 앱 업데이트

구현 목표: macOS AppUpdater와 동일한 사용자 흐름. 설정의 수동 확인, 기본 켜짐인 하루 한 번 자동 확인, 설정 안 새 버전 알림, 한 번의 설치 선택으로 다운로드·서명/해시 확인·MSIX 교체·재시작. 자동 설치는 하지 않는다.

## 변경 전 위험·대안·검증

| 경로 | 위험 | 처리 및 검증 |
| --- | --- | --- |
| 확인·다운로드 중복/취소 | 이전 요청이 새 상태를 덮거나 취소 후 설치 | UI thread의 단일 updater, 세션별 선택, 취소 후 선택 폐기. 중복·취소·재시도 시험 |
| 업데이트 목록/파일 | 변조된 주소·다른 앱·하위 버전 설치 | 고정 Ed25519 키로 목록 검증, HTTPS 및 다운로드 출처 제한, SHA-256·길이·MSIX identity/publisher/architecture/version 검사. 변조·오류·downgrade 시험 |
| 앱 교체 | 실행 중 파일 충돌, 강제 종료, 실패 후 데이터 손실 | Windows PackageManager와 RegisterApplicationRestart 사용. 앱 제거/사용자 데이터 복사 없이 같은 identity의 MSIX update. 실제 설치·실패·재시작 시험 |
| 설정 저장 경쟁 | 수집 설정 저장이 업데이트 설정 덮어쓰기 | 업데이트 환경 설정을 독립 파일에 원자적으로 기록 |
| 수집과 설치 | 종료 중 미완료 응답, 잠금 중 설치 | 파일 검증 완료 후 수집 중지, 설치 실패 시 수집 복구. 프로필 삭제 금지 |

공개 배포와 구현 시험을 구분한다. macOS feed와 설치물은 변경하지 않는다. Windows 전용 서명된 feed·게시 도구를 준비하며, 공개 업데이트 주소의 게시 여부를 별도로 기록한다. 개인키는 기존 macOS Keychain의 `com.limod.xbar.updates`를 도구로 사용하고 내보내지 않는다. MSIX 서명과 feed 서명은 별개이며 updater가 인증서 신뢰나 Developer Mode를 자동 변경하지 않는다.

공식 근거: [Windows 비 Store 업데이트](https://learn.microsoft.com/en-us/windows/msix/non-store-developer-updates), [NSec 서명 검증](https://nsec.rocks/docs/api/nsec.cryptography.signaturealgorithm).

## 사용자 동작과 보존 범위

- 설정의 **업데이트 확인**과 위젯 메뉴에서 같은 창을 연다. 자동 확인은 기본 켜짐이며 마지막 확인 후 24시간이 지나면 실행한다.
- 자동 확인 결과는 설정에 표시한다. 설치는 사용자가 **설치하고 재시작**을 누른 경우에만 진행한다. 나중에, 이 버전 건너뛰기, 확인/다운로드 취소를 제공한다. 건너뛴 버전도 수동 확인으로 다시 설치할 수 있다.
- 설치 선택은 해당 화면에만 유효하고 취소·오류·새 확인·앱 종료 때 폐기한다. 중복 확인과 중복 설치를 합친다.
- 피드 검증 → 크기 제한 다운로드 → SHA-256/길이 → MSIX 식별자/게시자/아키텍처/버전 → Windows 서명 신뢰 검증 → 파일 재검증 → OS 교체 순서다. 수집은 마지막 단계에서만 중지하며 설치 실패 시 다시 시작한다.
- Windows Restart Manager의 60초 실행 조건 때문에 앱을 막 실행한 경우 준비 단계에서 잠시 기다릴 수 있다. 대기 중에도 취소할 수 있다.
- 업데이트 설정은 `updates.json`, 설치 결과 확인은 `update-install.json`, 임시 파일은 `Updates/<임의 ID>/`에 저장한다. 쿠키/계정/지표는 업데이트 서버에 보내지 않는다. 기존 WebView2 저장소를 삭제·복사·교체하지 않는다.
- 공개 채널이 404이면 ‘Windows 업데이트 채널이 아직 공개되지 않았습니다’를 표시한다. 연결 오류, 검증 오류, 사용자 취소를 구분한다.

## Windows 배포 후보 준비

고정 주소: `https://yooongza.github.io/xbar/windows-updates.json`. macOS `appcast.xml`은 그대로 둔다.

1. 같은 identity/publisher의 더 높은 네 자리 버전으로 MSIX를 빌드한다. 정식 배포에서는 적절한 공개 신뢰 서명으로 서명하고 Windows `signtool verify /pa`를 통과시킨다.
2. `ko`/`en`/`ja` 키의 JSON 파일에 일반 텍스트 릴리스 노트를 준비한다.
3. macOS의 기존 Sparkle 2.9.6 도구와 Keychain 계정으로 Windows 목록을 서명한다.

```sh
python3 windows/scripts/prepare-update.py XBar-0.3.1.0-x64.msix \
  --tools /path/to/Sparkle/bin \
  --feed outputs/windows/windows-updates.json \
  --url https://github.com/yooongZa/xbar/releases/download/windows-v0.3.1/XBar-0.3.1.0-x64.msix \
  --notes release-notes.json \
  --verification XBar-0.3.1.0-x64.msix.verification.json
```

개발용 식별자의 비공개 시험 후보에는 `--allow-development`가 필요하다. 도구는 기존 공개키 일치를 확인하고 개인키를 내보내지 않으며, 결과를 자동 게시하지 않는다. 공개 배포에서는 패키지를 먼저 게시/다운로드 검증한 뒤 서명된 JSON 파일을 공개한다. feed는 원본 payload와 그 Ed25519 서명을 함께 담는 단일 파일이다. 실행 앱에는 feed URL/검증 키를 외부 설정이나 명령줄로 바꾸는 기능이 없다.

## 시험 경계

`-p:UpdaterLab=true`로만 만드는 `LIMOD.XBar.UpdaterLab`은 데이터 폴더와 Named pipe가 분리된다. 일반 프로토콜·자동 시작·Widgets Board 등록을 제거한 시험 패키지이며, 루프백 URL은 해당 빌드에만 허용된다. 일반 패키징은 `UpdaterLab.json`이 섞이면 중단한다. 실제 계정 대신 별도 WebView2 프로필의 합성 쿠키로 세션 보존을 시험한다.

```powershell
dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj -c Release
dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64
dotnet windows/XBar.WindowsTests/bin/Release/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll updater-ui-results.json --updates-only
```

Windows UI 시험은 로그인된 사용자 세션에서 실행한다. 서명된 MSIX의 실제 교체/자동 재시작 증거는 별도 실기기 검증 기록에 남긴다. 공개 feed 게시, 공개 서명/신규 PC 신뢰, ARM64 검증은 구현·비공개 시험과 구분한다.
