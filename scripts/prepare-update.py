#!/usr/bin/env python3
"""Create and verify a signed Sparkle feed; does not upload or publish anything."""
import argparse
import hashlib
import json
import plistlib
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

def run(args):
    return subprocess.run([str(x) for x in args], check=True, text=True, capture_output=True).stdout.strip()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path, help='Signed universal X-Bar ZIP')
    parser.add_argument('--tools', required=True, type=Path, help='Sparkle 2.9.6 bin directory')
    parser.add_argument('--account', default='com.limod.xbar.updates')
    parser.add_argument('--feed', required=True, type=Path, help='Feed to create/update, retaining existing entries')
    args = parser.parse_args()
    archive = args.archive.resolve()
    with zipfile.ZipFile(archive) as zipped:
        info = plistlib.loads(zipped.read('X-Bar.app/Contents/Info.plist'))
    if info.get('CFBundleIdentifier') != 'com.limod.xbar':
        raise SystemExit('This tool only packages the production X-Bar bundle.')
    public_key = run([args.tools/'generate_keys', '--account', args.account, '-p'])
    if public_key != info.get('SUPublicEDKey'):
        raise SystemExit('Signing account does not match the public key embedded in this app.')
    version = info['CFBundleShortVersionString']
    if archive.name != f'X-Bar-{version}-macOS-universal.zip':
        raise SystemExit('Archive filename must match its embedded version.')
    with tempfile.TemporaryDirectory(prefix='xbar-feed-') as folder:
        staging = Path(folder)
        shutil.copy2(archive, staging/archive.name)
        if args.feed.exists(): shutil.copy2(args.feed, staging/'appcast.xml')
        print(run([args.tools/'generate_appcast', '--account', args.account,
            '--maximum-deltas', '0', '--download-url-prefix',
            f'https://github.com/yooongZa/xbar/releases/download/v{version}/',
            '--link', 'https://yooongza.github.io/xbar/', staging]))
        feed = staging/'appcast.xml'
        print(run([args.tools/'sign_update', '--account', args.account, '--verify', feed]))
        args.feed.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(feed, args.feed)
    sha = hashlib.sha256(archive.read_bytes()).hexdigest()
    print(json.dumps({'version': version, 'build': info['CFBundleVersion'],
        'archive': str(archive), 'sha256': sha, 'feed': str(args.feed.resolve()),
        'published': False}, indent=2))

if __name__ == '__main__': main()
