import Foundation

/// Only update versions and a random local installation ID leave the app.
/// The actor keeps new events while a previous delivery is awaiting its response.
public actor UpdateDownloadMetrics {
    public struct Event: Codable, Sendable, Equatable {
        public let version: Int
        public let eventId: String
        public let installationId: String
        public let platform: String
        public let fromVersion: String
        public let toVersion: String
    }
    private struct State: Codable {
        var installationId = UUID().uuidString.lowercased()
        var pending: [Event] = []
        var recorded: [String] = []
    }
    private let file: URL
    private let send: @Sendable (Data) async -> Bool
    private var state: State
    private var sending = false
    public init(file: URL, send: @escaping @Sendable (Data) async -> Bool) {
        self.file = file; self.send = send
        state = (try? JSONDecoder().decode(State.self, from: Data(contentsOf: file))) ?? State()
    }
    public func record(fromVersion: String, toVersion: String) async {
        guard fromVersion.compare(toVersion, options: .numeric) == .orderedAscending,
              !state.recorded.contains(toVersion), !state.pending.contains(where: { $0.toVersion == toVersion }) else { return }
        let previous = state
        state.pending.append(Event(version: 1, eventId: UUID().uuidString.lowercased(),
            installationId: state.installationId, platform: "macos", fromVersion: fromVersion, toVersion: toVersion))
        guard save() else { state = previous; return }
        await flush()
    }
    public func flush() async {
        guard !sending else { return }; sending = true
        defer { sending = false }
        while let event = state.pending.first {
            guard let body = try? JSONEncoder().encode(event), await send(body) else { return }
            // Apply the acknowledgement to current state, preserving events queued during await.
            let previous = state
            state.pending.removeAll { $0.eventId == event.eventId }
            state.recorded.append(event.toVersion)
            guard save() else { state = previous; return }
        }
    }
    private func save() -> Bool {
        do {
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try JSONEncoder().encode(state).write(to: file, options: .atomic)
            return true
        } catch { return false }
    }
}
