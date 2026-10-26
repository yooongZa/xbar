#!/usr/bin/env python3
"""Test current controller/extractors with offline WebKit; never use app sessions.

Only storage, resources, clock, timer, and network responses are replaced. The
production navigation callbacks, scan cancellation, retry and UI state run intact.
"""
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]


def replace_once(source, before, after):
    assert source.count(before) == 1, before
    return source.replace(before, after)


with tempfile.TemporaryDirectory(prefix='xbar-navigation-') as directory:
    temp = Path(directory)
    browser = (root/'Sources/TodayBar/BrowserController.swift').read_text()
    browser = replace_once(browser, 'WKWebsiteDataStore = .default()', 'WKWebsiteDataStore = .nonPersistent()')
    browser = replace_once(browser, 'pathMonitor.start(queue: DispatchQueue(label: "dev.xbar.reachability"))', '// Offline: no network monitor.')
    browser = replace_once(browser, 'withTimeInterval: 5, repeats: true', 'withTimeInterval: 3600, repeats: true')
    browser = replace_once(browser, 'Bundle.main.url(forResource: name, withExtension: "js")', 'Optional(URL(fileURLWithPath: CommandLine.arguments[1]).deletingLastPathComponent().appendingPathComponent(name + ".js"))')
    response = 'webView.loadHTMLString(RecoveryFixture.responseHTML, baseURL: Self.analyticsURL)'
    browser = replace_once(browser, 'webView.reloadFromOrigin()', response)
    browser = replace_once(browser, 'webView.load(URLRequest(url: Self.analyticsURL, cachePolicy: .reloadIgnoringLocalCacheData))', response)
    browser = browser.replace('Date()', 'RecoveryFixture.now')
    browser = replace_once(browser, '        let method = advance ? "advance" : "snapshot"', '''        if RecoveryFixture.stallReads {
            RecoveryFixture.stalledReadStarted = true
            try await Task.sleep(for: .seconds(60))
        }
        let method = advance ? "advance" : "snapshot"''')
    browser += '\n@MainActor extension BrowserController { func auditTick() { tick() } }\n'
    (temp/'BrowserController.swift').write_text(browser)

    content = (root/'Sources/TodayBar/ContentMonitor.swift').read_text()
    content = replace_once(content, 'config.websiteDataStore = .default()', 'config.websiteDataStore = .nonPersistent()')
    for name in ['ContentExtractor', 'PageContext']:
        content = replace_once(content, f'Bundle.main.url(forResource: "{name}", withExtension: "js")', f'Optional(URL(fileURLWithPath: CommandLine.arguments[1]).deletingLastPathComponent().appendingPathComponent("{name}.js"))')
    content = replace_once(content, 'view.load(URLRequest(url: range.url, cachePolicy: .reloadIgnoringLocalCacheData))', 'view.loadHTMLString(RecoveryFixture.contentHTML, baseURL: range.url)')
    (temp/'ContentMonitor.swift').write_text(content)
    sources = sorted((root/'Sources/TodayBarCore').glob('*.swift')) + [temp/'BrowserController.swift', temp/'ContentMonitor.swift', root/'Tests/Integration/NavigationRecoveryHarness.swift']
    binary = temp/'NavigationRecovery'
    subprocess.run(['swiftc', '-swift-version', '6', '-parse-as-library', '-framework', 'AppKit', '-framework', 'WebKit', '-framework', 'Network', '-framework', 'CryptoKit', *map(str, sources), '-o', str(binary)], check=True)
    subprocess.run([str(binary), str(root/'Resources/ContentExtractor.js')], check=True, timeout=120)
