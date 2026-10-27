# SignPath 무료 서명 준비

2026-10-27 현재 신청·승인·서비스 연동은 완료되지 않았다. 유료 구독, 인증서 구매, GitHub App 권한 부여를 수행하지 않는다. [신청 화면](https://signpath.org/apply)의 프로젝트 설명·평판·연락처·약관 항목을 확인하고 [신청 초안](../SIGNING-APPLICATION-DRAFT.md)을 준비한다.

## 현재 실행 가능한 무료 빌드

[Windows source build](../../.github/workflows/windows-build.yml)는 공개 원본 저장소의 표준 `windows-2025` 실행기만 사용한다. 수동 실행으로 잠금 복원 → Core 시험 → 앱/공급자 빌드 → 계정 없는 WPF 화면·updater 시험 → 개발 MSIX 패키징 → 출처·메타데이터 검사를 수행한다. 전체 WebView2 및 새 일반 PC 시험은 별도다.

```sh
gh workflow run windows-build.yml --repo yooongZa/xbar --ref main -f upload_artifact=false
```

캐시를 사용하지 않으며 artifact(산출물) 업로드는 기본 꺼짐이다. 로그와 작업 요약에 소스 commit·시험 수·패키지 SHA-256을 남긴다. 공개 표준 실행기는 무료지만 산출물 저장의 계정별 무료 잔량은 별도로 확인한다. 업로드가 필요할 때만 확인 후 `upload_artifact=true`로 실행하며 보관 기간은 1일이다. 현재 읽기 권한으로 계정의 저장 과금 현황을 확인하지 못해 업로드를 실행하지 않는다. 과금 설정이나 예산을 변경하지 않는다.

## 승인 후 연결할 항목

1. 소유자가 연락처·약관·개인정보 처리 동의를 확인해 무료 신청을 제출한다. MFA와 프로젝트 평판·Windows 배포 이력의 적격성은 서비스의 확인이 필요하다. 소스 공개나 CI 성공을 심사 승인으로 표현하지 않는다.
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
