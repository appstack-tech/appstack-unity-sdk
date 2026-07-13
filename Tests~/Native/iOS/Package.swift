// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AppstackUnityIOSContract",
    platforms: [
        .macOS(.v13),
    ],
    targets: [
        .target(
            name: "AppstackSDK",
            path: "Stubs/AppstackSDK"
        ),
        .target(
            name: "AppstackUnityBridge",
            dependencies: ["AppstackSDK"],
            path: "Sources/AppstackUnityBridge"
        ),
        .testTarget(
            name: "AppstackUnityBridgeTests",
            dependencies: ["AppstackUnityBridge", "AppstackSDK"],
            path: "Tests/AppstackUnityBridgeTests"
        ),
    ]
)
