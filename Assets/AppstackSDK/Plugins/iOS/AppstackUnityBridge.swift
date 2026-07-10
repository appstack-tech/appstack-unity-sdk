import Foundation
@_spi(AppstackInternal) @preconcurrency import AppstackSDK

@objc(AppstackUnityBridge)
public class AppstackUnityBridge: NSObject {

    private static let wrapperVersion = "unity-1.0.0"

    private static func eventTypeFromString(_ string: String) -> EventType? {
        return EventType.allCases.first { $0.rawValue == string.uppercased() }
    }

    @objc public static func configure(apiKey: String, logLevel: Int, customerUserId: String?) {
        // Translate the C#-side logLevel contract (0=DEBUG, 1=INFO, 2=WARN, 3=ERROR;
        // verbosity descending) into the native LogLevel enum (off/error/info/debug;
        // verbosity ascending). This keeps iOS consistent with Android and with the
        // documented C# values. iOS has no dedicated WARN tier, so WARN folds down to
        // .error — quieter than INFO, and there are no warn-level logs on iOS to lose.
        let logLevelEnum: LogLevel
        switch logLevel {
        case 0: logLevelEnum = .debug
        case 1: logLevelEnum = .info
        case 2, 3: logLevelEnum = .error
        default: logLevelEnum = .info
        }

        // Testing-only proxy override, read from the app's Info.plist. This is NOT
        // exposed through the public Configure() API: a proxy URL is applied only if
        // the host app deliberately ships an APPSTACK_DEV_PROXY_URL key; published-
        // package consumers do not. Routed through the SDK's @_spi setProxyUrl(_:)
        // hook and applied before configure so the SDK's initial requests target it.
        if let devProxyUrl = (Bundle.main.object(forInfoDictionaryKey: "APPSTACK_DEV_PROXY_URL") as? String)
            .flatMap({ $0.isEmpty ? nil : $0 }) {
            AppstackAttributionSdk.shared.setProxyUrl(devProxyUrl)
        }

        AppstackAttributionSdk.shared.configure(
            apiKey: apiKey,
            logLevel: logLevelEnum,
            customerUserId: customerUserId,
            wrapperVersion: AppstackUnityBridge.wrapperVersion
        )
    }

    @objc public static func sendEvent(_ eventType: String?, eventName: String?, parameters: NSDictionary?) {
        let finalEventType: EventType
        let finalEventName: String?
        if let eventTypeString = eventType, !eventTypeString.isEmpty {
            if let enumEvent = eventTypeFromString(eventTypeString) {
                finalEventType = enumEvent
                finalEventName = (enumEvent == .CUSTOM) ? eventName : nil
            } else {
                finalEventType = .CUSTOM
                finalEventName = eventName ?? eventTypeString
            }
        } else if let eventNameString = eventName, !eventNameString.isEmpty {
            if let enumEvent = eventTypeFromString(eventNameString) {
                finalEventType = enumEvent
                finalEventName = (enumEvent == .CUSTOM) ? eventNameString : nil
            } else {
                finalEventType = .CUSTOM
                finalEventName = eventNameString
            }
        } else {
            finalEventType = .CUSTOM
            finalEventName = "UNKNOWN_EVENT"
        }
        let parametersDict = parameters as? [String: Any]
        AppstackAttributionSdk.shared.sendEvent(
            event: finalEventType,
            name: finalEventName,
            parameters: parametersDict
        )
    }

    @objc public static func enableAppleAdsAttribution() {
        AppstackASAAttribution.shared.enableAppleAdsAttribution()
    }

    @objc public static func getAppstackId() -> String {
        return AppstackAttributionSdk.shared.getAppstackId() ?? ""
    }

    @objc public static func isSdkDisabled() -> Bool {
        return AppstackAttributionSdk.shared.isSdkDisabled()
    }

    @objc(getAttributionParamsWithCompletion:)
    public static func getAttributionParams(completion: @escaping (NSDictionary?, NSError?) -> Void) {
        Task {
            let params = await AppstackAttributionSdk.shared.getAttributionParams()
            completion(params as NSDictionary? ?? [:], nil)
        }
    }
}
