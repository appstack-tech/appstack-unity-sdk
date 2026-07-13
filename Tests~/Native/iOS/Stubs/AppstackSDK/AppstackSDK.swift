import Foundation

public enum LogLevel: Equatable {
    case debug
    case info
    case error
}

public enum EventType: String, CaseIterable {
    case INSTALL
    case LOGIN
    case SIGN_UP
    case REGISTER
    case PURCHASE
    case ADD_TO_CART
    case ADD_TO_WISHLIST
    case INITIATE_CHECKOUT
    case START_TRIAL
    case SUBSCRIBE
    case LEVEL_START
    case LEVEL_COMPLETE
    case TUTORIAL_COMPLETE
    case SEARCH
    case VIEW_ITEM
    case VIEW_CONTENT
    case SHARE
    case CUSTOM
}

public final class AppstackAttributionSdk {
    public struct ConfigureCall {
        public let apiKey: String
        public let logLevel: LogLevel
        public let customerUserId: String?
        public let wrapperVersion: String?
    }

    public struct EventCall {
        public let event: EventType
        public let name: String?
        public let parameters: [String: Any]?
    }

    public static let shared = AppstackAttributionSdk()

    public private(set) var configureCall: ConfigureCall?
    public private(set) var eventCall: EventCall?
    public private(set) var proxyUrl: String?
    public var appstackId: String?
    public var sdkDisabled = false
    public var attributionResult: [String: Any]?
    public private(set) var attributionCalls = 0

    private init() {}

    public func reset() {
        configureCall = nil
        eventCall = nil
        proxyUrl = nil
        appstackId = nil
        sdkDisabled = false
        attributionResult = nil
        attributionCalls = 0
    }

    @_spi(AppstackInternal)
    public func setProxyUrl(_ value: String) {
        proxyUrl = value
    }

    @_spi(AppstackInternal)
    public func configure(
        apiKey: String,
        logLevel: LogLevel,
        customerUserId: String?,
        wrapperVersion: String?
    ) {
        configureCall = ConfigureCall(
            apiKey: apiKey,
            logLevel: logLevel,
            customerUserId: customerUserId,
            wrapperVersion: wrapperVersion
        )
    }

    public func sendEvent(
        event: EventType,
        name: String?,
        parameters: [String: Any]?
    ) {
        eventCall = EventCall(event: event, name: name, parameters: parameters)
    }

    public func getAppstackId() -> String? {
        appstackId
    }

    public func isSdkDisabled() -> Bool {
        sdkDisabled
    }

    public func getAttributionParams() async -> [String: Any]? {
        attributionCalls += 1
        return attributionResult
    }
}

public final class AppstackASAAttribution {
    public static let shared = AppstackASAAttribution()

    public private(set) var enableCalls = 0

    private init() {}

    public func reset() {
        enableCalls = 0
    }

    public func enableAppleAdsAttribution() {
        enableCalls += 1
    }
}
