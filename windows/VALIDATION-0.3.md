# Windows 0.3.0 업데이트 검증

검증일: 2026-10-27. Windows 11 x64 빌드 26200, SIYONG 로그인 세션, .NET SDK 10.0.107. 설치된 일반 시험본은 `LIMOD.XBar.Dev` **0.3.0.0**이다. 공개 출시를 의미하지 않는다.

## 완료 조건과 결과

| 범위 | 확인 결과 |
| --- | --- |
| macOS와 같은 사용자 흐름 | 설정의 수동 확인, 기본 켜짐인 24시간 자동 확인, 설정 안 새 버전 표시, 설치 선택 후 다운로드·검증·교체·재시작, 나중에/버전 건너뛰기/취소 구현 |
| Core | Windows 74개 통과. 실제 Ed25519 서명/변조, 호환성·버전·URL 제한, 파일 길이·해시·매니페스트, 취소 정리, 24시간 주기, 404 구분 |
| 업데이트 통합 | 로그인 세션에서 22개 통과. 중복 요청, 취소, 오래된 설치 선택, 준비/설치 실패 복구, 건너뛰기, 한·영·일/밝음·어두움 UI, 재확인 후 실제 버튼 동작, 네트워크 시간 초과 분리, 재시작 결과 처리. 실패 주입은 시험용 HTTP/installer 대역을 사용 |
| 기존 기능 회귀 | 로그인 세션에서 59개 통과. WebView2 읽기·로그아웃, 수집 재시도·중단·복구, 계정/기간 검증, 위젯/설정/접근성·배율·테마. 네트워크/절전 경로는 앱 입력으로 모사했으며 시스템 설정을 바꾸지 않음 |
| 실제 다운로드 취소 | 별도 설치 앱에서 다운로드 후 취소. 구버전 `0.3.0.12` 유지, 임시 파일 0개, 설치 인계 기록 없음 |
| 실제 변조 목록 차단 | Ed25519 서명 1비트 변조. 설치 버튼 숨김, 검증 오류 안내, `0.3.0.12` 유지 |
| 실제 앱 자체 업데이트 | 같은 식별자의 서명된 MSIX **0.3.0.12 → 0.3.0.13**. 앱의 설치 버튼 한 번으로 다운로드/검증/OS 교체 완료. Windows가 PID 16752를 종료하고 새 버전 PID 23452를 자동 실행. 재시작 후 설치 기록 정리 |
| 시험 세션 보존 | 별도 WebView2 프로필의 합성 쿠키가 실제 MSIX 교체 후 유지됨. 자동 확인 꺼짐, Desktop, Week, 10분, 한국어, 효과 꺼짐 유지 |
| 기존 사용자 앱 설치 | 일반 시험본 **0.2.0.1 → 0.3.0.0**은 외부 MSIX 설치로 최초 updater를 도입. 기존 settings.json의 SHA-256 동일. X 재로그인 없이 오늘 지표와 게시물 3개를 새로 수집 |
| 두 위젯 유지 | Both/Today 유지, 등록 카드 1개. Widgets Board 실제 렌더링 및 `0.3.0.0` 경로의 WidgetProvider 프로세스 확인 |
| 실제 설정 조작 | ‘새 버전 자동 확인’ UI를 끄고 파일 저장을 확인, 다시 기본 켜짐으로 복구 |
| 공개 채널 상태 | 일반 앱의 고정 공개 URL 확인은 아직 미게시 상태. ‘Windows 업데이트 채널이 아직 공개되지 않았습니다’ 안내 확인 |
| 정리 후 재실행 | 시험 인증서 신뢰/개인키 제거 후 일반 앱 재실행. Ready, 게시물 3개, Both/Today, 로그인 유지, 기존 설정 파일 동일. Developer Mode는 시작/종료 모두 미설정 |

**자동 시험 총 155개(74 + 22 + 59)**. 실제 MSIX/쿠키/설정 조작 검증은 이 숫자와 별도다. 자동 시험 성공을 신규 PC 설치나 공개 배포 성공으로 해석하지 않는다.

## 설치물 검사

- 파일: `outputs/windows/XBar-0.3.0.0-x64.msix`
- SHA-256: `75982aedf337f32010c5c3eb3f66d2822354d689bc0d8ef5c0d1bdcdd6dd7c67`
- 임시 개발 인증서로 서명했고 설치 전 Windows `signtool verify /pa` 통과. 정식 배포용 신뢰 서명은 아니다.
- 일반 설치물에는 `UpdaterLab.json`과 lab 전용 source 분기가 없고 고정 HTTPS 업데이트 주소가 포함된다.
- 신규 의존성 NSec.Cryptography/libsodium의 라이선스 고지를 포함한다.
- 포함한 `Extractor.js`, `PageContext.js`는 기존 macOS 리소스와 바이트가 같다. macOS 소스·appcast·공개 릴리스 변경 없음.

## 증거와 실행

로컬 `outputs/windows/updater-evidence/`에 Core TRX, UI 22개/기존 회귀 59개 JSON, 실제 교체/취소/변조/쿠키/설정/정리 결과를 보관한다. `updater-artifact-audit.json`은 설치물의 manifest/서명 존재/일반 주소/lab 코드 배제/공통 JS 검사를 기록한다. 화면은 `updater-available.png`, `updater-public-channel.png`, `updater-native.png`, `updater-desktop-final.png`이다.

주요 명령:

```text
dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c Release
dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64
dotnet XBar.WindowsTests.dll <result.json> --updates-only
dotnet XBar.WindowsTests.dll <regression.json>
dotnet publish XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true -p:UpdaterLab=false
package.ps1 -Version 0.3.0.0
signtool verify /pa XBar-0.3.0.0-x64.msix
python3 windows/scripts/prepare-update.py ... --allow-development --lab
```

실기기 UI 시험은 원격 SSH의 Session 0이 아닌 로그인 세션에서 임시 예약 작업으로 실행했다. 종료 시 해당 작업, 루프백 서버/SSH 터널, 별도 lab 패키지/합성 프로필, 임시 TrustedPeople 인증서 및 사용자 개인키를 정리했다.

## 남은 배포 작업

공개 Windows feed/다운로드 파일 게시와 정식 배포 서명은 수행하지 않았다. 실제 앱 자체 업데이트는 분리된 lab 식별자로 검증했고 일반 사용자 앱에는 updater 최초 도입을 위한 외부 MSIX 설치를 사용했다. 새 Windows PC 설치, 공개 GitHub 다운로드에서의 전체 업데이트, ARM64, 장시간 실행·물리 절전·다중 모니터 시험은 이번 완료 범위 밖이다. 배포 준비 절차는 [UPDATES.md](UPDATES.md)를 따른다.
