// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "TodayBarCore",
    defaultLocalization: "ko",
    platforms: [.macOS(.v14)],
    products: [.library(name: "TodayBarCore", targets: ["TodayBarCore"])],
    targets: [
        .target(name: "TodayBarCore", resources: [.process("Resources")]),
        .testTarget(name: "TodayBarCoreTests", dependencies: ["TodayBarCore"])
    ]
)
