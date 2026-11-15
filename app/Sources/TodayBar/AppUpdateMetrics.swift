import Foundation

@MainActor
enum AppUpdateMetrics {
    static func make() -> UpdateDownloadMetrics? {
        #if DEBUG
        return nil
        #else
        guard Bundle.main.bundleIdentifier == "com.limod.xbar", !ProcessInfo.processInfo.arguments.contains("--metrics-disabled"),
              let folder = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first else { return nil }
        let file = folder.appendingPathComponent("com.limod.xbar/update-metrics.json")
        return UpdateDownloadMetrics(file: file) { body in
            let config = URLSessionConfiguration.ephemeral
            config.timeoutIntervalForRequest = 5; config.timeoutIntervalForResource = 8
            config.httpCookieStorage = nil; config.httpShouldSetCookies = false
            let session = URLSession(configuration: config)
            defer { session.finishTasksAndInvalidate() }
            var request = URLRequest(url: URL(string: "https://stats.lazinessisthemotherof.dev/app/xbar/update-download")!)
            request.httpMethod = "POST"; request.httpBody = body; request.httpShouldHandleCookies = false
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            do { let (_, response) = try await session.data(for: request); return (response as? HTTPURLResponse)?.statusCode == 204 }
            catch { return false }
        }
        #endif
    }
}
