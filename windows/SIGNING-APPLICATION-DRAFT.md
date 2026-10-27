# 오픈소스 코드 서명 신청 초안

아래는 소유자가 검토하고 직접 제출할 수 있는 정보다. 신청을 전송하지 않았고, 서명 서비스가 승인하거나 인증서를 제공한 상태가 아니다.

- Project: X-Bar
- Repository: https://github.com/yooongZa/xbar
- Maintainer: yooongZa / LIMOD
- License: GPL-3.0-only, without commercial dual licensing
- Target: Windows 11 x64, WPF desktop application and Windows Widgets Board provider, distributed as MSIX
- Languages: C#/.NET, JavaScript, PowerShell, Python build tooling
- Description: A free desktop utility displaying the signed-in user's own X Analytics in a desktop widget and Windows Widgets Board. The app uses a dedicated local WebView2 profile. It does not operate a separate analytics backend.
- Updates: The user chooses installation; an Ed25519-signed release manifest, SHA-256, package identity/version and Windows signature verification protect the update. Preferences and the local sign-in profile are retained.
- Repository/download documentation: `windows/README.md`, `windows/RELEASE.md`, `windows/UPDATES.md`
- Network behavior: X content is loaded after user sign-in. Update checks contact GitHub Pages and GitHub Releases. Updater requests do not include the X account, cookies or analytics values. WebView2 Runtime follows Microsoft's runtime update mechanism.
- Build provenance: Complete source, pinned NuGet dependencies, and separated local test evidence. A verifiable CI build must be configured to the signing provider's requirements before signing requests.
- Release state: macOS is already public. The Windows beta is being prepared; a Windows public release in the target format has not yet been completed.

## 실제 신청서에 맞춘 내용

- Project Name: X-Bar
- Repository URL / Homepage URL: https://github.com/yooongZa/xbar
- Download URL: 비워 둔다. Windows 공개 설치 파일이 아직 없고, SignPath 서명 제공이 승인되지 않았다.
- Privacy Policy URL: https://github.com/yooongZa/xbar/blob/main/windows/PRIVACY.md
- Tagline: A free, open-source desktop companion for viewing your own X Analytics.
- Category: Productivity
- Maintainer Type: Individual maintainer(s)
- Build System: GitHub Actions
- Primary Discovery Channel: AI / LLM tools
- Exact source: OpenAI Codex and the official SignPath Foundation documentation
- Contact: 소유자가 신청에 사용할 영문 성명·이메일을 확인한다. 개인정보를 소스에 추가하지 않는다.

Description:

> X-Bar displays the signed-in user's own X Analytics in compact desktop surfaces. The macOS edition uses a menu bar, and the Windows edition offers a desktop widget and Windows Widgets Board integration. Account sessions and display data stay on the user's device; the project does not operate an analytics backend. The source is GPL-3.0-only.

Reputation:

> This is an early-stage project. Four macOS releases (0.5.1 through 0.5.4) are publicly available at https://github.com/yooongZa/xbar/releases. We do not yet have substantial independent adoption, press coverage, or a publicly installable Windows release; download counters include development and verification downloads. Windows source and validation records are public. A GitHub-hosted Windows source-build workflow is available at https://github.com/yooongZa/xbar/actions/workflows/windows-build.yml. We are requesting an eligibility assessment for free Windows signing and do not claim that the existing macOS releases satisfy the Windows release-history requirement. Code signing policy: https://github.com/yooongZa/xbar/blob/main/windows/CODE-SIGNING-POLICY.md

신청 화면에는 Code of Conduct 동의와 개인정보 저장·처리 동의가 필수로 표시된다. 인증서는 SignPath Foundation 명의이며 조건 위반 시 취소될 수 있다는 문구를 포함한다. 마케팅 수신 동의는 선택 사항이다. 필수 동의와 최종 제출은 소유자 확인을 받은 뒤에만 진행한다. CAPTCHA가 나타나면 소유자가 확인한다. 이 문서는 동의를 대신하지 않는다.

제출자가 확인할 항목: 서비스의 대상 프로젝트·배포 이력 조건, WebView2/Windows SDK 구성 요소의 허용 범위, 저장소와 서명 서비스 MFA, 담당자/검토자/승인자 역할, 심사 승인 후 실제 인증서 subject와 MSIX Publisher 일치. 연락처·법적 동의·계정 입력은 소유자가 작성한다. 승인 전 ‘SignPath가 서명을 제공한다’는 문구를 공개 안내에 넣지 않는다.

신청 및 조건: https://signpath.org/apply · https://signpath.org/terms
