#!/usr/bin/env python3
"""Package an already notarized/stapled X-Bar app as an update ZIP and signed DMG.

The DMG still needs its own notary submission and stapling after this command.
"""
import argparse
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile


def run(arguments):
    result = subprocess.run([str(x) for x in arguments], text=True, capture_output=True)
    if result.returncode:
        raise SystemExit(result.stdout + result.stderr)
    return result.stdout + result.stderr


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('app', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--identity', required=True, help='Developer ID Application identity')
    args = parser.parse_args()
    info = plistlib.loads((args.app/'Contents/Info.plist').read_bytes())
    if info['CFBundleIdentifier'] != 'com.limod.xbar':
        raise SystemExit('Expected the production X-Bar app.')
    version = info['CFBundleShortVersionString']
    output = args.output.resolve()
    archive = output/f'X-Bar-{version}-macOS-universal.zip'
    dmg = output/f'X-Bar-{version}-macOS-universal.dmg'
    if archive.exists() or dmg.exists():
        raise SystemExit('Use an empty output folder to preserve previous release files.')
    output.mkdir(parents=True, exist_ok=True)
    root = Path(__file__).resolve().parents[1]
    with tempfile.TemporaryDirectory(prefix='xbar-package-') as temporary:
        staging = Path(temporary)/'contents'
        staging.mkdir()
        app = staging/'X-Bar.app'
        run(['ditto', '--noextattr', '--norsrc', args.app.resolve(), app])
        # Finder metadata is not part of the signed application or notary ticket.
        for attribute in ['com.apple.FinderInfo', 'com.apple.ResourceFork']:
            run(['xattr', '-dr', attribute, app])
        run(['xcrun', 'stapler', 'validate', app])
        run(['codesign', '--verify', '--deep', '--strict', app])
        run(['spctl', '--assess', '--type', 'execute', app])
        run(['ditto', '-c', '-k', '--keepParent', '--norsrc', '--noextattr', app, archive])
        (staging/'Applications').symlink_to('/Applications')
        shutil.copy2(root/'LICENSE', staging/'LICENSE.txt')
        (staging/'Install.txt').write_text(f'''X-Bar {version} — macOS 14+ / Apple Silicon & Intel

X-Bar를 Applications(응용 프로그램)로 드래그하고 실행하세요.
Safari 연결 또는 앱 내 로그인을 선택하세요.
사용 안내: https://yooongza.github.io/xbar/support.html

Drag X-Bar into Applications, open it, then connect with Safari or sign in in the app.
Help: https://yooongza.github.io/xbar/support-en.html

X-BarをApplicationsへドラッグして開き、Safari接続かアプリ内ログインを選んでください。
使い方: https://yooongza.github.io/xbar/support-ja.html

Follow the creator / 제작자 팔로우 / 開発者をフォロー: https://x.com/yongZa_R
GPL-3.0-only source: https://github.com/yooongZa/xbar/tree/v{version}
''')
        run(['hdiutil', 'create', '-volname', f'X-Bar {version}', '-srcfolder', staging,
             '-format', 'UDZO', '-fs', 'HFS+', dmg])
        run(['codesign', '--sign', args.identity, '--timestamp', dmg])
        run(['codesign', '--verify', '--strict', dmg])
    print(json.dumps({'version': version, 'zip': str(archive), 'dmg': str(dmg),
                      'dmg_requires_notarization': True}, indent=2))


if __name__ == '__main__':
    main()
