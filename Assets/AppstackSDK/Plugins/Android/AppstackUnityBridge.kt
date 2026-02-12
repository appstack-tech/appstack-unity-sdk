package com.appstack.unity

import android.util.Log
import com.appstack.attribution.AppstackAttributionSdk
import com.appstack.attribution.EventType
import com.appstack.attribution.LogLevel
import com.unity3d.player.UnityPlayer
import org.json.JSONObject

/**
 * Unity bridge for Appstack Attribution SDK on Android.
 * Called from C# via AndroidJavaClass("com.appstack.unity.AppstackUnityBridge").
 */
object AppstackUnityBridge {

    private const val TAG = "AppstackUnityBridge"

    @JvmStatic
    fun configure(
        apiKey: String,
        isDebug: Boolean,
        endpointBaseUrl: String,
        logLevel: Int,
        customerUserId: String
    ) {
        val context = UnityPlayer.currentActivity?.applicationContext
            ?: run {
                Log.e(TAG, "No application context available")
                return
            }
        if (apiKey.isBlank()) {
            Log.e(TAG, "API key is required")
            return
        }
        val logLevelEnum = when (logLevel) {
            0 -> LogLevel.DEBUG
            1 -> LogLevel.INFO
            2 -> LogLevel.WARN
            3 -> LogLevel.ERROR
            else -> LogLevel.INFO
        }
        val endpoint = if (endpointBaseUrl.isBlank()) null else endpointBaseUrl
        val customerId = if (customerUserId.isBlank()) null else customerUserId
        if (endpoint != null) {
            AppstackAttributionSdk.configure(
                context = context,
                apiKey = apiKey,
                isDebug = isDebug,
                endpointBaseUrl = endpoint,
                logLevel = logLevelEnum,
                customerUserId = customerId
            )
        } else {
            AppstackAttributionSdk.configure(
                context = context,
                apiKey = apiKey,
                isDebug = isDebug,
                logLevel = logLevelEnum,
                customerUserId = customerId
            )
        }
    }

    @JvmStatic
    fun sendEvent(eventType: String, eventName: String, parametersJson: String) {
        val eventTypeEnum = stringToEventType(eventType) ?: EventType.CUSTOM
        val name = if (eventTypeEnum == EventType.CUSTOM && eventName.isNotBlank()) eventName else null
        val parameters = parseParameters(parametersJson)
        AppstackAttributionSdk.sendEvent(event = eventTypeEnum, name = name, parameters = parameters)
    }

    @JvmStatic
    fun enableAppleAdsAttribution() {
        // No-op on Android
    }

    @JvmStatic
    fun getAppstackId(): String {
        return AppstackAttributionSdk.getAppstackId() ?: ""
    }

    @JvmStatic
    fun isSdkDisabled(): Boolean {
        return AppstackAttributionSdk.isSdkDisabled()
    }

    @JvmStatic
    fun getAttributionParams(): String {
        val params = AppstackAttributionSdk.getAttributionParams(rawReferrer = null)
        return try {
            JSONObject(params).toString()
        } catch (e: Exception) {
            Log.e(TAG, "getAttributionParams serialization failed", e)
            "{}"
        }
    }

    private fun stringToEventType(string: String): EventType? = when (string.uppercase()) {
        "INSTALL" -> EventType.INSTALL
        "LOGIN" -> EventType.LOGIN
        "SIGN_UP" -> EventType.SIGN_UP
        "REGISTER" -> EventType.REGISTER
        "PURCHASE" -> EventType.PURCHASE
        "ADD_TO_CART" -> EventType.ADD_TO_CART
        "ADD_TO_WISHLIST" -> EventType.ADD_TO_WISHLIST
        "INITIATE_CHECKOUT" -> EventType.INITIATE_CHECKOUT
        "START_TRIAL" -> EventType.START_TRIAL
        "SUBSCRIBE" -> EventType.SUBSCRIBE
        "LEVEL_START" -> EventType.LEVEL_START
        "LEVEL_COMPLETE" -> EventType.LEVEL_COMPLETE
        "TUTORIAL_COMPLETE" -> EventType.TUTORIAL_COMPLETE
        "SEARCH" -> EventType.SEARCH
        "VIEW_ITEM" -> EventType.VIEW_ITEM
        "VIEW_CONTENT" -> EventType.VIEW_CONTENT
        "SHARE" -> EventType.SHARE
        "CUSTOM" -> EventType.CUSTOM
        else -> null
    }

    private fun parseParameters(json: String): Map<String, Any>? {
        if (json.isBlank() || json == "{}") return null
        return try {
            val obj = JSONObject(json)
            val map = mutableMapOf<String, Any>()
            obj.keys().asSequence().forEach { key ->
                when (val v = obj.get(key)) {
                    is String -> map[key] = v
                    is Number -> map[key] = v
                    is Boolean -> map[key] = v
                    else -> map[key] = v.toString()
                }
            }
            map
        } catch (e: Exception) {
            Log.w(TAG, "parseParameters failed: $json", e)
            null
        }
    }
}
