# Windows 앱 업데이트

0.4부터 사용자 계정용 EXE 설치 프로그램으로 업데이트한다. macOS와 같은 흐름으로 설정에서 수동 확인, 하루 한 번 자동 확인, 나중에·건너뛰기·취소, **설치하고 재시작**을 제공한다. 설치는 사용자가 선택한 경우에만 시작한다. 0.4.2부터 확인에 성공한 시각만 저장하며, 통신 실패 시 재시도 간격을 최대 5분까지 늘려 다시 확인한다. 정상 확인 후에는 하루 주기로 돌아간다.

## 보존과 검증

| 경로 | 처리 |
| --- | --- |
| 중복·취소·이전 요청 | 단일 updater와 화면별 설치 선택, 취소 후 선택 폐기 |
| 업데이트 목록 | 기존 고정 Ed25519 공개키로 검증. 임의 주소·키의 런타임 변경 금지 |
| 다운로드 | HTTPS와 제품 GitHub 경로 제한, 길이·SHA-256 대조 |
| 설치 파일 | EXE 형식, 제품·회사·버전 검사. MSIX와 EXE 업데이트 채널의 포맷 혼용 거부 |
| 실행 중 교체 | 파일 재검증 후 수집 중지, Inno Setup과 Windows Restart Manager로 종료·교체·재시작 |
| 실패 | 설치 제출 전 실패·취소는 수집을 유지하거나 복구하고 설치 선택을 폐기 |
| 계정·설정 | 사용자 데이터 폴더를 교체하거나 삭제하지 않음. 자동 시작 등록 보존 |

설치 EXE는 사용자 결정에 따라 **코드 서명하지 않는다**. Windows 서명 신뢰 검사는 EXE 경로에서 수행하지 않는다. 업데이트 목록의 암호학적 서명은 무료로 유지하며 Windows Authenticode나 SignPath 인증서와는 별개다. 새로운 EXE의 제품·버전은 서명된 목록의 파일 해시와 함께 검증한다.

Windows Restart Manager의 조건 때문에 실행 직후 업데이트하면 준비 단계에서 최대 약 65초 기다릴 수 있다. 이 대기는 취소할 수 있다. 설치 프로그램에는 시스템 재부팅 금지·강제 앱 종료 금지 옵션을 전달한다.

## 목록 준비

고정 주소는 `https://yooongza.github.io/xbar/windows-updates.json`이다. macOS `appcast.xml`을 수정하지 않는다. 기존 macOS Keychain의 업데이트 키를 Sparkle 도구로 사용하며 개인키를 내보내지 않는다.

```sh
python3 windows/scripts/prepare-update.py XBar-0.4.2.0-windows-x64-setup.exe \
  --desktop-version 0.4.2.0 \
  --tools /path/to/Sparkle/bin \
  --feed outputs/windows/windows-updates.json \
  --url https://github.com/yooongZa/xbar/releases/download/windows-v0.4.2-beta.1/XBar-0.4.2.0-windows-x64-setup.exe \
  --notes release-notes.json \
  --verification XBar-0.4.2.0-windows-x64-setup.exe.verification.json
```

보고서의 제품·버전·포맷·해시·길이·무서명 상태가 후보와 다르면 게시 도구가 중단한다. 패키지를 먼저 공개하고 재다운로드로 확인한 뒤 목록을 게시한다. MSIX 개발판으로는 새 EXE가 자동 설치되지 않는다.

## 시험 경계

`-p:UpdaterLab=true`로만 만드는 시험본은 설치 식별자·데이터 폴더·통신 파이프가 분리된다. 일반 프로토콜·자동 시작·시작 메뉴 등록 없이 루프백 업데이트를 시험한다. 일반 패키징은 시험 설정 파일을 거부한다. 실계정 대신 합성 쿠키로 보존을 확인한다.

실기기 설치·실제 공개 업데이트 결과는 해당 버전의 검증 기록에 남긴다. 무서명 앱의 SmartScreen·기업 정책·Smart App Control 동작은 PC마다 다를 수 있으며 앱이 보안 정책을 변경하지 않는다.

공식 근거: [Inno Setup 앱 종료](https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm), [재시작](https://jrsoftware.org/ishelp/topic_setup_restartapplications.htm).
