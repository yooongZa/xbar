# 𝕏-Bar

| Mac | Windows |
| --- | --- |
| **[Download for Mac](https://github.com/yooongZa/xbar/releases/download/v0.5.4/X-Bar-0.5.4-macOS-universal.dmg)** | **[Download for Windows](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.2-beta.1/XBar-0.4.2.0-windows-x64-setup.exe)** |
| 0.5.4 · macOS 14+ · Apple Silicon / Intel | 0.4.2 beta.1 · Windows 11 x64 |
| DMG · Signed & notarized | EXE · Unsigned installer |

Windows is a beta with an unsigned EXE. SmartScreen may warn, or device policy may block it. [Installation guide](windows/README.md).

[한국어](README.ko.md) · [English](README.en.md) · [日本語](README.ja.md)

**Your posts’ performance, right in the Mac menu bar.**

Curious how your posts are doing? Take a quick look, then get back to your day. X-Bar is a free, open-source Mac app for today’s impressions and your top posts.

**[Follow the creator @yongZa_R ↗](https://x.com/yongZa_R)**

macOS 14+ · Apple Silicon & Intel · 한국어 / English / 日本語

## Install and get started on Mac

1. Choose **Download for Mac** at the top of this page. No GitHub account is required.
2. Open the downloaded DMG and **drag X-Bar into Applications**.
3. Launch X-Bar from Applications. Quit any previously installed version first.
4. Choose **Connect with Safari** or **Sign in within the app**.
5. Setup completes once your account and today’s impressions are verified. Click the menu bar number to explore.

Official downloads are signed with Developer ID and notarized by Apple. macOS may ask you to confirm the first launch. You do not need to disable security settings or enter terminal commands.

### Connect with Safari

Use your familiar Safari sign-in and saved passwords. Enable X-Bar in Safari Settings → Extensions and allow access to x.com, then sign in to X. Open X-Bar in Safari’s toolbar, review the explanation, and choose **Connect to X-Bar**. After connecting, X-Bar can attempt updates while Safari is closed, as long as X-Bar remains running.

### Sign in within the app

Use your email or username on the X page inside X-Bar. Google sign-in is not supported through this route. Your account must have access to X Analytics, and X may require additional verification.

## What you can see

- Your account’s **total impressions today**, in the menu bar.
- Up to five top posts observed by the app, published **Today / Last 2 days / Last 7 days**, with impressions, likes, and reposts.
- A post’s analytics page when you click its row.
- A small rainbow effect when repost activity rises quickly. You can turn it off in Settings.

The period filter refers to the **publication date**. Per-post numbers are lifetime totals, not impressions earned only within the selected period. Today’s account total can include older posts. Rankings cover posts the app has observed and are not guaranteed to include every post on the account.

## Good to know

Today’s total is checked every 10 minutes by default; Settings offers 5, 10, or 30 minutes. Posts are checked roughly every 10 minutes. Network conditions, sleep, and X’s response can delay updates. New posts may take time to appear in X Analytics. Updates stop when X-Bar quits. Changes to X’s web interface may require an app update.

“Since first checked” compares a post’s current number with the first value observed by the app. It is not a full-day total. Restarting the app or changing the account, date, or selected period can reset the baseline.

Starting with 0.5.2, use **Check for Updates** in Settings. X-Bar checks once a day unless you turn off automatic checks. Choosing **Install** continues through download, installation, and relaunch.

### Launch at login

Starting with 0.5.4, X-Bar registers to launch at login the first time you run it. Use Settings → Launch at login to turn it on or off. If macOS requires approval, choose Open Login Items Settings and allow X-Bar. Turning it off is remembered when you reopen the app.

## Privacy and help

Account names, posts, and metrics are not sent to the developer’s servers. Sign-in sessions remain in the app’s local storage on this Mac. Safari connection transfers x.com sign-in cookies to the local app after your confirmation. Communication with X is subject to X’s privacy policy.

[Help](https://yooongza.github.io/xbar/support-en.html) · [Privacy](https://yooongza.github.io/xbar/privacy-en.html) · [Email support](mailto:sukmack@gmail.com)

Enjoying X-Bar? **[Follow @yongZa_R](https://x.com/yongZa_R)** for app news and work in progress. Following is optional.

## Build from source

Requires macOS 14+, Xcode with Swift 6 support, and Command Line Tools. The Xcode project builds the complete app and Safari extension. The Swift package contains core logic tests only. Node.js is used for extension tests.

```sh
git clone https://github.com/yooongZa/xbar.git
cd xbar
swift test --package-path app --scratch-path /private/tmp/xbar-core-tests
node --test app/Tests/popup-tests.cjs
zsh app/build-local.sh /private/tmp/xbar-build
```


The script uses a development ad hoc signature by default and does not notarize the result. To use a self-built Safari extension, sign with your own Apple developer team and configure matching bundle identifiers and App Groups. Update these together:

- Team, app, and extension identifiers in `app/project.yml`
- App Group in `app/TodayBar.entitlements` and `app/SafariExtension/SafariExtension.entitlements`
- The same App Group in `app/Shared/TransferFiles.swift`

After installing XcodeGen, regenerate with `cd app && xcodegen generate`. Developer ID signing and notarization are separate steps for your own distribution. Set `TODAYBAR_SIGN_IDENTITY` to choose a signing certificate for the build script.

## License and service terms

Project code is licensed under [GPL-3.0-only](LICENSE). Distributed derivatives must meet GPLv3 requirements, including corresponding source. X-Bar is independent and not affiliated with X. This license does not grant rights to X’s service, trademarks, or third-party content in screenshots. Review the [X Terms of Service](https://x.com/en/tos) separately before use or distribution. This project has not obtained written approval from X.
