# Windows 바탕화면 위젯 GitHub 배포

2026-10-27 사용자가 **바탕화면 위젯 전용·코드 서명 없는 GitHub 배포**를 선택했다. 0.4 EXE 배포에는 SignPath 승인이나 공인 코드 서명 인증서를 요구하지 않는다. 이전 MSIX 서명 절차는 이전 포맷에만 적용한다. macOS 릴리스·Latest·appcast는 독립적으로 유지한다.

## 설치 파일

- .NET self-contained(런타임 포함) Windows x64 본앱만 포함한다. 보드 공급자·COM 등록·MSIX 매니페스트·시험 프로필 설정을 포함하지 않는다.
- Inno Setup 6.7.3의 해시와 게시자를 검증해 빌드 도구를 준비한다. 프로젝트의 배포 EXE는 서명하지 않는다.
- 사용자별 `LocalAppData\Programs\X-Bar`에 설치하고 시작 메뉴·`xbar:` 링크를 등록한다. 자동 시작은 앱 설정에서 사용자가 선택한다. 설치·업데이트가 자동 시작 선택을 덮지 않는다.
- 기존 `%LOCALAPPDATA%\XBar` 로그인·설정을 유지한다. 제거 시 사용자 데이터는 남긴다. 이전 개발 MSIX에서 옮길 때는 실제 데이터 경로와 보존을 확인한 뒤 이전 앱·보드 등록을 정리한다.
- 코드 서명이 없으므로 SmartScreen 또는 보안 정책의 경고·차단 가능성을 다운로드 페이지에 표시한다. 보안 기능을 끄거나 인증서를 등록하는 설치 동작을 넣지 않는다.

## 공개 조건

1. 기록한 소스 commit으로 잠금 복원, Core·Windows·로그인·설정·업데이트 시험을 통과한다. 무료 공개 표준 GitHub 실행기만 쓰며 유료 서비스를 연결하지 않는다.
2. `package-desktop.ps1`로 EXE를 만들고 파일 버전·제품·회사·무서명 상태와 SHA-256·길이를 보고서에 기록한다. `UpdaterLab.json`이 포함되면 중단한다.
3. 로그인된 실제 Windows 세션에서 설치·위젯·X 세션·자동 실행 설정을 확인한다. 격리된 업데이트 시험에서 새 EXE 교체·재시작·설정과 합성 세션 보존, 변조 파일 차단을 확인한다.
4. Windows 전용 GitHub 베타에 설치 EXE, 정확한 소스 압축 파일, 체크섬·검증 기록을 게시한다. macOS Latest를 바꾸지 않는다. 공개 파일을 다시 내려받아 해시를 대조한다.
5. 검증한 파일을 가리키는 Windows 업데이트 JSON을 마지막에 게시한다. 실제 공개 주소에서 앱 내 업데이트를 확인한다. 실패하면 목록을 성공으로 보고하지 말고 원인과 복구 상태를 기록한다.

```powershell
.\windows\scripts\build.ps1
.\windows\scripts\package-desktop.ps1 -Version 0.4.3.0 -ISCC 'C:\path\to\ISCC.exe'
```

`verify-release.ps1`·`sign-release.ps1`·`package.ps1`은 과거 MSIX 경로의 도구다. 새 EXE에 적용해 서명을 요구하지 않는다. 기존 공개 파일을 덮어쓰지 않으며 변경 시 버전을 올린다.
