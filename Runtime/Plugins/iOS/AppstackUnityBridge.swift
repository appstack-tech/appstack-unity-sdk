import Foundation
@_spi(AppstackInternal) @preconcurrency import AppstackSDK

/// May run on any thread; C# posts to the captured context when available.
public typealias AppstackUnityAttributionCallback = @convention(c) (
    Int32,
    UnsafeMutablePointer<CChar>?,
    UnsafeMutablePointer<CChar>?
) -> Void

@_cdecl("AppstackUnityConfigure")
public func AppstackUnityConfigure(
    _ apiKeyPointer: UnsafePointer<CChar>?,
    _ logLevel: Int32,
    _ customerUserIdPointer: UnsafePointer<CChar>?,
    _ wrapperVersionPointer: UnsafePointer<CChar>?
) {
    guard let apiKey = string(from: apiKeyPointer), !apiKey.isEmpty else {
        return
    }

    if let proxyUrl = Bundle.main.object(
        forInfoDictionaryKey: "APPSTACK_DEV_PROXY_URL"
    ) as? String, !proxyUrl.isEmpty {
        AppstackAttributionSdk.shared.setProxyUrl(proxyUrl)
    }

    AppstackAttributionSdk.shared.configure(
        apiKey: apiKey,
        logLevel: nativeLogLevel(from: logLevel),
        customerUserId: string(from: customerUserIdPointer),
        wrapperVersion: string(from: wrapperVersionPointer)
    )
}

@_cdecl("AppstackUnitySendEvent")
public func AppstackUnitySendEvent(
    _ eventTypePointer: UnsafePointer<CChar>?,
    _ eventNamePointer: UnsafePointer<CChar>?,
    _ parametersJsonPointer: UnsafePointer<CChar>?
) {
    let eventTypeString = string(from: eventTypePointer) ?? "CUSTOM"
    let normalizedEventType = eventTypeString.uppercased()
    let eventType = EventType(rawValue: normalizedEventType) ?? .CUSTOM
    let suppliedName = string(from: eventNamePointer)
    let eventName = eventType == .CUSTOM
        ? (suppliedName ?? (normalizedEventType == "CUSTOM" ? nil : eventTypeString))
        : nil

    AppstackAttributionSdk.shared.sendEvent(
        event: eventType,
        name: eventName,
        parameters: dictionary(fromJson: string(from: parametersJsonPointer))
    )
}

@_cdecl("AppstackUnityEnableAppleAdsAttribution")
public func AppstackUnityEnableAppleAdsAttribution() {
    AppstackASAAttribution.shared.enableAppleAdsAttribution()
}

@_cdecl("AppstackUnityGetAppstackId")
public func AppstackUnityGetAppstackId() -> UnsafeMutablePointer<CChar>? {
    retainedCString(AppstackAttributionSdk.shared.getAppstackId())
}

@_cdecl("AppstackUnityIsSdkDisabled")
public func AppstackUnityIsSdkDisabled() -> Int32 {
    AppstackAttributionSdk.shared.isSdkDisabled() ? 1 : 0
}

@_cdecl("AppstackUnityGetAttributionParams")
public func AppstackUnityGetAttributionParams(
    _ requestId: Int32,
    _ callback: AppstackUnityAttributionCallback?
) {
    Task {
        let parameters = await AppstackAttributionSdk.shared.getAttributionParams() ?? [:]
        guard let json = jsonString(from: parameters) else {
            callback?(
                requestId,
                nil,
                retainedCString("Unable to serialize attribution parameters.")
            )
            return
        }

        callback?(requestId, retainedCString(json), nil)
    }
}

@_cdecl("AppstackUnityFreeCString")
public func AppstackUnityFreeCString(_ pointer: UnsafeMutablePointer<CChar>?) {
    guard let pointer else { return }
    free(pointer)
}

private func nativeLogLevel(from value: Int32) -> LogLevel {
    switch value {
    case 0:
        return .debug
    case 1:
        return .info
    case 2, 3:
        return .error
    default:
        return .info
    }
}

private func string(from pointer: UnsafePointer<CChar>?) -> String? {
    guard let pointer else { return nil }
    let value = String(cString: pointer)
    return value.isEmpty ? nil : value
}

private func dictionary(fromJson json: String?) -> [String: Any]? {
    guard let json, let data = json.data(using: .utf8), !json.isEmpty else {
        return nil
    }

    return (try? JSONSerialization.jsonObject(with: data)) as? [String: Any]
}

private func jsonString(from dictionary: [String: Any]) -> String? {
    guard JSONSerialization.isValidJSONObject(dictionary),
          let data = try? JSONSerialization.data(withJSONObject: dictionary) else {
        return nil
    }

    return String(data: data, encoding: .utf8)
}

private func retainedCString(_ value: String?) -> UnsafeMutablePointer<CChar>? {
    guard let value else { return nil }
    return strdup(value)
}
