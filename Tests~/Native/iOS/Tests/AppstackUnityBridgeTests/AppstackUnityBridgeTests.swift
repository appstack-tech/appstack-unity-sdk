import Foundation
import XCTest
@_spi(AppstackInternal) import AppstackSDK
@testable import AppstackUnityBridge

private let callbackLock = NSLock()
private var callbackExpectation: XCTestExpectation?
private var callbackRequestId: Int32?
private var callbackJson: String?
private var callbackError: String?

private func recordAttributionCallback(
    _ requestId: Int32,
    _ json: UnsafeMutablePointer<CChar>?,
    _ error: UnsafeMutablePointer<CChar>?
) {
    callbackLock.lock()
    callbackRequestId = requestId
    callbackJson = json.map { String(cString: $0) }
    callbackError = error.map { String(cString: $0) }
    let expectation = callbackExpectation
    callbackLock.unlock()

    AppstackUnityFreeCString(json)
    AppstackUnityFreeCString(error)
    expectation?.fulfill()
}

final class AppstackUnityBridgeTests: XCTestCase {
    override func setUp() {
        super.setUp()
        AppstackAttributionSdk.shared.reset()
        AppstackASAAttribution.shared.reset()
        resetCallbackState()
    }

    override func tearDown() {
        AppstackAttributionSdk.shared.reset()
        AppstackASAAttribution.shared.reset()
        resetCallbackState()
        super.tearDown()
    }

    func testConfigureForwardsWrapperContractAndLogMapping() {
        let mappings: [(Int32, LogLevel)] = [
            (0, .debug),
            (1, .info),
            (2, .error),
            (3, .error),
            (-1, .info),
            (4, .info),
        ]

        for (unityLevel, expectedLevel) in mappings {
            AppstackAttributionSdk.shared.reset()
            callConfigure(
                apiKey: "api-key",
                logLevel: unityLevel,
                customerUserId: "customer-123",
                wrapperVersion: "unity-1.0.0"
            )

            let call = AppstackAttributionSdk.shared.configureCall
            XCTAssertEqual(call?.apiKey, "api-key")
            XCTAssertEqual(call?.logLevel, expectedLevel)
            XCTAssertEqual(call?.customerUserId, "customer-123")
            XCTAssertEqual(call?.wrapperVersion, "unity-1.0.0")
        }
    }

    func testConfigureRejectsEmptyApiKeyAndNormalizesEmptyOptionals() {
        callConfigure(apiKey: "", customerUserId: "", wrapperVersion: "")
        XCTAssertNil(AppstackAttributionSdk.shared.configureCall)

        callConfigure(apiKey: "api-key", customerUserId: "", wrapperVersion: "")
        XCTAssertNil(AppstackAttributionSdk.shared.configureCall?.customerUserId)
        XCTAssertNil(AppstackAttributionSdk.shared.configureCall?.wrapperVersion)
    }

    func testDevelopmentProxyReadsExpectedInfoPlistKey() throws {
        let bundleUrl = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString)
            .appendingPathExtension("bundle")
        try FileManager.default.createDirectory(
            at: bundleUrl,
            withIntermediateDirectories: true
        )
        defer { try? FileManager.default.removeItem(at: bundleUrl) }

        let info: [String: Any] = [
            "CFBundleIdentifier": "com.appstack.unity.contract",
            "CFBundleName": "AppstackContract",
            "CFBundlePackageType": "BNDL",
            "APPSTACK_DEV_PROXY_URL": "http://127.0.0.1:9090",
        ]
        let infoData = try PropertyListSerialization.data(
            fromPropertyList: info,
            format: .xml,
            options: 0
        )
        try infoData.write(to: bundleUrl.appendingPathComponent("Info.plist"))

        let bundle = try XCTUnwrap(Bundle(url: bundleUrl))
        applyDevelopmentProxy(from: bundle)

        XCTAssertEqual(
            AppstackAttributionSdk.shared.proxyUrl,
            "http://127.0.0.1:9090"
        )
    }

    func testSendEventMapsKnownEventsAndDropsStandardNames() {
        for event in EventType.allCases {
            callSendEvent(type: event.rawValue, name: "supplied", json: "{}")

            let call = AppstackAttributionSdk.shared.eventCall
            XCTAssertEqual(call?.event, event)
            XCTAssertEqual(call?.name, event == .CUSTOM ? "supplied" : nil)
        }
    }

    func testUnknownEventMapsToCustomAndPreservesUsefulName() {
        callSendEvent(type: "future_event", name: nil, json: nil)
        XCTAssertEqual(AppstackAttributionSdk.shared.eventCall?.event, .CUSTOM)
        XCTAssertEqual(AppstackAttributionSdk.shared.eventCall?.name, "future_event")

        callSendEvent(type: "future_event", name: "future_name", json: nil)
        XCTAssertEqual(AppstackAttributionSdk.shared.eventCall?.name, "future_name")
    }

    func testSendEventParsesJsonParametersAndRejectsMalformedJson() throws {
        callSendEvent(
            type: "PURCHASE",
            name: "ignored",
            json: #"{"revenue":12.5,"currency":"USD","empty":null,"items":[1,{"active":true}]}"#
        )

        let parameters = try XCTUnwrap(AppstackAttributionSdk.shared.eventCall?.parameters)
        XCTAssertEqual((parameters["revenue"] as? NSNumber)?.doubleValue, 12.5)
        XCTAssertEqual(parameters["currency"] as? String, "USD")
        XCTAssertTrue(parameters["empty"] is NSNull)
        let items = try XCTUnwrap(parameters["items"] as? [Any])
        XCTAssertEqual((items[0] as? NSNumber)?.intValue, 1)
        XCTAssertEqual((items[1] as? [String: Bool])?["active"], true)

        callSendEvent(type: "LOGIN", name: nil, json: "not-json")
        XCTAssertNil(AppstackAttributionSdk.shared.eventCall?.parameters)
    }

    func testAppleAdsAndSynchronousQueriesForwardToNativeSdk() {
        AppstackAttributionSdk.shared.appstackId = "idé-夏-🚀"
        AppstackAttributionSdk.shared.sdkDisabled = true

        AppstackUnityEnableAppleAdsAttribution()
        XCTAssertEqual(AppstackASAAttribution.shared.enableCalls, 1)

        let idPointer = AppstackUnityGetAppstackId()
        XCTAssertEqual(idPointer.map { String(cString: $0) }, "idé-夏-🚀")
        AppstackUnityFreeCString(idPointer)
        XCTAssertEqual(AppstackUnityIsSdkDisabled(), 1)
        AppstackUnityFreeCString(nil)
    }

    func testAttributionSuccessReturnsJsonAndOwnedCString() throws {
        AppstackAttributionSdk.shared.attributionResult = [
            "campaign": "Café 夏 🚀",
            "matched": true,
        ]
        callbackExpectation = expectation(description: "attribution callback")

        AppstackUnityGetAttributionParams(51, recordAttributionCallback)

        waitForExpectations(timeout: 2)
        XCTAssertEqual(callbackRequestId, 51)
        XCTAssertNil(callbackError)
        let json = try XCTUnwrap(callbackJson)
        let data = try XCTUnwrap(json.data(using: .utf8))
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any]
        )
        XCTAssertEqual(object["campaign"] as? String, "Café 夏 🚀")
        XCTAssertEqual(object["matched"] as? Bool, true)
        XCTAssertEqual(AppstackAttributionSdk.shared.attributionCalls, 1)
    }

    func testAttributionSerializationFailureReturnsOwnedErrorCString() {
        AppstackAttributionSdk.shared.attributionResult = ["unsupported": Date()]
        callbackExpectation = expectation(description: "attribution error callback")

        AppstackUnityGetAttributionParams(52, recordAttributionCallback)

        waitForExpectations(timeout: 2)
        XCTAssertEqual(callbackRequestId, 52)
        XCTAssertNil(callbackJson)
        XCTAssertEqual(
            callbackError,
            "Unable to serialize attribution parameters."
        )
    }

    private func callConfigure(
        apiKey: String,
        logLevel: Int32 = 1,
        customerUserId: String,
        wrapperVersion: String
    ) {
        apiKey.withCString { apiKeyPointer in
            customerUserId.withCString { customerPointer in
                wrapperVersion.withCString { wrapperPointer in
                    AppstackUnityConfigure(
                        apiKeyPointer,
                        logLevel,
                        customerPointer,
                        wrapperPointer
                    )
                }
            }
        }
    }

    private func callSendEvent(type: String, name: String?, json: String?) {
        type.withCString { typePointer in
            withOptionalCString(name) { namePointer in
                withOptionalCString(json) { jsonPointer in
                    AppstackUnitySendEvent(typePointer, namePointer, jsonPointer)
                }
            }
        }
    }

    private func withOptionalCString<T>(
        _ value: String?,
        _ body: (UnsafePointer<CChar>?) throws -> T
    ) rethrows -> T {
        guard let value else {
            return try body(nil)
        }
        return try value.withCString(body)
    }

    private func resetCallbackState() {
        callbackLock.lock()
        callbackExpectation = nil
        callbackRequestId = nil
        callbackJson = nil
        callbackError = nil
        callbackLock.unlock()
    }
}
