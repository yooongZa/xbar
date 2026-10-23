import SafariServices

// The context is retained for one response, on one worker, without shared mutation.
private final class ReplyContext: @unchecked Sendable {
    let context: NSExtensionContext
    init(_ context: NSExtensionContext) { self.context = context }
    func finish(_ result: [String: Any]) {
        let response = NSExtensionItem()
        response.userInfo = [SFExtensionMessageKey: result]
        context.completeRequest(returningItems: [response], completionHandler: nil)
    }
}

final class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling {
    func beginRequest(with context: NSExtensionContext) {
        let reply = ReplyContext(context)
        do {
            guard let item = context.inputItems.first as? NSExtensionItem,
                  let message = item.userInfo?[SFExtensionMessageKey] as? [String: Any],
                  let action = message["action"] as? String else { throw TransferError.invalid }
            if action == "prepare" {
                let available = try TransferFiles.read(PairingAvailability.self, at: TransferFiles.availabilityURL())
                let intent = PairingIntent(id: UUID(), instance: available.instance, createdAt: Date())
                try TransferFiles.write(intent, at: TransferFiles.intentURL())
                // No credential is read until the running app creates this request.
                DispatchQueue.global(qos: .userInitiated).async {
                    for _ in 0..<50 {
                        if let requestURL = try? TransferFiles.requestURL(),
                           let request = try? TransferFiles.read(TransferRequest.self, at: requestURL),
                           request.id == intent.id, (try? request.validate()) != nil {
                            reply.finish(["ok": true, "id": request.id.uuidString]); return
                        }
                        Thread.sleep(forTimeInterval: 0.1)
                    }
                    reply.finish(["ok": false, "error": "app_not_ready"])
                }
                return
            }
            guard action == "connect" else { throw TransferError.invalid }
            let requestURL = try TransferFiles.requestURL()
            let request = try TransferFiles.read(TransferRequest.self, at: requestURL)
            try request.validate()
            guard message["id"] as? String == request.id.uuidString,
                  let raw = message["cookies"], JSONSerialization.isValidJSONObject(raw) else { throw TransferError.invalid }
            let data = try JSONSerialization.data(withJSONObject: raw)
            guard data.count <= SessionTransfer.maximumBytes else { throw TransferError.invalid }
            let cookies = try JSONDecoder().decode([SessionCookie].self, from: data)
            let envelope = try SessionTransfer.seal(cookies, for: request)
            let claim = requestURL.deletingLastPathComponent().appendingPathComponent(request.id.uuidString + ".claimed")
            try FileManager.default.moveItem(at: requestURL, to: claim)
            defer { try? FileManager.default.removeItem(at: claim) }
            try TransferFiles.write(envelope, at: TransferFiles.responseURL(request.id))
            reply.finish(["ok": true])
        } catch { reply.finish(["ok": false, "error": "retry"]) }
        // Never include cookie values, message bodies, or raw errors in logs.
    }
}
