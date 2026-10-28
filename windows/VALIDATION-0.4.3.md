# Windows 0.4.3 beta.1 검증

2026-10-28 KST. Windows 최초 계정 연결, Google 안내, Apple 인증 창을 개선했다.

- 배포 소스: `433b30b3af482f473ac8873a88517eb77cce5076`. 해당 커밋을 추출한 별도 Windows 폴더에서 잠금 복원·빌드·시험·패키징했다.
- [Windows 0.4.3 beta.1 릴리스](https://github.com/yooongZa/xbar/releases/tag/windows-v0.4.3-beta.1), 설치 버전 `0.4.3.0`.
- [GitHub 빌드](https://github.com/yooongZa/xbar/actions/runs/40638827192): 통과. 일반 공개 실행기, 산출물 업로드 없음.
- 사용자별 EXE 배포이며 Windows 코드 서명은 없다. macOS 배포와 독립된 Windows 베타다.

## 변경

- 처음 실행할 때 계정 연결 안내와 로그인 버튼을 바로 표시한다. 기존 연결 사용자는 위젯 설정 탭으로 진입한다.
- Apple 새 창에 같은 WebView2 환경의 창을 연결하여 `window.opener`와 인증 결과 전달을 유지한다. 취소·로그아웃·종료 시 인증 창을 정리하며, 인증 창이 열리거나 생성되는 동안 로그인 완료를 보류한다.
- Google 버튼을 누르면 X 이메일·사용자 아이디 로그인 방법을 안내한다. 입력하던 X 화면과 내용은 유지한다. 앱 내 Google 직접 인증은 지원하지 않는다.

## 검사

| 검사 | 통과 |
| --- | ---: |
| Core | 84 |
| 로그인·팝업 | 51 |
| 설정 | 25 |
| 브라우저 표시 | 3 |
| 수집 복구 | 25 |
| Windows 수집·화면 | 66 |
| 업데이트 | 35 |
| 바탕화면·자동 시작 | 14 |
| 레이아웃 | 4 |
| 배포 도구 | 2 |
| 합계 | **309** |

빌드 오류·경고 0개. Windows UI 시험은 로그인된 데스크톱 세션의 별도 프로필에서 실행했다. 같은 로그인 구현의 실제 X·Apple 페이지 진입과 화면 캡처는 [로그인 수정 검증](VALIDATION-LOGIN-20261028.md)에 기록했다. Apple 이메일/전화번호 입력 폼, Google 안내, 최초 계정 연결 화면과 취소 후 복귀를 확인했다.

## 설치 파일과 업데이트

Inno Setup 네이티브 컴파일러로 설치 파일을 생성했다. PowerShell 파일 실행 제한과 Windows 보안 설정은 변경하지 않았다. 제품·회사·버전·무서명 상태, 파일 길이·SHA-256과 시험 설정 제외를 확인했다.

| 파일 | SHA-256 |
| --- | --- |
| XBar-0.4.3.0-windows-x64-setup.exe (50,751,238바이트) | `4b27879338d1cc55598012b4f5d7f0e6cd4951fbd9d354079f9e32795d2516df` |
| XBar-0.4.3-beta.1-source.zip | `65db2862edab4018c113c401ec3d749a7534238d968c3137c895f3f7a8784b95` |

격리된 앱에서 **0.4.2.0 → 0.4.3.0 업데이트·자동 재시작**을 확인했다. 언어·수집 주기·표시·자동 업데이트·건너뛰기 설정과 합성 로그인 쿠키를 보존했고 설치 완료 기록이 정리됐다. 일반 배포판과 시험 앱의 설치 위치·데이터·통신 파이프는 분리된다. 업데이트 목록은 기존 고정 키로 서명하고 로컬에서 검증했다.

## 실행과 범위

`dotnet test` / `dotnet publish` / `dotnet build`에 `RestoreLockedMode=true`를 적용했다. Windows 시험 실행 파일의 기본 검사와 `--login-only`, `--settings-only`, `--browser-only`, `--recovery-only`, `--updates-only`, `--desktop-only`, `--layout-only`, 배포 도구의 `python3 -m unittest`를 실행했다. 원본 결과는 `outputs/windows/release-0.4.3/`에 보관한다.

한 대의 Windows 11 x64 PC에서 검증했다. 실계정 비밀번호·2단계 인증을 입력한 Apple 인증 최종 완료, ARM64·다른 PC·장기 실행은 미검증이다. 개인 계정 화면·로그인 데이터·시험 설치 파일은 공개 릴리스에 포함하지 않는다.

## 공개 확인

- 공개 GitHub 주소에서 인증 없이 설치 EXE와 소스 ZIP을 다시 내려받았으며 원본과 SHA-256이 일치했다.
- 한국어·영어·일본어 홈페이지와 `windows-updates.json`을 공개 주소에서 읽어 게시 원본과 바이트 단위로 대조했다. 두 운영체제 다운로드의 같은 위계를 유지했다.
- 실제 설치된 0.4.2 앱에서 공개 목록을 읽어 **X-Bar 0.4.3 / 설치하고 재시작** 안내가 표시됨을 확인했다. 이 확인에서는 설치를 요청하지 않았다. 확인 도중 설정이 유지됐고 로그인·수집 상태는 `Ready`였다.
- macOS Latest는 `v0.5.4`이며 macOS 앱과 appcast를 변경하지 않았다.
- 시험 앱·프로필·예약 작업·루프백 서버와 SSH 시험 연결을 정리했다.
- 공개 검증 결과는 릴리스의 `XBar-Windows-0.4.3-beta.1-public-verification.json`에 첨부했다.
