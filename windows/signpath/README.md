# SignPath 무료 서명 준비

2026-10-27 [무료 신청](https://signpath.org/apply)을 제출했고 `Form submitted` 확인 화면을 읽었다. 소유자가 필수 약관·개인정보 처리와 보안 확인에 동의했으며 마케팅 수신은 선택하지 않았다. 현재는 심사 대기 상태다. 승인·인증서 제공·서비스 연동은 아직 완료되지 않았다. 유료 구독, 인증서 구매, GitHub App 권한 부여는 수행하지 않았다. [신청 기록](../SIGNING-APPLICATION-DRAFT.md)에 제출 내용과 남은 확인 항목을 정리했다.

## 현재 실행 가능한 무료 빌드

[Windows source build](../../.github/workflows/windows-build.yml)는 공개 원본 저장소의 표준 `windows-2025` 실행기만 사용한다. 수동 실행으로 잠금 복원 → Core 시험 → 앱/공급자 빌드 → 계정 없는 WPF 화면·updater 시험 → 개발 MSIX 패키징 → 출처·메타데이터 검사를 수행한다. 전체 WebView2 및 새 일반 PC 시험은 별도다.

```sh
gh workflow run windows-build.yml --repo yooongZa/xbar --ref main -f upload_artifact=false
```

캐시를 사용하지 않으며 artifact(산출물) 업로드는 기본 꺼짐이다. 로그와 작업 요약에 소스 commit·시험 수·패키지 SHA-256을 남긴다. 공개 표준 실행기는 무료지만 산출물 저장의 계정별 무료 잔량은 별도로 확인한다. 업로드가 필요할 때만 확인 후 `upload_artifact=true`로 실행하며 보관 기간은 1일이다. 현재 읽기 권한으로 계정의 저장 과금 현황을 확인하지 못해 업로드를 실행하지 않는다. 과금 설정이나 예산을 변경하지 않는다.

### 0.3.1 추가 검증

[실행 40464143815](https://github.com/yooongZa/xbar/actions/runs/40464143815)은 소스 `307ec70f9957558ab1707aa6272d1bb97f51e486`에서 성공했다. Core 76, 화면 4, updater 22, 설정 20, Python 1개가 통과했고 unsigned 패키지 검사를 마쳤다. `upload_artifact=false`이며 공개 서명을 수행하지 않았다. [검사 결과](ci-build-proof-40464143815.json), [실기기 UX 검증](../VALIDATION-0.3.1.md)을 함께 참고한다.

### 이전 검증 완료 기록

- [실행 40458101405](https://github.com/yooongZa/xbar/actions/runs/40458101405): 성공. 소스 `328d575e89e0ef01826781cf6897827606f9a200`, .NET SDK `10.0.114`.
- Core 74개, 화면 배치 4개, updater 22개, Python 배포 도구 1개가 모두 통과했다.
- 개발용 unsigned MSIX 생성과 소스·버전·앱 식별자·자체 바이너리 6개·공유 JavaScript 일치 검증을 통과했다. [로그에서 보존한 검사 결과](ci-build-proof-40458101405.json)를 함께 제공한다.
- `upload_artifact=false`로 실행해 산출물 업로드 단계는 건너뛰었다. 패키지는 해당 실행에서 검사한 개발 산출물이며 공개 설치 파일로 게시하지 않았다.
- 이 실행에는 실제 X 로그인, 전체 WebView2, 새 일반 PC 설치·업데이트 시험이 포함되지 않는다. 공개 서명 패키지로 이 항목을 확인한 뒤 배포한다.

## 승인 후 연결할 항목

1. 제출한 신청의 심사 결과를 확인한다. MFA와 프로젝트 평판·Windows 배포 이력의 적격성은 서비스의 확인이 필요하다. 소스 공개나 CI 성공을 심사 승인으로 표현하지 않는다.
2. SignPath가 지정한 organization/project/policy 값과 실제 certificate subject를 받는다. 인증서 게시자는 SignPath Foundation이므로 임의의 `CN=LIMOD`를 사용하지 않는다.
3. 필요 시 소유자가 SignPath GitHub App의 정확한 접근 범위를 검토해 승인한다. 토큰은 최소 submitter 권한으로 repository secret `SIGNPATH_API_TOKEN`에 설정하고 대화나 소스에 넣지 않는다.
4. 이 폴더의 XML을 서비스에 등록해 검증한다. X-Bar 소유 파일 6개와 바깥 MSIX만 서명하며 Microsoft/.NET/WebView2/NSec 등 타사 파일은 다시 서명하지 않는다. 이 XML은 로컬 구문 확인만 완료한 준비 파일이다.
5. 공개 식별자 `LIMOD.XBar`와 확인된 subject로 **새 hosted build**를 만든다. 로컬 패키지를 업로드해 빌드 출처 검증을 대신하지 않는다. 업로드 저장 범위를 확인하고 해당 실행의 artifact ID를 서명 요청에 사용한다.
6. 유지관리자가 서명 요청을 승인한다. 다운로드한 결과에 `verify-release.ps1`을 실행한 뒤 새 PC 설치·업데이트 시험을 완료한다. 공개 파일 게시와 feed 게시도 별도 단계다.

승인 후 사용할 공식 action 입력은 다음과 같다. 아래는 실행되지 않는 연결 예시이며 실제 값과 서비스 검증이 필요하다.

```yaml
- uses: signpath/github-action-submit-signing-request@f6d04783b4569d051e0c80105fe66e82819d0092 # v3
  with:
    api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
    organization-id: ${{ vars.SIGNPATH_ORGANIZATION_ID }}
    project-slug: ${{ vars.SIGNPATH_PROJECT_SLUG }}
    signing-policy-slug: ${{ vars.SIGNPATH_SIGNING_POLICY_SLUG }}
    artifact-configuration-slug: ${{ vars.SIGNPATH_ARTIFACT_CONFIGURATION_SLUG }}
    github-artifact-id: ${{ steps.unsigned.outputs.artifact-id }}
    wait-for-completion: true
    output-artifact-directory: windows/artifacts/signed
    parameters: |
      version: "0.3.0.0"
```

근거: [GitHub 공개 표준 실행기](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job), [SignPath 빌드 출처 연동](https://docs.signpath.io/trusted-build-systems/github), [MSIX와 메타데이터 설정](https://docs.signpath.io/artifact-configuration/reference).
