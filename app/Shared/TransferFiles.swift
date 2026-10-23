import Foundation

// Only public pairing material and encrypted, short-lived responses enter disk.
// The private decryption key lives exclusively in the running app's memory.
enum TransferFiles {
    static let group = "495S4FVMCB.com.limod.xbar"
    static func directory() throws -> URL {
        guard let groupURL = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: group) else { throw TransferError.unavailable }
        let directory = groupURL.appendingPathComponent("SafariPairing", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        return directory
    }
    static func availabilityURL() throws -> URL { try directory().appendingPathComponent("available.json") }
    static func intentURL() throws -> URL { try directory().appendingPathComponent("intent.json") }
    static func clearHandshake() {
        guard let dir = try? directory(), let urls = try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil) else { return }
        for url in urls where url.lastPathComponent != "available.json" && ["json", "response", "claimed"].contains(url.pathExtension) { try? FileManager.default.removeItem(at: url) }
    }
    static func requestURL() throws -> URL { try directory().appendingPathComponent("request.json") }
    static func responseURL(_ id: UUID) throws -> URL { try directory().appendingPathComponent(id.uuidString + ".response") }
    static func read<T: Decodable>(_ type: T.Type, at url: URL) throws -> T {
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard let count = attributes[.size] as? NSNumber, count.intValue <= 49152,
              attributes[.type] as? FileAttributeType == .typeRegular else { throw TransferError.invalid }
        return try JSONDecoder().decode(type, from: Data(contentsOf: url))
    }
    static func write<T: Encodable>(_ value: T, at url: URL) throws {
        try JSONEncoder().encode(value).write(to: url, options: [.atomic, .completeFileProtection])
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
    }
    static func cleanup() {
        guard let dir = try? directory(), let urls = try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil) else { return }
        for url in urls where ["json", "response", "claimed"].contains(url.pathExtension) { try? FileManager.default.removeItem(at: url) }
    }
}
