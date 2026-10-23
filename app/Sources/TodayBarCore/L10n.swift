import Foundation

/// Native UI localization. Account names, post text, stored identifiers and X data
/// are deliberately not translated. The language is selected at app launch.
enum L10n {
    static func language(for preferences: [String]) -> String {
        for preference in preferences {
            let code = preference.replacingOccurrences(of: "_", with: "-")
                .split(separator: "-").first.map(String.init)?.lowercased() ?? ""
            if ["ko", "en", "ja"].contains(code) { return code }
        }
        return "en"
    }

    static let language = language(for: Locale.preferredLanguages)
    static var resourceBundle: Bundle {
        #if SWIFT_PACKAGE
        Bundle.module
        #else
        Bundle.main
        #endif
    }

    static func text(_ key: String, language: String = language) -> String {
        let resources = resourceBundle
        let fallback = resources.path(forResource: "en", ofType: "lproj")
            .flatMap(Bundle.init(path:))?.localizedString(forKey: key, value: key, table: nil) ?? key
        guard let path = resources.path(forResource: language, ofType: "lproj"),
              let bundle = Bundle(path: path) else { return fallback }
        return bundle.localizedString(forKey: key, value: fallback, table: nil)
    }

    static func format(_ key: String, _ arguments: CVarArg...) -> String {
        String(format: text(key), locale: Locale(identifier: language), arguments: arguments)
    }

    static func time(_ date: Date) -> String {
        date.formatted(Date.FormatStyle(date: .omitted, time: .shortened)
            .locale(Locale(identifier: language)))
    }
}
