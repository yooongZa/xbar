# Windows 0.4.5 beta.1 검증

2026-11-13 · Windows 11 x64 · EXE 0.4.5.0

게시글 노출 급증을 기준으로 무지개를 표시하도록 Mac과 동일한 판정을 적용했다. 재게시 수와 무관하게 감지하며, 축약 숫자의 연속 증가와 30분 반복 제한을 처리한다.

- 소스: `61fa4c5e83acb610d2da75e65816657965912a5e`.
- [Windows CI](https://github.com/yooongZa/xbar/actions/runs/43424229862): 성공.
- Core 100개, 잠금 의존성 복원·전체 앱 빌드 통과.
- Windows 달력·화면 배치·업데이트 UI·설정·바탕화면 배포·릴리스 도구 검사 통과.
- 설치 EXE의 제품·버전·길이·SHA-256 검증 통과. 기존 코드 서명 없는 베타 배포 방식을 유지했다.
- 업데이트 JSON은 기존 고정 키로 서명·검증했다.
- 공개 EXE·소스·검증 파일을 다시 다운로드해 SHA-256을 대조했다. 공개 업데이트 JSON과 다운로드 페이지의 0.4.5 배포를 확인했다.
- 실제 사용자 Windows 세션의 설치·공개 업데이트는 미확인. 테스트 PC에 연결되지 않아 CI의 격리된 화면·업데이트 검증을 수행했다.

주요 명령: `dotnet test`, `windows/scripts/build.ps1`, `package-desktop.ps1`, `prepare-update.py`, `gh workflow run`, `gh release`.

[GitHub 릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.5-beta.1)
