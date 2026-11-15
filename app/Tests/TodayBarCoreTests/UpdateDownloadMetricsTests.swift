import XCTest
@testable import TodayBarCore

private actor MetricsDelivery {
    var bodies: [Data] = []
    var succeeds = false
    func send(_ body: Data) -> Bool { bodies.append(body); return succeeds }
    func enable() { succeeds = true }
}
@MainActor
final class UpdateDownloadMetricsTests: XCTestCase {
    func testRetryKeepsEventIDAndAcknowledgedVersionStaysDeduplicatedAfterRestart() async throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: dir) }
        let file = dir.appendingPathComponent("metrics.json"), delivery = MetricsDelivery()
        let metrics = UpdateDownloadMetrics(file: file) { await delivery.send($0) }
        await metrics.record(fromVersion: "0.5.9", toVersion: "0.5.10")
        await delivery.enable()
        let restarted = UpdateDownloadMetrics(file: file) { await delivery.send($0) }
        await restarted.flush()
        await restarted.record(fromVersion: "0.5.9", toVersion: "0.5.10")
        let bodies = await delivery.bodies
        XCTAssertEqual(bodies.count, 2)
        let a = try JSONDecoder().decode(UpdateDownloadMetrics.Event.self, from: bodies[0])
        let b = try JSONDecoder().decode(UpdateDownloadMetrics.Event.self, from: bodies[1])
        XCTAssertEqual(a, b)
        XCTAssertEqual(a.platform, "macos")
        XCTAssertNotNil(UUID(uuidString: a.installationId))
        await restarted.record(fromVersion: "0.5.10", toVersion: "0.5.10")
        await restarted.record(fromVersion: "0.5.10", toVersion: "0.5.9")
        let count = await delivery.bodies.count
        XCTAssertEqual(count, 2)
    }
    func testNewEventQueuedDuringDeliverySurvivesAcknowledgement() async throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: dir) }
        let started = expectation(description: "delivery started")
        let gate = DeliveryGate()
        let metrics = UpdateDownloadMetrics(file: dir.appendingPathComponent("metrics.json")) { body in
            let event = try! JSONDecoder().decode(UpdateDownloadMetrics.Event.self, from: body)
            if event.toVersion == "0.5.10" { started.fulfill(); await gate.wait() }
            await gate.record(event.toVersion)
            return true
        }
        let first = Task { await metrics.record(fromVersion: "0.5.9", toVersion: "0.5.10") }
        await fulfillment(of: [started], timeout: 2)
        await metrics.record(fromVersion: "0.5.10", toVersion: "0.5.11")
        await gate.release(); await first.value
        let sent = await gate.sent
        XCTAssertEqual(sent, ["0.5.10", "0.5.11"])
    }
}
private actor DeliveryGate {
    var continuation: CheckedContinuation<Void, Never>?
    var released = false
    var sent: [String] = []
    func wait() async { if !released { await withCheckedContinuation { continuation = $0 } } }
    func release() { released = true; continuation?.resume(); continuation = nil }
    func record(_ version: String) { sent.append(version) }
}
