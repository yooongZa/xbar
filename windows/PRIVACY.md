# X-Bar Windows privacy

Updated: 2026-10-27. This document describes the Windows implementation. The macOS policy is available on the [project website](https://yooongza.github.io/xbar/privacy-en.html).

## X account and local data

X-Bar displays the signed-in user's own X Analytics pages. Sign-in takes place in Microsoft WebView2 with an app-specific local profile. The developer does not receive your password, cookies, posts or analytics values and does not operate an analytics backend. X pages and their loaded resources are subject to [X's privacy policy](https://x.com/en/privacy).

The app stores preferences, the latest collected results and its browser profile on the current Windows user's device. It does not build a server-side history of your metrics. The Widgets Board provider receives the display snapshot, not the browser cookies. Diagnostic logging records exception types, error codes and method names rather than page bodies or credentials. X-Bar contains no advertising or developer analytics SDK.

## Updates and third-party services

Automatic app update checks are enabled by default and run at most once per 24 hours when due; manual checks are also available. Checks use GitHub Pages and selected downloads use GitHub Releases. Update requests do not contain X credentials or analytics values. GitHub receives ordinary connection information such as IP addresses and HTTP headers under the [GitHub privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement). Turn off automatic checks in Settings. Installing an app update requires your choice.

WebView2 loads X content and its runtime is serviced by Microsoft. Windows Widgets Board and other operating-system services have their own settings and policies. See the [Microsoft privacy statement](https://privacy.microsoft.com/en-us/privacystatement).

## Disconnect and uninstall

To remove the app's X connection, choose Log out inside X-Bar. The app cancels collection, clears displayed account data and removes its dedicated browser profile; interrupted cleanup is retried before reconnecting. Then exit X-Bar and uninstall it from Windows Settings → Apps → Installed apps. Remove any pinned X-Bar card from Widgets Board if it remains visible. The app does not change Developer Mode or certificate trust automatically.

Contact: [project maintainer](https://github.com/yooongZa), using the public contact details on the project website. Do not post passwords, session cookies or private analytics in public issues.
