#!/usr/bin/env python3
"""Build a signed Windows update feed using the existing Sparkle Keychain account. Never publishes."""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import plistlib
import re
import subprocess
import tempfile
from urllib.parse import urlparse, quote
import xml.etree.ElementTree as ET
import zipfile


def run(args):
    return subprocess.run([str(x) for x in args], check=True, capture_output=True, text=True).stdout.strip()


def verify_report(path, identity, digest, length):
    verification = json.loads(path.read_text(encoding='utf-8-sig'))
    expected = {'schema': 1, 'status': 'passed', 'sha256': digest, 'length': length,
                'identity': identity['Name'], 'publisher': identity['Publisher'],
                'version': identity['Version'], 'architecture': identity['ProcessorArchitecture'], 'timestamped': True}
    if not isinstance(verification, dict) or any(verification.get(k) != v for k, v in expected.items()):
        raise SystemExit('Windows release verification is missing, failed, or belongs to another package.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('--tools', type=Path, required=True, help='Sparkle 2.9.6 bin directory')
    parser.add_argument('--feed', type=Path, required=True)
    parser.add_argument('--url', required=True)
    parser.add_argument('--notes', type=Path, required=True, help='JSON object with ko/en/ja plain-text release notes')
    parser.add_argument('--verification', type=Path, help='Passed Windows verify-release.ps1 report; required for public candidates')
    parser.add_argument('--account', default='com.limod.xbar.updates')
    parser.add_argument('--allow-development', action='store_true', help='Prepare a private test candidate, not a public release')
    parser.add_argument('--lab', action='store_true', help='Allow loopback URL only for LIMOD.XBar.UpdaterLab')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    expected_key = plistlib.loads((root/'app/Info.plist').read_bytes())['SUPublicEDKey']
    key = run([args.tools/'generate_keys', '--account', args.account, '-p'])
    if key != expected_key:
        raise SystemExit('Keychain public key differs from the product update key.')
    with zipfile.ZipFile(args.package) as archive:
        if archive.namelist().count('AppxManifest.xml') != 1 or 'AppxSignature.p7x' not in archive.namelist():
            raise SystemExit('A signed MSIX is required. Sign and verify it on Windows first.')
        manifest = ET.fromstring(archive.read('AppxManifest.xml'))
    identity = manifest.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity').attrib
    version = identity['Version']
    if not re.fullmatch(r'\d{1,5}(?:\.\d{1,5}){3}', version) or any(int(x) > 65535 for x in version.split('.')):
        raise SystemExit('Invalid MSIX version.')
    if identity['Name'].endswith(('.Dev', '.UpdaterLab')) and not args.allow_development:
        raise SystemExit('Use --allow-development for a private development candidate.')
    url = urlparse(args.url)
    lab = args.lab and identity['Name'] == 'LIMOD.XBar.UpdaterLab' and url.scheme == 'http' and url.hostname == '127.0.0.1'
    public = url.scheme == 'https' and url.hostname == 'github.com' and url.path.startswith('/yooongZa/xbar/releases/download/') and url.port in (None, 443)
    if not (lab or public) or url.username or url.password or url.fragment or url.query:
        raise SystemExit('Use the product GitHub release URL or the isolated lab loopback server.')
    notes = json.loads(args.notes.read_text())
    if not isinstance(notes, dict) or not notes or not set(notes) <= {'ko','en','ja'} or any(not isinstance(v, str) or len(v) > 6000 for v in notes.values()):
        raise SystemExit('Invalid localized notes.')
    with args.package.open('rb') as package_file:
        digest = hashlib.file_digest(package_file, 'sha256').hexdigest()
    if not args.allow_development:
        if not args.verification:
            raise SystemExit('Public candidates require --verification from Windows verify-release.ps1.')
        verify_report(args.verification, identity, digest, args.package.stat().st_size)
    payload = {
        'schema': 1, 'platform': 'windows', 'version': version,
        'displayVersion': '.'.join(version.split('.')[:3]), 'minimumOsBuild': 22000,
        'publishedAt': datetime.now(timezone.utc).isoformat(), 'notes': notes,
        'packages': [{'identity': identity['Name'], 'publisher': identity['Publisher'],
                      'architecture': identity['ProcessorArchitecture'], 'url': args.url,
                      'length': args.package.stat().st_size, 'sha256': digest}]
    }
    raw = json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode()
    with tempfile.TemporaryDirectory(prefix='xbar-windows-feed-') as folder:
        signing_file = Path(folder)/'payload.bin'
        signing_file.write_bytes(raw)
        signature = run([args.tools/'sign_update', '--account', args.account, '-p', signing_file])
        run([args.tools/'sign_update', '--account', args.account, '--verify', signing_file, signature])
        if signing_file.read_bytes() != raw or len(base64.b64decode(signature, validate=True)) != 64:
            raise SystemExit('Signature output failed verification.')
    envelope = {'payload': base64.b64encode(raw).decode(), 'signature': signature}
    args.feed.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.feed.with_suffix(args.feed.suffix+'.tmp')
    temporary.write_text(json.dumps(envelope, separators=(',', ':'))+'\n')
    temporary.replace(args.feed)
    print(json.dumps({'version': version, 'identity': identity['Name'], 'sha256': digest,
                      'feed': str(args.feed.resolve()), 'published': False}, ensure_ascii=False))

if __name__ == '__main__':
    main()
