#!/usr/bin/env python3
"""Compile current production logic against isolated WebKit infrastructure.
No real credentials, X requests, or installed app data. Requires macOS + Xcode.
"""
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='xbar-regression-') as directory:
    temp = Path(directory)
    browser = (root/'Sources/TodayBar/BrowserController.swift').read_text()
    browser = browser.replace('WKWebsiteDataStore = .default()', 'WKWebsiteDataStore = .nonPersistent()')
    browser = browser.replace('        pathMonitor.start(queue: DispatchQueue(label: "dev.xbar.reachability"))',
                              '        // Offline harness: no network monitor.')
    (temp/'BrowserController.swift').write_text(browser)
    monitor = (root/'Sources/TodayBar/ContentMonitor.swift').read_text()
    monitor = monitor.replace('config.websiteDataStore = .default()', 'config.websiteDataStore = .nonPersistent()')
    monitor = monitor.replace('Bundle.main.url(forResource: "ContentExtractor", withExtension: "js")',
                              'Optional(URL(fileURLWithPath: CommandLine.arguments[1]))')
    monitor = monitor.replace('Bundle.main.url(forResource: "PageContext", withExtension: "js")',
                              'Optional(URL(fileURLWithPath: CommandLine.arguments[1]).deletingLastPathComponent().appendingPathComponent("PageContext.js"))')
    monitor = monitor.replace('view.load(URLRequest(url: range.url, cachePolicy: .reloadIgnoringLocalCacheData))',
                              'view.loadHTMLString(AuditFixture.html, baseURL: range.url)')
    (temp/'ContentMonitor.swift').write_text(monitor)
    core = sorted((root/'Sources/TodayBarCore').glob('*.swift'))
    command = ['swiftc', '-swift-version', '6', '-parse-as-library', '-framework', 'AppKit',
               '-framework', 'WebKit', '-framework', 'Network', '-framework', 'CryptoKit']
    cases = {
        'Controller': [root/'Sources/TodayBar/ContentMonitor.swift', temp/'BrowserController.swift', root/'Tests/Integration/ControllerHarness.swift'],
        'ContinuousMetrics': [temp/'ContentMonitor.swift', root/'Tests/Integration/ContinuousMetricsHarness.swift']
    }
    for name, sources in cases.items():
        binary = temp/name
        subprocess.run(command + [str(p) for p in core+sources] + ['-o', str(binary)], check=True)
        subprocess.run([str(binary), str(root/'Resources/ContentExtractor.js')], check=True, timeout=60)
