# Windows 0.4.4 beta.1 검증

2026-10-30 · Windows 11 x64 · EXE 0.4.4.0 · 소스 `845e9149a08b386785be466d14fc47a91d08376c`

X 분석의 변경된 달력 구조를 지원하고, 날짜·연도를 확인한 뒤 오늘 노출을 게시한다. 한국어·영어·일본어 fixture(시험 화면)와 실제 X 계정에서 확인했다. Mac과 게시물 판독 코드를 공유하며 구조 변경 오류는 업데이트 안내로 구분한다.

| 확인 항목 | 결과 |
|---|---|
| Core | 84 통과 |
| WebView2·수집·달력 통합 | 90 통과, 달력 24 포함 |
| 복구 | 25 통과 |
| 로그인 | 51 통과 |
| 설정 | 25 통과 |
| 업데이트 | 35 통과, 변조 파일 차단 포함 |
| 바탕화면·브라우저·배치 | 14·3·4 통과 |
| 실제 계정 | 오늘 노출·게시물 5개, 재실행 후 새 수집 확인 |
| 사용자 데이터 | 로그인·설정·업데이트 설정·자동 시작 선택 보존 |
| 격리 업데이트 | 0.4.3.0 → 0.4.4.0 설치·재실행, 설정·합성 세션 보존 |
| GitHub CI | [소스 빌드·테스트·EXE 패키징 성공](https://github.com/yooongZa/xbar/actions/runs/40987686411) |

주요 명령: `dotnet test`, `dotnet publish -p:RestoreLockedMode=true`, `XBar.WindowsTests`, `package-desktop.ps1`, `windows/scripts/prepare-update.py`.

배포 EXE는 기존 방침대로 코드 서명이 없다. App-final 배포 폴더에 시험 프로필을 포함하지 않았으며 파일 버전·길이·SHA-256을 검사했다. ARM64 실기기와 실제 OS 재부팅은 미확인.

[릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.4-beta.1)
