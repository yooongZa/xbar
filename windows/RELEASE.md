# Windows GitHub 직접 배포

공개 베타 대상은 Windows 11 x64다. 두 위젯, 한국어·영어·일본어, 앱 내 업데이트를 제공한다. macOS 릴리스와 appcast는 독립적으로 유지한다. 현재 개발용 설치물은 일반 사용자 배포 대상이 아니다.

## 두 위젯의 배포 조건

GitHub 직접 배포를 유지한다. 바탕화면 위젯 자체에는 Developer Mode(개발자 모드)가 필요하지 않다. Windows Widgets Board 공급자를 직접 설치하는 경로는 [Microsoft 공식 샘플의 배포 안내](https://learn.microsoft.com/en-us/samples/microsoft/windowsappsdk-samples/widgets/)상 개발자 모드를 요구한다. 공개 코드 서명만으로 이 조건이 해소된다고 가정하지 않는다. 설정의 추가 안내와 세 언어 릴리스 설명에 이 차이를 표시한다.

기존 PC에서 임시 개발자 모드로 추가하고 복구한 뒤 카드가 유지된 결과는 새 PC의 일반 설치 증거를 대신하지 않는다. 새 환경에서 바탕화면 설치·업데이트를 개발자 모드 없이 검증하고, 보드 위젯은 사용자가 선택한 개발자 모드 조건에서 별도로 검증한다. 앱이 시스템 설정을 자동 변경하지 않는다.

## 재현 가능한 소스

`windows/XBar.App`도 반드시 소스 아카이브에 포함한다. 상위 저장소의 macOS `*.app/` 제외 규칙을 `windows/.gitignore`에서 해제했다. 다섯 .NET 프로젝트의 `packages.lock.json`을 추적하며 빌드는 locked restore(잠금 복원)를 사용한다. SDK는 `windows/global.json`을 따른다.

```powershell
# 저장소 루트, 로그인된 Windows 사용자 세션
.\windows\scripts\build.ps1
# UI가 없는 빌드 환경에서는 Core 시험과 publish/build까지만 실행한다.
.\windows\scripts\build.ps1 -SkipUiTests
```

UI 시험에는 Edge WebView2 Runtime이 필요하다. `-SkipUiTests`는 실기기 시험 통과를 의미하지 않는다. UI 시험은 격리된 프로필과 로컬 응답을 사용하며 기존 X 계정을 읽지 않는다. 빌드 결과와 사용자별 증거는 Git에 포함하지 않는다.

## 서명과 업데이트 목록

1. 배포용 코드 서명 인증서 또는 승인된 서명 서비스를 준비하고 최종 Publisher subject(게시자 이름)를 확인한다. macOS Developer ID와 Microsoft Store 서명은 GitHub용 Windows 서명 자격을 대신하지 않는다.
2. 같은 소스로 `LIMOD.XBar` 패키지를 만든다. `Publisher`에는 인증서의 실제 subject를 사용한다.
3. 기존 인증서 저장소/HSM의 키로 서명하고 timestamp(타임스탬프)를 붙인다. 개인키를 저장소에 넣지 않는다.
4. Windows 서명 검증 보고서와 동일한 파일에 대해서만 업데이트 목록을 생성한다.

```powershell
.\windows\scripts\package.ps1 -Version 0.3.0.0 -Identity 'LIMOD.XBar' -Publisher '<certificate subject>'
.\windows\scripts\sign-release.ps1 -Package .\windows\artifacts\XBar-0.3.0.0-x64.msix -CertificateThumbprint '<thumbprint>' -ExpectedPublisher '<certificate subject>'
# 클라우드 서비스에서 서명한 파일도 아래 검증을 통과해야 한다.
.\windows\scripts\verify-release.ps1 -Package .\windows\artifacts\XBar-0.3.0.0-x64.msix -ExpectedPublisher '<certificate subject>'
```

`verify-release.ps1`은 개발 식별자·개발 Publisher, 식별자/아키텍처 불일치, lab 설정, 서명 누락, 신뢰 검증 실패, 자체 서명, 타임스탬프 누락을 차단한다. 인증서나 Windows 보안 설정을 변경하지 않는다. `prepare-update.py`는 공개 후보의 검증 보고서를 파일 해시·길이·버전·게시자와 대조한다. 정확한 명령은 [UPDATES.md](UPDATES.md)를 따른다.

## 게시 순서

- 검증한 소스 commit에서 빌드하고 source archive(소스 압축 파일)·설치 파일·SHA256SUMS·검증 기록을 준비한다.
- `windows-v0.3.0-beta.1`처럼 Windows 전용 태그의 GitHub **draft prerelease(베타 출시 초안)**에 모은다. macOS의 Latest 릴리스는 유지한다.
- 새 Windows 11 환경에서 별도 인증서 등록·개발자 모드 없이 설치 → X 로그인 → 바탕화면 위젯 → 다음 버전 업데이트 → 자동 재시작을 확인한다. 보드 위젯은 위 배포 조건에 따라 별도로 추가·복구를 확인한다.
- 서명과 실기기 검증이 끝난 패키지만 공개한다. 공개 다운로드를 다시 받아 SHA-256을 확인한 뒤 Windows 업데이트 JSON을 게시한다. JSON이 먼저 새 파일을 가리키게 하지 않는다.
- 게시 이후 파일을 교체하지 않는다. 수정은 더 높은 네 자리 MSIX 버전으로 배포한다.

현재 개발판 `LIMOD.XBar.Dev`와 공개판 `LIMOD.XBar`는 다른 식별자다. 개발판은 공개판으로 자동 교체되지 않는다. 개발 사용자 전환 시 기존 로그인·설정 보존과 위젯 재등록을 별도로 확인한다. 기존 데이터나 인증서를 자동 삭제하는 마이그레이션은 구현하지 않는다.

## 서명 수단 확인 결과와 남은 입력

2026-10-27 확인한 IMgest Windows 출시는 Microsoft Store의 서명을 사용했다. 확인한 PC의 사용자/컴퓨터 인증서 저장소와 관련 GitHub 설정에는 X-Bar 직접 배포에 쓸 코드 서명 수단이 없었다. 사용자는 GitHub 직접 배포 유지를 선택했다.

선택 가능한 수단은 공개 신뢰 코드 서명 인증서/HSM 서비스 또는 승인된 오픈소스 서명 서비스다. [Microsoft 서명 안내](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview)를 참고한다. [SignPath Foundation](https://signpath.org/)은 오픈소스에 무료 서명을 제공하지만 [자격 심사와 운영 조건](https://signpath.org/terms)이 있으며 X-Bar의 승인은 아직 받지 않았다. 신청 초안은 `SIGNING-APPLICATION-DRAFT.md`에 있다. 발급·구독·법적 동의·외부 신청 전송은 수행하지 않았다.

공개 서명 성공 경로와 새 PC의 공개 채널 설치/업데이트 시험은 서명 수단 확정 후 완료해야 한다. 개발 인증서를 설치하도록 일반 사용자에게 요구하는 방식으로 이 조건을 대체하지 않는다.
