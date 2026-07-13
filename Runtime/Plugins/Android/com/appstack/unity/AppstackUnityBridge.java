package com.appstack.unity;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import com.appstack.attribution.AppstackAttributionSdk;
import com.appstack.attribution.EventType;
import com.appstack.attribution.LogLevel;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import kotlin.ResultKt;
import kotlin.coroutines.Continuation;
import kotlin.coroutines.CoroutineContext;
import kotlin.coroutines.EmptyCoroutineContext;
import kotlin.coroutines.intrinsics.IntrinsicsKt;
import org.json.JSONObject;

/** Stable Java boundary between Unity's JNI API and the native Android SDK. */
public final class AppstackUnityBridge {
    private static final String WRAPPER_VERSION = "unity-1.0.0";
    private static final String DEV_PROXY_URL_KEY = "APPSTACK_DEV_PROXY_URL";

    public interface AttributionParamsCallback {
        void onResult(int requestId, String json, String error);
    }

    private AppstackUnityBridge() {
    }

    public static void configure(
            Context context,
            String apiKey,
            int logLevel,
            String customerUserId) {
        String proxyUrl = readDevProxyUrl(context);
        if (proxyUrl != null && !proxyUrl.trim().isEmpty()) {
            AppstackAttributionSdk.setProxyUrl(proxyUrl);
        }

        // The pinned SDK exposes this wrapper-only API. Do not fall back to the
        // public configure method because that would omit wrapper attribution.
        AppstackAttributionSdk.configureWrapper(
                context,
                apiKey,
                WRAPPER_VERSION,
                nativeLogLevel(logLevel),
                null,
                emptyToNull(customerUserId));
    }

    public static void sendEvent(
            String eventType,
            String eventName,
            String parametersJson) {
        AppstackAttributionSdk.sendEvent(
                nativeEventType(eventType),
                emptyToNull(eventName),
                parametersFromJson(parametersJson));
    }

    public static String getAppstackId() {
        return AppstackAttributionSdk.getAppstackId();
    }

    public static boolean isSdkDisabled() {
        return AppstackAttributionSdk.isSdkDisabled();
    }

    @SuppressWarnings("unchecked")
    public static void awaitAttributionParams(
            final int requestId,
            final AttributionParamsCallback callback) {
        if (callback == null) {
            return;
        }

        try {
            Object result = AppstackAttributionSdk.awaitAttributionParams(
                    null,
                    new Continuation<Map<String, String>>() {
                        @Override
                        public CoroutineContext getContext() {
                            return EmptyCoroutineContext.INSTANCE;
                        }

                        @Override
                        public void resumeWith(Object result) {
                            try {
                                ResultKt.throwOnFailure(result);
                                callback.onResult(
                                        requestId,
                                        toJson((Map<String, String>) result),
                                        null);
                            } catch (Throwable throwable) {
                                callback.onResult(requestId, null, errorMessage(throwable));
                            }
                        }
                    });

            if (result != IntrinsicsKt.getCOROUTINE_SUSPENDED()) {
                callback.onResult(requestId, toJson((Map<String, String>) result), null);
            }
        } catch (Throwable throwable) {
            callback.onResult(requestId, null, errorMessage(throwable));
        }
    }

    /** Converts Unity's JSON boundary to the map expected by the native SDK. */
    private static Map<String, Object> parametersFromJson(String json) {
        if (json == null || json.trim().isEmpty() || "{}".equals(json)) {
            return null;
        }

        try {
            JSONObject object = new JSONObject(json);
            Map<String, Object> result = new HashMap<>();
            Iterator<String> keys = object.keys();
            while (keys.hasNext()) {
                String key = keys.next();
                Object value = object.get(key);
                if (value != JSONObject.NULL) {
                    result.put(key, value);
                }
            }
            return result;
        } catch (Throwable ignored) {
            return null;
        }
    }

    private static String toJson(Map<String, String> params) {
        if (params == null || params.isEmpty()) {
            return "{}";
        }

        return new JSONObject(params).toString();
    }

    private static String errorMessage(Throwable throwable) {
        String message = throwable.getMessage();
        return message == null || message.isEmpty()
                ? throwable.getClass().getSimpleName()
                : message;
    }

    private static LogLevel nativeLogLevel(int logLevel) {
        switch (logLevel) {
            case 0:
                return LogLevel.DEBUG;
            case 2:
                return LogLevel.WARN;
            case 3:
                return LogLevel.ERROR;
            case 1:
            default:
                return LogLevel.INFO;
        }
    }

    private static EventType nativeEventType(String eventType) {
        if (eventType == null || eventType.trim().isEmpty()) {
            return EventType.CUSTOM;
        }

        try {
            return EventType.valueOf(eventType);
        } catch (IllegalArgumentException ignored) {
            return EventType.CUSTOM;
        }
    }

    private static String readDevProxyUrl(Context context) {
        try {
            PackageManager packageManager = context.getPackageManager();
            ApplicationInfo applicationInfo = packageManager.getApplicationInfo(
                    context.getPackageName(),
                    PackageManager.GET_META_DATA);
            Bundle metadata = applicationInfo.metaData;
            return metadata == null ? null : metadata.getString(DEV_PROXY_URL_KEY);
        } catch (Throwable ignored) {
            return null;
        }
    }

    private static String emptyToNull(String value) {
        return value == null || value.trim().isEmpty() ? null : value;
    }
}
