import XCTest
@testable import TodayBarCore

final class LocalizationTests: XCTestCase {
    func testPreferredLanguageAndRegionalFallback() {
        XCTAssertEqual(L10n.language(for: ["en-GB"]), "en")
        XCTAssertEqual(L10n.language(for: ["ja-JP", "en"]), "ja")
        XCTAssertEqual(L10n.language(for: ["ko_KR"]), "ko")
        XCTAssertEqual(L10n.language(for: ["fr-FR", "ja"]), "ja")
        XCTAssertEqual(L10n.language(for: ["fr"]), "en")
    }

    func testAllLanguagesHaveIdenticalKeysAndFormatArguments() throws {
        var catalogs: [String: [String: String]] = [:]
        for language in ["ko", "en", "ja"] {
            let folder = try XCTUnwrap(L10n.resourceBundle.url(forResource: language, withExtension: "lproj"))
            let data = try Data(contentsOf: folder.appendingPathComponent("Localizable.strings"))
            catalogs[language] = try XCTUnwrap(PropertyListSerialization.propertyList(from: data, format: nil) as? [String: String])
        }
        let korean = try XCTUnwrap(catalogs["ko"])
        XCTAssertGreaterThan(korean.count, 150)
        for language in ["en", "ja"] {
            let translated = try XCTUnwrap(catalogs[language])
            XCTAssertEqual(Set(korean.keys), Set(translated.keys))
            for (key, value) in translated {
                XCTAssertFalse(value.isEmpty, key)
                XCTAssertEqual(value.components(separatedBy: "%@").count,
                               korean[key]!.components(separatedBy: "%@").count, key)
                XCTAssertNil(value.range(of: "[가-힣]", options: .regularExpression), key)
            }
        }
    }

    func testRuntimeLookupAndEnglishFallback() {
        XCTAssertEqual(L10n.text("설정", language: "en"), "Settings")
        XCTAssertEqual(L10n.text("설정", language: "ja"), "設定")
        XCTAssertEqual(L10n.text("설정", language: "ko"), "설정")
        XCTAssertEqual(L10n.text("최근 2일", language: "en"), "Last 2 days")
        XCTAssertEqual(L10n.text("최근 7일", language: "ja"), "直近7日間")
        XCTAssertEqual(L10n.text("설정", language: "fr"), "Settings")
    }

    func testLocalizedFormatKeepsValuesAndPostText() {
        let template = L10n.text("post.accessibility", language: "ja")
        let value = String(format: template, "1", "Original 한국어 post", "1.2万", "42", "6")
        XCTAssertEqual(value, "1位、Original 한국어 post、表示回数1.2万、いいね42、リポスト6")
    }

    func testJapaneseAbbreviationsKeepApproximationAndValidation() {
        XCTAssertEqual(DisplayCount("1.2万")?.value, 12_000)
        XCTAssertEqual(DisplayCount("2億")?.value, 200_000_000)
        XCTAssertNil(DisplayCount("1万")?.exactInteger)
        XCTAssertNil(DisplayCount("1万%"))
        XCTAssertNil(DisplayCount("1,2万"))
    }
}
