# Windows 0.3.0 GitHub 배포 준비 기록

확인일: 2026-10-27. 배포 경로는 GitHub 직접 다운로드로 유지한다. **소스와 베타 출시 초안 준비 완료, 일반 사용자용 설치 파일 공개는 보류** 상태다.

## 검증한 소스와 결과

빌드한 commit은 `6ceec3608d627c8059a497b53e5d4bb15f3091fb`다. `git archive`로 만든 소스를 새 폴더에 풀어 Windows 11 x64 26H2 빌드 `26300.9711`, .NET SDK `10.0.107`에서 확인했다. 원격 빌드 후 UI 시험은 로그인된 사용자 세션에서 실행했다.

| 검사 | 결과 |
| --- | --- |
| 잠금 파일을 사용하는 새 폴더 빌드 | 앱·위젯 공급자·시험 프로그램 성공, 경고 0개 |
| Windows 자동 시험 | Core 74 + 기존 기능 회귀 59 + 업데이트 통합 22 = **155개 통과** |
| 배포 도구 시험 | Python 1개 통과, 검증 보고서 변경·누락 등 거부 조건 19개 확인 |
| 서명 전 MSIX | MakeAppx 검증·ZIP CRC 통과, lab 설정 미포함, 공통 JS가 macOS 원본과 바이트 동일 |
| 공개 배포 차단 조건 | 기존 개발 서명 패키지를 `verify-release.ps1`이 거부함. 공개 인증서 성공 경로는 미시험 |
| 설정 화면 | 한·영·일에 보드 위젯의 개발자 모드 조건 표시, 현재 시스템 테마에서 줄바꿈 확인 |
| 기존 사용자 앱 | 설치 버전 0.3.0.0 유지, Ready·로그인 연결·게시물 3개·Both/Today 확인 |
| 시험 후 환경 | 임시 예약 작업 제거, Developer Mode 미설정 유지 |

새 소스 빌드에서 확인한 runtime target(실행 대상)과 NuGet 잠금 파일 불일치를 수정했다. macOS의 `*.app/` 무시 규칙 때문에 누락되던 `windows/XBar.App/` 소스도 추적한다. 소스 압축 파일에는 Windows 앱과 잠금 파일 5개가 모두 포함되고 사용자 데이터·빌드 산출물·개인키는 포함되지 않는다.

이전 설치물의 실제 MSIX 자체 업데이트·재시작·합성 쿠키 보존, 기존 X 로그인 유지 결과는 [업데이트 검증](VALIDATION-0.3.md)에 별도로 기록했다. 이번 새 폴더의 서명 전 패키지를 설치하거나 공개 업데이트한 결과로 해석하지 않는다. 자동 UI 시험은 격리된 로컬 응답을 사용한다. 강제 테마 시험은 커스텀 색상·배치를 확인하며 Windows 시스템 테마 전환 전체 검증은 별도다.

## 파일과 주요 명령

- 소스: `XBar-0.3.0-beta.1-source.zip`, SHA-256 `c123773c82c88e601f0f69795065c001aad9c3aa214e99d35f2b210fb23497f7`
- 내부 빌드 증거: `XBar-0.3.0.0-x64-DEVELOPMENT-UNSIGNED.msix`, SHA-256 `3fd31665884f4259727934efe70190f845ebe1815147a5e23be6080cf21452fd`. 개발 식별자이며 공개 설치 파일로 첨부하지 않는다.
- 검증 보고서: `XBar-Windows-0.3.0-beta.1-validation.json`. 로컬 증거는 `outputs/windows/beta-0.3.0/`에 보관한다.
- 실행: `git archive`, `build.ps1 -SkipUiTests`, 로그인 세션의 `dotnet XBar.WindowsTests.dll` 및 `--updates-only`, `package.ps1 -Version 0.3.0.0`, `python3 -m unittest discover -s windows/scripts -p 'test_*.py'`.

## 공개 전 남은 조건

1. **공개 신뢰 코드 서명 확보.** 확인한 IMgest 설치물은 Microsoft Store 서명을 사용한다. 관련 인증서 저장소·GitHub 설정에서 GitHub 직접 배포에 재사용할 수단을 찾지 못했다. [SignPath 신청 초안](SIGNING-APPLICATION-DRAFT.md)은 준비했으며 제출·승인·유료 계약은 수행하지 않았다.
2. **새 일반 Windows 11 환경 검증.** 공개 Publisher로 다시 패키징·서명한 뒤 별도 인증서 등록과 개발자 모드 없이 설치 → 로그인 → 바탕화면 위젯 → 공개 채널 업데이트 → 재시작을 확인해야 한다. 현재 26H2 시험 PC는 이 조건을 충족하는 새 환경이 아니다.
3. **보드 위젯 별도 조건.** [Microsoft 공식 배포 안내](https://learn.microsoft.com/en-us/samples/microsoft/windowsappsdk-samples/widgets/)상 직접 설치한 Widgets Board 공급자에는 Developer Mode가 필요하다. 바탕화면 위젯 자체에는 필요하지 않다. 공개 서명만으로 보드 등록 제약이 해소된다고 가정하지 않는다.

ARM64, 장시간 실행, 물리 절전/복귀, 다중 모니터 전체 조합은 미검증이다. 개발판과 공개판의 식별자가 달라 기존 개발 사용자의 로그인·설정 전환도 별도 검증 대상이다. 공개 설치 파일을 확인한 후에만 Windows 업데이트 목록을 게시한다. macOS Latest·appcast·설치 파일은 이 작업에서 변경하지 않는다.
