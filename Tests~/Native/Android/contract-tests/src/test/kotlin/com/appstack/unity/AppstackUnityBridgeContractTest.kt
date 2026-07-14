package com.appstack.unity

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import android.os.Bundle
import com.appstack.attribution.AppstackAttributionSdk
import com.appstack.attribution.EventType
import com.appstack.attribution.LogLevel
import org.json.JSONObject
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.Arguments
import org.junit.jupiter.params.provider.MethodSource
import java.util.concurrent.atomic.AtomicInteger
import java.util.stream.Stream

class AppstackUnityBridgeContractTest {
    private lateinit var context: RecordingContext

    @BeforeEach
    fun setUp() {
        AppstackAttributionSdk.reset()
        context = RecordingContext()
    }

    @AfterEach
    fun tearDown() {
        AppstackAttributionSdk.reset()
    }

    @ParameterizedTest
    @MethodSource("logLevels")
    fun `configure maps log levels and forwards wrapper contract`(
        unityLevel: Int,
        nativeLevel: LogLevel,
    ) {
        AppstackUnityBridge.configure(
            context,
            "api-key",
            "unity-1.0.0",
            unityLevel,
            "customer-123",
        )

        val call = requireNotNull(AppstackAttributionSdk.configureCall)
        assertEquals("api-key", call.apiKey)
        assertEquals("unity-1.0.0", call.wrapperVersion)
        assertEquals(nativeLevel, call.logLevel)
        assertEquals("customer-123", call.customerUserId)
        assertTrue(call.context === context)
    }

    @Test
    fun `configure normalizes blank customer id and applies development proxy`() {
        context.metadata.putString("APPSTACK_DEV_PROXY_URL", "http://127.0.0.1:9090")

        AppstackUnityBridge.configure(
            context,
            "api-key",
            "unity-1.0.0",
            1,
            "   ",
        )

        assertNull(AppstackAttributionSdk.configureCall?.customerUserId)
        assertEquals("http://127.0.0.1:9090", AppstackAttributionSdk.proxyUrl)
    }

    @ParameterizedTest
    @MethodSource("knownEvents")
    fun `sendEvent maps every known event`(eventType: EventType) {
        AppstackUnityBridge.sendEvent(eventType.name, "supplied", "{}")

        val call = requireNotNull(AppstackAttributionSdk.eventCall)
        assertEquals(eventType, call.eventType)
        assertEquals(if (eventType == EventType.CUSTOM) "supplied" else null, call.eventName)
    }

    @Test
    fun `sendEvent maps unknown event to custom and preserves custom name`() {
        AppstackUnityBridge.sendEvent("future_event", "future_name", null)

        val call = requireNotNull(AppstackAttributionSdk.eventCall)
        assertEquals(EventType.CUSTOM, call.eventType)
        assertEquals("future_name", call.eventName)
        assertNull(call.parameters)
    }

    @Test
    fun `sendEvent converts JSON to native maps lists primitives and nulls`() {
        AppstackUnityBridge.sendEvent(
            "PURCHASE",
            "ignored",
            """{"revenue":12.5,"currency":"USD","empty":null,"items":[1,{"active":true}]}""",
        )

        val parameters = requireNotNull(AppstackAttributionSdk.eventCall?.parameters)
        assertEquals(12.5, (parameters["revenue"] as Number).toDouble())
        assertEquals("USD", parameters["currency"])
        assertTrue(parameters.containsKey("empty"))
        assertNull(parameters["empty"])
        val items = parameters["items"] as List<*>
        assertEquals(1, (items[0] as Number).toInt())
        assertEquals(true, (items[1] as Map<*, *>)["active"])
    }

    @Test
    fun `invalid parameter JSON becomes absent parameters`() {
        AppstackUnityBridge.sendEvent("LOGIN", null, "not-json")

        assertNull(AppstackAttributionSdk.eventCall?.parameters)
    }

    @Test
    fun `immediate attribution completion returns matching request id and JSON`() {
        AppstackAttributionSdk.attributionResult = mapOf("campaign" to "summer")
        val callbacks = AtomicInteger()
        var callbackId = 0
        var callbackJson: String? = null
        var callbackError: String? = null

        AppstackUnityBridge.awaitAttributionParams(41) { id, json, error ->
            callbacks.incrementAndGet()
            callbackId = id
            callbackJson = json
            callbackError = error
        }

        assertEquals(1, callbacks.get())
        assertEquals(41, callbackId)
        assertEquals("summer", JSONObject(callbackJson).getString("campaign"))
        assertNull(callbackError)
    }

    @Test
    fun `suspended attribution completion resumes once with matching request`() {
        AppstackAttributionSdk.attributionMode =
            AppstackAttributionSdk.AttributionMode.SUSPENDED
        val callbacks = AtomicInteger()
        var callbackId = 0
        var callbackJson: String? = null

        AppstackUnityBridge.awaitAttributionParams(73) { id, json, error ->
            assertNull(error)
            callbacks.incrementAndGet()
            callbackId = id
            callbackJson = json
        }

        assertEquals(0, callbacks.get())
        AppstackAttributionSdk.completeAttribution(mapOf("source" to "organic"))

        assertEquals(1, callbacks.get())
        assertEquals(73, callbackId)
        assertEquals("organic", JSONObject(callbackJson).getString("source"))
    }

    @Test
    fun `immediate and suspended failures reach error callback`() {
        AppstackAttributionSdk.attributionMode =
            AppstackAttributionSdk.AttributionMode.FAILURE
        var immediateError: String? = null
        AppstackUnityBridge.awaitAttributionParams(1) { _, _, error -> immediateError = error }
        assertTrue(immediateError?.contains("native attribution failure") == true)

        AppstackAttributionSdk.attributionMode =
            AppstackAttributionSdk.AttributionMode.SUSPENDED
        var suspendedError: String? = null
        AppstackUnityBridge.awaitAttributionParams(2) { _, _, error -> suspendedError = error }
        AppstackAttributionSdk.failAttribution()
        assertTrue(suspendedError?.contains("suspended failure") == true)
    }

    @Test
    fun `null attribution callback performs no native work`() {
        AppstackAttributionSdk.attributionMode =
            AppstackAttributionSdk.AttributionMode.SUSPENDED

        AppstackUnityBridge.awaitAttributionParams(1, null)

        assertEquals(0, AppstackAttributionSdk.attributionCalls)
    }

    @Test
    fun `synchronous getters forward native values`() {
        AppstackAttributionSdk.recordedAppstackId = "appstack-id"
        AppstackAttributionSdk.sdkDisabled = true

        assertEquals("appstack-id", AppstackUnityBridge.getAppstackId())
        assertTrue(AppstackUnityBridge.isSdkDisabled())
    }

    private class RecordingContext : Context() {
        val metadata = Bundle()
        private val applicationInfo = ApplicationInfo().also { it.metaData = metadata }
        private val packageManager = object : PackageManager() {
            override fun getApplicationInfo(packageName: String, flags: Int): ApplicationInfo {
                assertEquals("com.appstack.contract", packageName)
                assertEquals(GET_META_DATA, flags)
                return applicationInfo
            }
        }

        override fun getPackageManager(): PackageManager = packageManager

        override fun getPackageName(): String = "com.appstack.contract"
    }

    companion object {
        @JvmStatic
        fun logLevels(): Stream<Arguments> = Stream.of(
            Arguments.of(0, LogLevel.DEBUG),
            Arguments.of(1, LogLevel.INFO),
            Arguments.of(2, LogLevel.WARN),
            Arguments.of(3, LogLevel.ERROR),
            Arguments.of(-1, LogLevel.INFO),
            Arguments.of(4, LogLevel.INFO),
        )

        @JvmStatic
        fun knownEvents(): Stream<EventType> = EventType.entries.stream()
    }
}
