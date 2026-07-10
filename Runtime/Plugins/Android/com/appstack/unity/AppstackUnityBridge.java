package com.appstack.unity;

import com.appstack.attribution.AppstackAttributionSdk;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import kotlin.ResultKt;
import kotlin.coroutines.Continuation;
import kotlin.coroutines.CoroutineContext;
import kotlin.coroutines.EmptyCoroutineContext;
import kotlin.coroutines.intrinsics.IntrinsicsKt;
import org.json.JSONObject;

/** Java-only adapter for the Android SDK's suspending attribution API. */
public final class AppstackUnityBridge {
    public interface AttributionParamsCallback {
        void onResult(int requestId, String json, String error);
    }

    private AppstackUnityBridge() {
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
    public static Map<String, Object> parametersFromJson(String json) {
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
}
