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

제출자가 확인할 항목: 서비스의 대상 프로젝트·배포 이력 조건, WebView2/Windows SDK 구성 요소의 허용 범위, 저장소와 서명 서비스 MFA, 담당자/검토자/승인자 역할, 심사 승인 후 실제 인증서 subject와 MSIX Publisher 일치. 연락처·법적 동의·계정 입력은 소유자가 작성한다. 승인 전 ‘SignPath가 서명을 제공한다’는 문구를 공개 안내에 넣지 않는다.

신청 및 조건: https://signpath.org/apply · https://signpath.org/terms
