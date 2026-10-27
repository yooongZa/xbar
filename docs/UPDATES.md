# X-Bar 업데이트

0.5.2 (11)부터 Sparkle 2.9.6으로 업데이트를 확인합니다. 설정에서 **업데이트 확인**을 누르거나 하루 한 번 자동 확인하도록 선택할 수 있습니다. 자동 확인의 초기값은 켜짐이며, 사용자가 변경한 값은 재실행 후에도 유지합니다. 앱이 실행 중일 때 확인하며 잠자기·네트워크 상태에 따라 늦어질 수 있습니다.

백그라운드에서 찾은 새 버전은 설정에 표시합니다. 사용자가 **설치**를 한 번 선택하면 다운로드·설치·재실행까지 이어집니다. 취소나 오류가 발생하면 설치 선택을 초기화합니다. 다음 시도는 새 선택을 받아야 합니다. 자동 설치는 사용하지 않습니다.

최신 배포 파일과 설치 안내는 [다운로드 페이지](https://yooongza.github.io/xbar/)에서 제공합니다. 일반 사용자는 DMG를, 앱 내 업데이트는 ZIP을 사용합니다.

## 구성

| 항목 | 값 |
|---|---|
| 앱 식별자 | `com.limod.xbar` |
| 업데이트 목록 | `https://yooongza.github.io/xbar/appcast.xml` |
| 설치 파일 | GitHub Release의 `X-Bar-VERSION-macOS-universal.zip` |
| 서명 계정 | Keychain(키체인)의 `com.limod.xbar.updates` |
| 공개 검증키 | `app/Info.plist`의 `SUPublicEDKey` |
| 플랫폼 | macOS 14+ / arm64·x86_64 |
| 서명 정책 | ZIP과 appcast 서명 모두 검증, 서명 실패 시 허용으로 전환하지 않음 |
| 개인정보 | X 계정·게시물·로그인 쿠키를 업데이트 요청에 넣지 않음. Sparkle 시스템 프로파일링 비활성화 |

로그인 저장소·App Group(앱 그룹)·앱 식별자는 유지합니다. 업데이트로 앱이 재실행되면 메모리에만 있던 게시물 비교 기록은 일반 재실행과 같이 초기화됩니다.

## 코드

- [AppUpdater.swift](../app/Sources/TodayBar/AppUpdater.swift): 확인 일정, 설정 상태, Sparkle UI와 설치 흐름.
- [UpdateInstallationConsent.swift](../app/Sources/TodayBarCore/UpdateInstallationConsent.swift): 화면별 설치 선택과 취소·오류 초기화. 디스크에 저장하지 않음.
- [AppDelegate.swift](../app/Sources/TodayBar/AppDelegate.swift): 설정 팝업을 닫은 뒤 업데이트 창을 열고, 기존 종료 시 세션 정리 동작 유지.
- [Info.plist](../app/Info.plist), [entitlements](../app/TodayBar.entitlements): HTTPS 주소, 검증키, 샌드박스 서비스 설정.
- [build-local.sh](../app/build-local.sh): 포함된 Sparkle helper(보조 프로그램)부터 바깥 앱 순서로 서명.
- [package-release.py](../scripts/package-release.py): 공증 확인서를 첨부한 앱으로 서명된 ZIP·DMG 생성. 별도 임시 폴더에서 Finder 메타데이터를 정리하고 서명을 다시 검사.
- [prepare-update.py](../scripts/prepare-update.py): 앱 버전·키·파일명을 확인한 뒤 ZIP 서명과 appcast 생성·검증. 업로드 기능은 없음.

## 배포 파일 만들기

Sparkle **2.9.6**의 전체 배포 패키지와 앱의 공개키에 대응하는 기존 Keychain 계정이 필요합니다. 키를 새로 생성하거나 저장소에 내보내지 않습니다. 아래 명령은 프로젝트 루트에서 실행합니다. `SPARKLE_TOOLS_DIR`는 `generate_appcast`, `generate_keys`, `sign_update`가 있는 `bin` 폴더로 지정합니다.

1. `app/project.yml`의 표시 버전과 build 번호를 올리고 `xcodegen generate --spec app/project.yml`로 프로젝트를 재생성합니다. 이미 공개한 버전·build 번호는 재사용하지 않습니다.
2. 테스트와 Developer ID 빌드를 실행합니다.

```sh
swift test --package-path app --scratch-path /private/tmp/xber-core-tests
node --test app/Tests/popup-tests.cjs
python3 app/Tests/run-navigation-recovery.py
python3 app/Tests/run-review-integration.py
TODAYBAR_SIGN_IDENTITY='Developer ID Application: roh yongwook (495S4FVMCB)' \
  zsh app/build-local.sh archive/outputs/X-Bar-OpenSource-0.5.3
```

3. 공개 배포 전 앱 ZIP을 `xcrun notarytool submit`으로 제출하고 승인·공증 로그를 확인합니다. 앱을 임시 폴더에 풀어 `xcrun stapler staple`로 확인서를 첨부합니다. `build-local.sh`는 공증을 자동 수행하지 않습니다.

```sh
python3 scripts/package-release.py "$STAPLED_APP" \
  --output "$RELEASE_OUTPUT" \
  --identity 'Developer ID Application: roh yongwook (495S4FVMCB)'
```

생성된 DMG도 별도로 공증·staple하고, `stapler validate`, `codesign --verify --deep --strict`, `spctl --assess`로 검사합니다. 출력 폴더에는 기존 ZIP·DMG가 없어야 합니다. **앱/ZIP 바이트가 바뀌면 서명된 appcast와 체크섬을 다시 만듭니다.**
4. 최종 ZIP으로 feed(업데이트 목록)를 생성합니다. 기존 appcast 항목은 보존합니다.

```sh
python3 scripts/prepare-update.py \
  archive/outputs/X-Bar-OpenSource-0.5.3/X-Bar-0.5.3-macOS-universal.zip \
  --tools "$SPARKLE_TOOLS_DIR" --feed appcast.xml
```

5. GitHub Release에 최종 ZIP·일반 사용자용 공증 DMG·체크섬·해당 버전의 소스를 먼저 공개하고 ZIP 다운로드를 확인합니다. 그 뒤 루트의 `appcast.xml`과 사이트를 GitHub Pages에 배포합니다. 사이트의 다운로드 버전도 같은 공개 버전으로 맞춥니다.
6. 공개 appcast를 다시 받아 서명을 검증하고, 이전 updater 지원 버전에서 실제 공개 주소로 업데이트합니다. 이 단계까지 성공해야 공개 업데이트 경로가 완료됩니다.

`appcast.xml`은 서명 후 문구·공백을 포함해 수정하면 재서명이 필요합니다. feed만 먼저 공개하면 존재하지 않는 ZIP으로 사용자를 안내하게 됩니다.

## 별도 앱으로 설치 시험

실제 X-Bar와 분리된 `com.limod.xbar.updaterlab20261023` 샌드박스 앱을 사용합니다. 실제 계정 대신 테스트 설정과 `updater-lab.invalid`의 합성 쿠키를 넣습니다. 로컬 HTTP 허용은 이 테스트 앱에만 적용합니다.

1. 임시 폴더를 만들고 `production/`에 빌드 ZIP을 `ditto -x -k`로 풉니다.
2. `python3 app/Tests/serve-updater-lab.py TEST_DIRECTORY`를 별도 터미널에서 실행합니다. `server.json`에 포트와 프로세스 번호가 저장됩니다.
3. 다음 명령으로 설치용 build 1과 업데이트 후보 build 2를 만듭니다. `SPARKLE_DISTRIBUTION`은 `bin/`과 헤더가 포함된 `Sparkle.framework`가 있는 전체 SDK 폴더입니다.

```sh
python3 app/Tests/build-updater-lab.py \
  --directory "$TEST_DIRECTORY" --tools "$SPARKLE_DISTRIBUTION" \
  --identity 'Developer ID Application: roh yongwook (495S4FVMCB)'
```

4. `installed/X-Bar Updater Lab.app`을 열고 새 버전 확인·설치·재실행을 시험합니다. UI의 버전, `Saved preference`, `Synthetic login cookie`를 확인합니다. `--target-build 3 --candidate-only`로 다음 후보만 만들 수 있습니다.
5. `served/.slow-download` 파일은 다운로드를 늦춰 취소를 시험합니다. 서명 후 feed나 ZIP 복사본을 변조해 차단되는지도 확인합니다. 시험 종료 후 해당 테스트 앱과 로컬 서버를 종료합니다.

0.5.4 결과는 [출시 검증 기록](releases/0.5.4.md)에, 이전 검증은 [0.5.3 기록](releases/0.5.3.md)과 [0.5.2 기록](releases/0.5.2.md)에 있습니다. Sparkle 공식 [사용자 인터페이스 확장](https://sparkle-project.org/documentation/customization/)과 [샌드박스 설정](https://sparkle-project.org/documentation/sandboxing/)을 확인했으며, DiskOUT의 명시적 설치 선택 유지 방식을 참고했습니다.
