# Windows 로딩 복구 검증

2026-11-10 · 실제 Windows 11 Pro 10.0.26300 · 설치본 0.4.4

macOS에서 발견한 무한 로딩 대기 경로를 Windows 공개 0.4.4 소스로 확인했다. Windows는 `NavigateAsync`가 45초 제한을 적용하고, 실패·취소 시 `Collector.ReleaseReaders()`가 수집용 WebView2를 해제한다. 다음 수집은 새 화면에서 시작하며, macOS의 `isLoading` 해제를 기다리는 분기가 없다.

## 실행 및 결과

- 공개 소스 commit: `845e9149a08b386785be466d14fc47a91d08376c`.
- 실행 코드 26개 파일이 공개 0.4.4와 일치. 실행 코드는 변경하지 않았다.
- SSH로 실제 Windows PC에서 빌드하고, 로그인된 사용자 세션의 임시 예약 작업으로 WebView2 시험을 실행했다.
- 앱의 실제 저장소와 별도 임시 프로필, 로컬 응답을 사용했다.
- Core 84개, 기본 Windows 통합 90개, 복구 37개 검사 통과. 합계 211개.
- 실제 요청 응답을 보류하여 45초 탐색 제한 발생, 로딩 종료, 자동 재시도, 수집 화면 교체 및 오늘 수치 복구를 확인했다.
- 첫 탐색 도중 연결 중단·복구에서도 수동 새로고침 없이 수집을 재개했다.
- 만료일을 지정한 합성 로그인 쿠키를 장애 전후 확인했다. 화면 교체 후 저장형 쿠키가 유지됐다.
- 실제 설치본에서 로그인 상태와 오늘 수치의 자동 갱신을 확인했다.
- 기존 로그인·OTP·초안 보호, 렌더러 실패, 잠자기·복귀, 수집 취소 및 독립 수집 간격 검사 통과.
- 시험 후 임시 예약 작업을 제거했다. 설치된 앱과 사용자 프로필은 유지했다.

## 별도 관찰

- 최초 시험의 만료일 없는 합성 세션 쿠키는 모든 수집 화면을 닫은 뒤 유지되지 않았다. 최종 쿠키 보존 검사는 만료일이 있는 저장형 쿠키로 범위를 명시했다.
- 실제 설치본은 `TwoDays` 게시물 목록에서 `content.error`, `PostsCurrent=false` 상태를 보였다. 오늘 전체 노출은 갱신됐다. 게시물 목록 오류 원인은 미확인이다.
- Windows 실행 코드 변경이나 새 Windows 릴리스는 수행하지 않았다.

## 명령과 테스트

- `dotnet test windows/XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -p:RestoreLockedMode=true`
- `dotnet build windows/XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -p:RestoreLockedMode=true -r win-x64`
- `dotnet XBar.WindowsTests.dll results/windows.json`
- `dotnet XBar.WindowsTests.dll results/recovery-final.json --recovery-only`
- 로딩 복구만 실행하려면 `--loading-only`를 사용한다.

`LoadingRecoveryTests.cs`의 12개 검사를 복구 검사에 포함했다. 원본 결과와 빌드 로그는 로컬 `outputs/windows/loading-audit-20261110/`에 보관했다.
