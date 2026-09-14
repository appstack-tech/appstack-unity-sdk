package com.appstack.attribution

import android.content.Context
import android.net.Uri
import kotlin.coroutines.Continuation
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlin.coroutines.suspendCoroutine

enum class LogLevel {
    DEBUG,
    INFO,
    WARN,
    ERROR,
}

enum class EventType {
    INSTALL,
    LOGIN,
    SIGN_UP,
    REGISTER,
    PURCHASE,
    ADD_TO_CART,
    ADD_TO_WISHLIST,
    INITIATE_CHECKOUT,
    START_TRIAL,
    SUBSCRIBE,
    LEVEL_START,
    LEVEL_COMPLETE,
    TUTORIAL_COMPLETE,
    SEARCH,
    VIEW_ITEM,
    VIEW_CONTENT,
    SHARE,
    CUSTOM,
}

class LinkOptions(val allowedHosts: Set<String>? = null)

data class AppLinkResult(
    val deeplinkId: String?,
    val queryParams: Map<String, String>,
    val uri: Uri,
)

object AppstackAttributionSdk {
    enum class AttributionMode {
        IMMEDIATE,
        SUSPENDED,
        FAILURE,
    }

    enum class DeleteMode {
        IMMEDIATE,
        SUSPENDED,
        FAILURE,
    }

    data class ConfigureCall(
        val context: Context,
        val apiKey: String,
        val wrapperVersion: String,
        val logLevel: LogLevel,
        val customerUserId: String?,
    )

    data class EventCall(
        val eventType: EventType,
        val eventName: String?,
        val parameters: Map<String, Any>?,
    )

    @JvmStatic
    var configureCall: ConfigureCall? = null
        private set

    /** Every setCustomerUserId argument received, in order — nulls included. */
    @JvmStatic
    val customerUserIdCalls: MutableList<String?> = mutableListOf()

    @JvmStatic
    var eventCall: EventCall? = null
        private set

    @JvmStatic
    var proxyUrl: String? = null
        private set

    @JvmStatic
    var recordedAppstackId: String? = null

    @JvmStatic
    var sdkDisabled: Boolean = false

    @JvmStatic
    var attributionMode: AttributionMode = AttributionMode.IMMEDIATE

    @JvmStatic
    var attributionResult: Map<String, String> = emptyMap()

    @JvmStatic
    var attributionCalls: Int = 0
        private set

    @JvmStatic
    var deleteMode: DeleteMode = DeleteMode.IMMEDIATE

    @JvmStatic
    var deleteCalls: Int = 0
        private set

    private var attributionContinuation: Continuation<Map<String, String>>? = null
    private var deleteContinuation: Continuation<Unit>? = null

    @JvmStatic
    var appLinkResult: AppLinkResult? = null

    @JvmStatic
    fun reset() {
        configureCall = null
        customerUserIdCalls.clear()
        eventCall = null
        proxyUrl = null
        recordedAppstackId = null
        sdkDisabled = false
        attributionMode = AttributionMode.IMMEDIATE
        attributionResult = emptyMap()
        attributionCalls = 0
        attributionContinuation = null
        deleteMode = DeleteMode.IMMEDIATE
        deleteCalls = 0
        deleteContinuation = null
        appLinkResult = null
    }

    @JvmStatic
    fun setProxyUrl(value: String) {
        proxyUrl = value
    }

    @JvmStatic
    fun configureWrapper(
        context: Context,
        apiKey: String,
        wrapperVersion: String,
        logLevel: LogLevel,
        listener: Any?,
        customerUserId: String?,
    ) {
        configureCall = ConfigureCall(
            context,
            apiKey,
            wrapperVersion,
            logLevel,
            customerUserId,
        )
    }

    @JvmStatic
    fun setCustomerUserId(customerUserId: String?) {
        customerUserIdCalls.add(customerUserId)
    }

    @JvmStatic
    suspend fun deleteUserData() {
        deleteCalls++
        when (deleteMode) {
            DeleteMode.IMMEDIATE -> Unit
            DeleteMode.FAILURE -> throw IllegalStateException("native deletion failure")
            DeleteMode.SUSPENDED -> suspendCoroutine { deleteContinuation = it }
        }
    }

    @JvmStatic
    fun completeDeletion() {
        val continuation = deleteContinuation ?: error("No deletion is suspended.")
        deleteContinuation = null
        continuation.resume(Unit)
    }

    @JvmStatic
    fun failDeletion(error: Throwable = IllegalStateException("suspended deletion failure")) {
        val continuation = deleteContinuation ?: kotlin.error("No deletion is suspended.")
        deleteContinuation = null
        continuation.resumeWithException(error)
    }

    @JvmStatic
    fun sendEvent(
        eventType: EventType,
        eventName: String?,
        parameters: Map<String, Any>?,
    ) {
        eventCall = EventCall(eventType, eventName, parameters)
    }

    @JvmStatic
    fun getAppstackId(): String? = recordedAppstackId

    @JvmStatic
    fun isSdkDisabled(): Boolean = sdkDisabled

    @JvmStatic
    fun handleAppLink(uri: Uri, options: LinkOptions): AppLinkResult? = appLinkResult

    @JvmStatic
    suspend fun awaitAttributionParams(rawReferrer: String?): Map<String, String> {
        attributionCalls++
        return when (attributionMode) {
            AttributionMode.IMMEDIATE -> attributionResult
            AttributionMode.FAILURE -> throw IllegalStateException("native attribution failure")
            AttributionMode.SUSPENDED -> suspendCoroutine { attributionContinuation = it }
        }
    }

    @JvmStatic
    fun completeAttribution(result: Map<String, String> = attributionResult) {
        val continuation = attributionContinuation
            ?: error("No attribution continuation is suspended.")
        attributionContinuation = null
        continuation.resume(result)
    }

    @JvmStatic
    fun failAttribution(error: Throwable = IllegalStateException("suspended failure")) {
        val continuation = attributionContinuation
            ?: kotlin.error("No attribution continuation is suspended.")
        attributionContinuation = null
        continuation.resumeWithException(error)
    }
}
