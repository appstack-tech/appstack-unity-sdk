import Foundation
import AppstackSDK

@objc(AppstackUnityBridge)
public class AppstackUnityBridge: NSObject {

    private static func eventTypeFromString(_ string: String) -> EventType? {
        return EventType.allCases.first { $0.rawValue == string.uppercased() }
    }

    @objc public static func configure(apiKey: String, isDebug: Bool, endpointBaseUrl: String?, logLevel: Int, customerUserId: String?) {
        let logLevelEnum: LogLevel
        switch logLevel {
        case 0: logLevelEnum = .off
        case 1: logLevelEnum = .error
        case 2: logLevelEnum = .debug
        case 3: logLevelEnum = .info
        default: logLevelEnum = .info
        }
        AppstackAttributionSdk.shared.configure(
            apiKey: apiKey,
            isDebug: isDebug,
            endpointBaseUrl: endpointBaseUrl,
            logLevel: logLevelEnum,
            customerUserId: customerUserId
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
