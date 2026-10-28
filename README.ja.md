# 𝕏-Bar

| Mac | Windows |
| --- | --- |
| **[Mac版をダウンロード](https://github.com/yooongZa/xbar/releases/download/v0.5.4/X-Bar-0.5.4-macOS-universal.dmg)** | **[Windows版をダウンロード](https://github.com/yooongZa/xbar/releases/download/windows-v0.4.2-beta.1/XBar-0.4.2.0-windows-x64-setup.exe)** |
| 0.5.4 · macOS 14+ · Apple Silicon / Intel | 0.4.2 beta.1 · Windows 11 x64 |
| DMG · Signed & notarized | EXE · Unsigned installer |

Windowsは未署名EXEのベータ版です。SmartScreenの警告や端末ポリシーによる実行制限が生じる場合があります。 [インストール方法](windows/README.md).

[한국어](README.ko.md) · [English](README.en.md) · [日本語](README.ja.md)

**投稿の反応を、メニューバーでひと目に。**

投稿したあとの反応が気になったら、Macの画面上部でさっと確認。X-Barは今日のインプレッションと人気の投稿を表示する、無料のオープンソースアプリです。

**[開発者 @yongZa_R をフォロー ↗](https://x.com/yongZa_R)**

macOS 14以降 · Apple Silicon / Intel · 한국어 / English / 日本語

## Macのインストールと使い始め方

1. このページ上部の **Mac版をダウンロード** を押します。GitHubへの登録は不要です。
2. DMGを開き、**X-Barをアプリケーションフォルダへドラッグ**します。
3. アプリケーションフォルダから起動します。旧バージョンが起動している場合は、先に終了してください。
4. **Safariで接続** または **アプリ内でログイン** を選びます。
5. アカウント名と今日のインプレッションを確認できれば、設定完了です。メニューバーの数字をクリックしてみてください。

公式の配布ファイルはDeveloper IDで署名し、Appleの公証を受けています。初回起動時にmacOSが確認を求める場合があります。セキュリティ設定の無効化やターミナル操作は必要ありません。

### Safariで接続

いつものSafariのログイン方法や保存済みパスワードを利用できます。Safariの設定 → 機能拡張でX-Barを有効にし、x.comへのアクセスを許可してXにログインします。SafariのツールバーからX-Barを開き、説明を確認して **X-Barに接続** を押してください。接続後は、Safariを終了してもX-Barが起動していれば更新を試みます。

### アプリ内でログイン

アプリ内のX画面でメールアドレスまたはユーザー名を入力してログインします。この方法ではGoogleログインに対応していません。Xアナリティクスを利用できるアカウントが必要です。Xから追加認証を求められることもあります。

## 確認できること

- アカウント全体の **今日のインプレッション** をメニューバーに表示。
- **今日 / 過去2日 / 過去7日** に投稿された、アプリが確認できた上位5件のインプレッション・いいね・リポスト数。
- 投稿をクリックすると、その投稿の分析画面へ移動。
- リポストが急増したときの小さな虹色のエフェクト。設定でオフにできます。

期間は **投稿日** を基準にしています。投稿ごとの数字は投稿後の累計で、選択期間内だけのインプレッションではありません。アカウントの今日の合計には以前の投稿も含まれます。順位はアプリが確認した投稿に基づき、アカウント内のすべての投稿を網羅するとは限りません。

## ご利用にあたって

今日の合計は初期設定で10分ごとに確認します。設定で5・10・30分を選べます。投稿一覧は約10分ごとに確認します。ネットワーク、Macのスリープ、Xの応答状況により更新が遅れる場合があります。新しい投稿がXアナリティクスに表示されるまで時間がかかることもあります。アプリを終了すると更新も止まります。Xの画面構成が変わると、アプリの更新が必要になる場合があります。

「初回確認後」は、アプリが投稿を最初に確認した時点からの増加量です。1日全体の増加量ではありません。アプリの再起動やアカウント・日付・表示期間の変更などで基準がリセットされます。

0.5.2以降は設定の**アップデートを確認**から更新できます。1日1回自動で確認し、設定でオフにできます。**インストール**を選ぶとダウンロード・インストール・再起動まで進みます。

### ログイン時に自動起動

0.5.4以降は、初回起動時にログイン項目へ登録します。設定の「ログイン時に自動起動」でオン・オフを切り替えられます。macOSの許可が必要な場合は「ログイン項目の設定を開く」からX-Barを許可してください。オフにした設定は再起動後も保持されます。

## プライバシーとサポート

アカウント名・投稿・指標を開発者のサーバーへ送信しません。ログインセッションは、このMacのアプリ専用領域に保存します。Safari接続では、利用者の確認を経てx.comのログインCookieをローカルアプリへ渡します。Xとの通信にはXのプライバシーポリシーが適用されます。

[使い方・サポート](https://yooongza.github.io/xbar/support-ja.html) · [プライバシー](https://yooongza.github.io/xbar/privacy-ja.html) · [メールで問い合わせ](mailto:sukmack@gmail.com)

気に入っていただけたら、**[@yongZa_Rをフォロー](https://x.com/yongZa_R)**してください。アプリのお知らせや制作の様子をお届けします。フォローは任意です。

## ソースからビルド

macOS 14以降、Swift 6対応のXcode、Command Line Toolsが必要です。Safari機能拡張を含むアプリ全体はXcodeプロジェクトでビルドします。Swift Packageはコアロジックのテスト用です。機能拡張のテストにはNode.jsを使います。

```sh
git clone https://github.com/yooongZa/xbar.git
cd xbar
swift test --package-path app --scratch-path /private/tmp/xbar-core-tests
node --test app/Tests/popup-tests.cjs
zsh app/build-local.sh /private/tmp/xbar-build
```


スクリプトは初期設定で開発用のad hoc署名を使い、公証は行いません。自分でビルドしたSafari機能拡張を利用するには、自分のApple開発者チームで署名し、Bundle IDとApp Groupを合わせる必要があります。次の設定をまとめて変更してください。

- `app/project.yml` のチーム・アプリ・機能拡張の識別子
- `app/TodayBar.entitlements` と `app/SafariExtension/SafariExtension.entitlements` のApp Group
- `app/Shared/TransferFiles.swift` の同じApp Group

XcodeGenをインストールした後、`cd app && xcodegen generate`で再生成します。独自に配布する場合のDeveloper ID署名とAppleの公証は別途必要です。ビルド時の署名証明書は`TODAYBAR_SIGN_IDENTITY`環境変数で指定できます。

## ライセンスとサービス規約

プロジェクトのコードは[GPL-3.0-only](LICENSE)で公開しています。派生プログラムを配布する場合は、対応するソースの提供を含めGPLv3の条件を守る必要があります。X-BarはXと提携していない独立したプロジェクトです。このライセンスはXのサービス・商標・画像に含まれる第三者のコンテンツへの権利を付与しません。利用・配布に際しては、[Xの利用規約](https://x.com/en/tos)を別途ご確認ください。本プロジェクトはXから書面による承認を得ていません。
