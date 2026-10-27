# Code signing policy

X-Bar is maintained by [yooongZa](https://github.com/yooongZa). The maintainer reviews release changes and approves signing requests. External contributions require maintainer review. Release maintainers must use MFA on the source host and signing service; account configuration is checked before signing is enabled.

## Current status

Windows public code signing is **not yet available**. We are preparing an application to the SignPath Foundation free open-source program. No application approval, certificate access, or successful SignPath signature is claimed. Local development signatures and unsigned CI packages are test evidence and are not public installers.

## Release controls

- Build from a recorded source commit using the checked-in dependency locks. The [Windows workflow](../.github/workflows/windows-build.yml) uses GitHub-hosted Windows runners.
- Sign only X-Bar's own binaries and the containing MSIX. Preserve third-party binaries and their notices.
- Match the MSIX publisher to the approved certificate subject. Verify the completed package's Windows trust, timestamp, identity, version and digest before publication.
- Require the maintainer's manual approval for each signing request. Keep signing credentials in the service or repository secret store.
- Publish the corresponding source and checksums. Publish the signed update listing only after checking the public package download.

The proposed provider configuration is in [signpath](signpath/). Service-side approval and validation remain pending. If the Foundation accepts the application, we will add its required attribution and confirmed publisher details before distributing signed installers.

See [Windows privacy](PRIVACY.md), [release procedure](RELEASE.md), and [SignPath conditions](https://signpath.org/terms).
