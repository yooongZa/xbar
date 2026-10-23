import AppKit

@MainActor
enum StatusSymbol {
    enum Style { case plain, loading, rainbow }
    static func image(style: Style, phase: Double, reduceMotion: Bool) -> NSImage {
        let image = NSImage(size: NSSize(width: 19, height: 19), flipped: false) { rect in
            NSGraphicsContext.saveGraphicsState()
            defer { NSGraphicsContext.restoreGraphicsState() }
            let context = NSGraphicsContext.current!.cgContext
            if style == .loading && !reduceMotion {
                context.translateBy(x: 9.5, y: 9.5)
                context.rotate(by: phase * 2 * .pi)
                context.translateBy(x: -9.5, y: -9.5)
            }
            let size: CGFloat = style == .loading ? 11 : 16
            let label = NSAttributedString(string: "𝕏", attributes: [.font: NSFont.systemFont(ofSize: size, weight: .medium), .foregroundColor: NSColor.white])
            let measured = label.size()
            label.draw(at: NSPoint(x: (19 - measured.width) / 2, y: (19 - measured.height) / 2))
            if style == .loading {
                let arc = NSBezierPath()
                arc.appendArc(withCenter: NSPoint(x: 9.5, y: 9.5), radius: 8, startAngle: 15, endAngle: 280)
                arc.lineWidth = 1.2; arc.lineCapStyle = .round; NSColor.white.setStroke(); arc.stroke()
            }
            if style == .rainbow {
                // Color only the glyph's alpha mask; the number stays monochrome.
                context.setBlendMode(.sourceIn)
                let hue = reduceMotion ? 0 : phase
                let colors = (0...6).map { NSColor(calibratedHue: (hue + Double($0) / 6).truncatingRemainder(dividingBy: 1), saturation: 0.85, brightness: 0.95, alpha: 1) }
                NSGradient(colors: colors)!.draw(in: rect, angle: 45)
            }
            return true
        }
        image.isTemplate = style != .rainbow
        return image
    }
}
