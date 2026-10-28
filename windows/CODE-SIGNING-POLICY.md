# Code signing policy

X-Bar is maintained by [yooongZa](https://github.com/yooongZa). The maintainer reviews release changes and external contributions.

## Windows desktop releases from 0.4

The Windows desktop EXE installer and X-Bar binaries are **not Authenticode-signed**. The owner chose unsigned GitHub distribution on 2026-10-27. Windows may display SmartScreen warnings or block execution according to device policy. X-Bar does not disable security features, install trust certificates, or change Developer Mode.

The SignPath Foundation application was declined because the project had insufficient public adoption and independent visibility. The SignPath approval monitor is paused. No Foundation certificate, approval or signature is claimed.

## Release controls

- Build the desktop app from a recorded commit with locked dependencies. Do not ship the legacy Widgets Board provider or updater-lab configuration.
- Verify the Inno Setup compiler download against its pinned hash and trusted publisher. Preserve third-party binaries and license notices.
- Check the resulting installer's product, company, version, length, SHA-256 and unsigned status. These checks do not constitute public Windows publisher verification.
- Test installation, replacement, restart and local data preservation. Publish corresponding source and checksums with the GitHub Windows release.
- Keep the update listing authenticated with the existing embedded Ed25519 public key. Validate package format, product/version and file hash before installation. This integrity check does not require a paid code-signing certificate and does not remove Windows reputation warnings.
- Publish the update listing after checking the public installer download. Preserve macOS signing and its independent update channel.

See [Windows privacy](PRIVACY.md), [release procedure](RELEASE.md), and the historical [SignPath preparation](signpath/).
