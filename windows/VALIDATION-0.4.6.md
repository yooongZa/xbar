# Windows 0.4.6 검증

2026-11-15 · 소스 `2bae5f0ad9c1296aaa648b76fd655c4c0fbcf4de`

업데이트 파일의 다운로드와 길이·SHA-256 검증이 끝나면 업데이트 횟수를 기록한다. 같은 설치·대상 버전은 한 번만 기록하며, 전송 실패는 업데이트 설치를 막지 않는다. 다음 앱 시작에서 미전송 이벤트를 재시도한다. X 계정·게시물·쿠키는 수집하지 않는다.

- 잠금 의존성 복원, Core 101개 통과, Windows x64 Release 빌드 경고·오류 0개.
- 실제 로그인된 Windows 세션의 통합 시험 259개 통과: 수집 복구 37, 업데이트 35, 캘린더·수집 90, 로그인 51, 설정 25, 배포·시작 등록 14, 브라우저 3, 레이아웃 4.
- 업데이트 목록 생성 도구 시험 2개 통과. 기존 키로 업데이트 JSON 서명·검증.
- Inno Setup 6.7.3 설치 파일의 버전·제품·회사·무서명 상태·SHA-256·크기 검사 통과. 런타임 포함 x64 EXE이며 UpdaterLab 설정 없음.
- 실제 PC의 0.4.4.0을 앱의 정상 종료 명령과 Inno Setup CLI로 0.4.6.0으로 교체·재실행했다. 로그인된 노출 수집, 설정, 자동 시작 선택, 업데이트 자동 확인·건너뛰기 선택, 설치된 바이너리 일치를 확인했다.
- 공개 EXE 재다운로드 SHA-256 일치: `e1a9765899564a617b2293d501cf3f311360a5c8cd5508913db620de58c0ba8f`.

실제 공개 updater의 설치 버튼 클릭은 이번 검증에서 수행하지 않았다. 업데이트 대화상자·취소·서명 오류·재실행 상태는 격리된 통합 시험으로 확인했다.

수집은 0.4.6에서 이후 업데이트를 내려받을 때부터 시작한다. 이전 버전의 업데이트 다운로드는 소급 수집하지 않는다.

주요 명령: `build.ps1 -SkipUiTests`, `XBar.WindowsTests.dll`, `package-desktop.ps1`, `prepare-update.py`, `gh release`.

[공개 릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.6-beta.1) · [검증 기록](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.6-beta.1/X-Bar-Windows-0.4.6-verification.json)
