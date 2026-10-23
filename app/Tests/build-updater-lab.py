#!/usr/bin/env python3
"""Build two isolated, sandboxed apps around the production Sparkle updater for UI testing."""
import argparse, json, plistlib, shutil, subprocess
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--directory',required=True,type=Path)
parser.add_argument('--tools',required=True,type=Path,help='Sparkle distribution root, containing bin/ and Sparkle.framework')
parser.add_argument('--identity',required=True)
parser.add_argument('--account',default='com.limod.xbar.updates')
parser.add_argument('--target-build',default='2')
parser.add_argument('--candidate-only',action='store_true')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
p=args.directory.resolve()
production=p/'production/X-Bar.app'
port=json.loads((p/'server.json').read_text())['port']
def run(command):
    return subprocess.run([str(x) for x in command],check=True,capture_output=True,text=True).stdout.strip()
run(['swiftc','-swift-version','6','-parse-as-library','-framework','AppKit','-framework','WebKit',
    '-framework','SwiftUI','-F',args.tools,'-framework','Sparkle',
    '-Xlinker','-rpath','-Xlinker','@executable_path/../Frameworks',
    root/'Sources/TodayBar/AppUpdater.swift',root/'Sources/TodayBarCore/UpdateInstallationConsent.swift',
    root/'Tests/Integration/UpdaterLab.swift','-o',p/'UpdaterLab'])
production_info=plistlib.loads((production/'Contents/Info.plist').read_bytes())
identifier='com.limod.xbar.updaterlab20261023'
entitlements={'com.apple.security.app-sandbox':True,'com.apple.security.network.client':True,
    'com.apple.security.temporary-exception.mach-lookup.global-name':[identifier+'-spks',identifier+'-spki']}
(p/'lab.entitlements').write_bytes(plistlib.dumps(entitlements))
versions = [(args.target_build,'candidate')] if args.candidate_only else [('1','installed'),(args.target_build,'candidate')]
for version,folder in versions:
    app=p/folder/'X-Bar Updater Lab.app'
    (app/'Contents/MacOS').mkdir(parents=True,exist_ok=True)
    shutil.copy2(p/'UpdaterLab',app/'Contents/MacOS/UpdaterLab')
    (app/'Contents/Frameworks').mkdir(exist_ok=True)
    run(['ditto',production/'Contents/Frameworks/Sparkle.framework',app/'Contents/Frameworks/Sparkle.framework'])
    info={'CFBundleIdentifier':identifier,'CFBundleExecutable':'UpdaterLab','CFBundleName':'X-Bar Updater Lab',
        'CFBundleDisplayName':'X-Bar Updater Lab','CFBundleVersion':version,'CFBundleShortVersionString':'0.0.'+version,
        'CFBundlePackageType':'APPL','LSMinimumSystemVersion':'14.0',
        'SUFeedURL':f'http://127.0.0.1:{port}/appcast.xml','SUPublicEDKey':production_info['SUPublicEDKey'],
        'SUEnableInstallerLauncherService':True,'SUEnableAutomaticChecks':False,'SUAutomaticallyUpdate':False,
        'SUAllowsAutomaticUpdates':False,'SUEnableSystemProfiling':False,'SUVerifyUpdateBeforeExtraction':True,
        'SURequireSignedFeed':True,'SUSignedFeedFailureExpirationInterval':0,
        'NSAppTransportSecurity':{'NSAllowsLocalNetworking':True,'NSAllowsArbitraryLoads':True}}
    (app/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
    run(['codesign','--force','--sign',args.identity,'--timestamp','--options','runtime','--entitlements',p/'lab.entitlements',app])
    run(['codesign','--verify','--deep','--strict',app])
archive=p/f'served/UpdaterLab-{args.target_build}.zip'
run(['ditto','-c','-k','--keepParent','--norsrc','--noextattr',p/'candidate/X-Bar Updater Lab.app',archive])
signature=run([args.tools/'bin/sign_update','--account',args.account,'-p',archive])
xml=f'''<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle"><channel><title>X-Bar Updater Lab</title><item><title>Updater Lab 0.0.{args.target_build}</title><sparkle:version>{args.target_build}</sparkle:version><sparkle:shortVersionString>0.0.{args.target_build}</sparkle:shortVersionString><sparkle:minimumSystemVersion>14.0</sparkle:minimumSystemVersion><description>Test signed update and preserve settings.</description><enclosure url="http://127.0.0.1:{port}/{archive.name}" sparkle:edSignature="{signature}" length="{archive.stat().st_size}" type="application/octet-stream"/></item></channel></rss>'''
feed=p/'served/appcast.xml';feed.write_text(xml)
run([args.tools/'bin/sign_update','--account',args.account,feed])
shutil.copy2(feed,p/'valid-appcast.xml')
print(json.dumps({'installed_app':str(p/'installed/X-Bar Updater Lab.app'),'candidate_app':str(p/'candidate/X-Bar Updater Lab.app'),
    'feed':str(feed),'result_file':'~/Library/Containers/'+identifier+'/Data/Library/Application Support/updater-lab-result.json'},indent=2))
