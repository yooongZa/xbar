#!/bin/zsh
set -euo pipefail
project_root="$(cd "$(dirname "$0")" && pwd)"
artifact_dir="${1:-$project_root/..}"
staging_dir="$(mktemp -d "${TMPDIR:-/private/tmp}/TodayBar-local.XXXXXX")"
trap 'rm -rf "$staging_dir"' EXIT
mkdir -p "$artifact_dir"
xcodebuild -project "$project_root/TodayBar.xcodeproj" -scheme TodayBar \
  -configuration Release -derivedDataPath "$staging_dir/DerivedData" \
  CODE_SIGNING_ALLOWED=NO ONLY_ACTIVE_ARCH=NO ARCHS="arm64 x86_64" build > "$staging_dir/build.log" 2>&1 || {
    tail -80 "$staging_dir/build.log"
    exit 1
  }
app_path="$staging_dir/DerivedData/Build/Products/Release/X-Bar.app"
signing_identity="${TODAYBAR_SIGN_IDENTITY:--}"
timestamp_args=(--timestamp=none)
if [[ "$signing_identity" != "-" ]]; then
  timestamp_args=(--timestamp)
fi
cp "$project_root/../LICENSE" "$app_path/Contents/Resources/LICENSE.txt"
cp "$project_root/../THIRD_PARTY_NOTICES.md" "$app_path/Contents/Resources/THIRD_PARTY_NOTICES.md"
sparkle_path="$app_path/Contents/Frameworks/Sparkle.framework/Versions/B"
# Sign nested helpers inside-out; each retains its own sandbox boundary.
for helper in "$sparkle_path/XPCServices/Installer.xpc" "$sparkle_path/XPCServices/Downloader.xpc" "$sparkle_path/Autoupdate" "$sparkle_path/Updater.app"; do
  codesign --force --sign "$signing_identity" "${timestamp_args[@]}" --options runtime --preserve-metadata=entitlements "$helper"
done
codesign --force --sign "$signing_identity" "${timestamp_args[@]}" --options runtime "$app_path/Contents/Frameworks/Sparkle.framework"
codesign --force --sign "$signing_identity" "${timestamp_args[@]}" --options runtime --entitlements "$project_root/SafariExtension/SafariExtension.entitlements" "$app_path/Contents/PlugIns/X-Bar Safari.appex"
codesign --force --sign "$signing_identity" "${timestamp_args[@]}" --options runtime --entitlements "$project_root/TodayBar.entitlements" "$app_path"
codesign --verify --deep --strict --verbose=2 "$app_path"
app_version=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app_path/Contents/Info.plist")
archive_path="$artifact_dir/X-Bar-${app_version}-macOS-universal.zip"
ditto -c -k --keepParent --norsrc --noextattr "$app_path" "$archive_path"
print "Created: $archive_path"
