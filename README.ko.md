# 𝕏-Bar

| Mac | Windows |
| --- | --- |
| **[Mac 다운로드](https://github.com/yooongZa/xbar/releases/download/v0.5.4/X-Bar-0.5.4-macOS-universal.dmg)** | **[Windows 다운로드](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.2-beta.1/XBar-0.4.2.0-windows-x64-setup.exe)** |
| 0.5.4 · macOS 14+ · Apple Silicon / Intel | 0.4.2 beta.1 · Windows 11 x64 |
| DMG · Signed & notarized | EXE · Unsigned installer |

Windows는 베타이며 코드 서명 없는 EXE를 제공합니다. SmartScreen 경고 또는 기기 정책에 따른 실행 제한이 있을 수 있습니다. [설치 안내](windows/README.md).

[한국어](README.ko.md) · [English](README.en.md) · [日本語](README.ja.md)

**메뉴바에서 내 글의 반응을 한눈에.**

글을 올린 뒤 반응이 궁금할 때, Mac 위쪽에서 가볍게 확인하세요. X-Bar는 오늘 노출 수와 인기 게시물을 메뉴바에 보여주는 무료 오픈소스 앱입니다.

**[제작자 @yongZa_R 팔로우 ↗](https://x.com/yongZa_R)**

macOS 14 이상 · Apple Silicon 및 Intel · 한국어 / English / 日本語

## Mac 설치하고 시작하기

1. 상단의 **Mac 다운로드**를 누릅니다. GitHub 가입은 필요 없습니다.
2. 받은 DMG를 열고 **X-Bar를 응용 프로그램으로 드래그**합니다.
3. 응용 프로그램에서 X-Bar를 실행합니다. 이미 설치한 버전이 있다면 먼저 종료해 주세요.
4. **Safari로 연결** 또는 **앱에서 직접 로그인**을 선택합니다.
5. 안내에 계정명과 오늘 노출 수가 표시되면 준비가 끝납니다. 메뉴바 숫자를 눌러 보세요.

공식 배포 파일은 Developer ID로 서명하고 Apple 공증을 거쳐 제공합니다. 첫 실행 때 macOS가 실행 여부를 확인할 수 있습니다. 보안 설정을 끄거나 터미널 명령을 입력할 필요는 없습니다.

### Safari로 연결

Safari에서 평소 쓰던 로그인과 저장된 암호를 사용할 수 있습니다. Safari 설정 → 확장 프로그램에서 X-Bar를 켜고 x.com 접근을 허용한 다음, X에 로그인하세요. 도구 막대의 X-Bar 확장에서 안내를 확인하고 **X-Bar에 연결**을 누릅니다. 연결을 마치면 Safari를 종료해도 X-Bar가 실행 중인 동안 갱신을 시도합니다.

### 앱에서 직접 로그인

앱 안의 X 화면에서 이메일이나 사용자 아이디로 로그인합니다. 이 경로의 Google 로그인은 지원하지 않습니다. X 분석 화면에 접근할 수 있는 계정이 필요하며, X가 추가 인증을 요청할 수 있습니다.

## 무엇을 볼 수 있나요?

- 계정 전체의 **오늘 노출 수**를 메뉴바에서 확인합니다.
- **오늘 / 최근 2일 / 최근 7일**에 게시한 글 중 앱이 확인한 상위 5개와 노출·좋아요·재게시 수를 봅니다.
- 글을 누르면 해당 글의 분석 화면이 열립니다.
- 재게시가 빠르게 늘면 작은 무지개 효과가 나타납니다. 설정에서 끌 수 있습니다.

기간 선택은 **게시일** 기준입니다. 글별 수치는 게시 후 누적 값이며, 선택한 기간에 발생한 노출만을 뜻하지 않습니다. 오늘 총노출에는 이전에 게시한 글도 포함됩니다. X-Bar가 확인한 목록의 순위이므로 계정의 모든 글을 포괄한다고 보장하지 않습니다.

## 알아두면 좋아요

오늘 총노출은 기본 10분 간격으로 확인하며, 설정에서 5·10·30분을 선택할 수 있습니다. 게시물 목록은 약 10분 간격으로 확인합니다. 네트워크 상태·Mac 잠자기·X의 응답에 따라 지연될 수 있고, 새 게시물은 X 분석에 늦게 나타날 수 있습니다. 앱이 종료되면 갱신도 멈춥니다. 웹 화면 구조가 바뀌면 앱 업데이트가 필요할 수 있습니다.

‘최근 확인 이후’는 앱이 글을 처음 확인한 값을 기준으로 그 뒤의 증가량을 비교하는 기능입니다. 하루 전체 증가량과는 다릅니다. 앱 재시작이나 계정·날짜·기간 변경 등으로 기준이 초기화됩니다.

0.5.2부터 설정의 **업데이트 확인**으로 새 버전을 받을 수 있습니다. 하루 한 번 자동으로 확인하며 설정에서 끌 수 있습니다. **설치**를 선택하면 다운로드·설치·재실행이 이어집니다.

### Mac 로그인 시 자동 실행

0.5.4부터 처음 실행할 때 자동 실행을 등록합니다. 메뉴바 → 설정 → ‘Mac 로그인 시 자동 실행’에서 켜거나 끌 수 있습니다. macOS에서 허용이 필요하면 ‘로그인 항목 설정 열기’를 눌러 X-Bar를 허용하세요. 꺼둔 선택은 앱을 다시 실행해도 유지됩니다.

## 개인정보와 지원

계정명·게시물·지표를 개발자 서버로 보내지 않습니다. 로그인 세션은 이 Mac의 앱 전용 저장소에 유지됩니다. Safari 연결은 사용자의 확인을 거쳐 x.com 로그인 쿠키를 로컬 앱으로 전달합니다. X 웹사이트와의 통신에는 X의 개인정보처리방침이 적용됩니다.

[사용 안내](https://yooongza.github.io/xbar/support.html) · [개인정보처리방침](https://yooongza.github.io/xbar/privacy.html) · [이메일 문의](mailto:sukmack@gmail.com)

유용하게 쓰셨다면 **[제작자 @yongZa_R를 팔로우](https://x.com/yongZa_R)**해 주세요. 앱 소식과 만드는 과정을 전합니다. 팔로우는 선택입니다.

## 소스에서 빌드하기

macOS 14 이상, Swift 6를 지원하는 Xcode와 Command Line Tools가 필요합니다. Safari 확장을 포함한 전체 앱은 Xcode 프로젝트로 빌드합니다. Swift Package는 핵심 로직 테스트용입니다. Node.js는 확장 테스트에 사용합니다.

```sh
git clone https://github.com/yooongZa/xbar.git
cd xbar
swift test --package-path app --scratch-path /private/tmp/xbar-core-tests
node --test app/Tests/popup-tests.cjs
zsh app/build-local.sh /private/tmp/xbar-build
```


기본 스크립트는 개발용 ad hoc 서명을 사용합니다. 배포용 공증을 수행하지 않습니다. 직접 빌드한 Safari 확장은 자신의 Apple 개발자 팀으로 서명하고 Bundle ID와 App Group 설정을 맞춰야 합니다. 아래 파일의 값을 함께 변경하세요.

- `app/project.yml`의 팀·앱·확장 식별자
- `app/TodayBar.entitlements`와 `app/SafariExtension/SafariExtension.entitlements`의 App Group
- `app/Shared/TransferFiles.swift`의 동일한 App Group

XcodeGen을 설치한 뒤 `cd app && xcodegen generate`로 프로젝트를 다시 만드세요. 자체 배포 시 Developer ID 서명과 Apple 공증은 별도로 수행해야 합니다. `TODAYBAR_SIGN_IDENTITY` 환경 변수로 빌드 스크립트의 서명 인증서를 지정할 수 있습니다.

## 라이선스와 이용 범위

프로젝트 코드는 [GPL-3.0-only](LICENSE)입니다. 파생 프로그램을 배포할 때는 해당 소스 제공을 포함한 GPLv3 조건을 따라야 합니다. X-Bar는 X와 제휴한 공식 앱이 아닙니다. 라이선스는 X 서비스·상표·화면 속 제3자 콘텐츠에 대한 이용 권한을 부여하지 않습니다. 사용과 배포 시 [X 이용약관](https://x.com/en/tos)을 별도로 확인하세요. X의 서면 승인을 받은 프로젝트는 아닙니다.
