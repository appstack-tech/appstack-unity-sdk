using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Appstack.Tests
{
    public sealed class AppstackNativeBridgeSeamTests
    {
        private RecordingNativeBridge bridge;
        private IDisposable bridgeOverride;

        [SetUp]
        public void SetUp()
        {
            bridge = new RecordingNativeBridge();
            bridgeOverride = AppstackSDKNative.OverrideBridgeForTesting(bridge);
        }

        [TearDown]
        public void TearDown()
        {
            bridgeOverride.Dispose();
        }

        [Test]
        public void ConfigureNormalizesAndForwardsArguments()
        {
            AppstackSDK.Configure("  api-key  ", 2, "  customer-123  ");

            Assert.That(bridge.ApiKey, Is.EqualTo("api-key"));
            Assert.That(bridge.LogLevel, Is.EqualTo(2));
            Assert.That(bridge.CustomerUserId, Is.EqualTo("customer-123"));
        }

        [Test]
        public void SendEventForwardsSerializedParameters()
        {
            AppstackSDK.SendEvent(
                EventType.CUSTOM,
                "sample_started",
                new Dictionary<string, object> { { "revenue", 12.5 } });

            Assert.That(bridge.EventType, Is.EqualTo("CUSTOM"));
            Assert.That(bridge.EventName, Is.EqualTo("sample_started"));
            Assert.That(bridge.ParametersJson, Is.EqualTo("{\"revenue\":12.5}"));
        }

        [Test]
        public void PublicQueriesAndAppleAdsDelegateToReplacementBridge()
        {
            bridge.AppstackId = "appstack-id";
            bridge.SdkDisabled = true;

            AppstackSDK.EnableAppleAdsAttribution();

            Assert.That(AppstackSDK.GetAppstackId(), Is.EqualTo("appstack-id"));
            Assert.That(AppstackSDK.IsSdkDisabled(), Is.True);
            Assert.That(bridge.EnableAppleAdsCalls, Is.EqualTo(1));
        }

        [Test]
        public void AttributionCallbacksDelegateToReplacementBridge()
        {
            var expected = new Dictionary<string, object> { { "campaign", "summer" } };
            bridge.AttributionResult = expected;
            Dictionary<string, object> actual = null;

            AppstackSDK.GetAttributionParams(result => actual = result);

            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void DisposingOverrideRestoresPreviousBridge()
        {
            bridge.AppstackId = "outer";
            var inner = new RecordingNativeBridge { AppstackId = "inner" };

            using (AppstackSDKNative.OverrideBridgeForTesting(inner))
            {
                Assert.That(AppstackSDK.GetAppstackId(), Is.EqualTo("inner"));
            }

            Assert.That(AppstackSDK.GetAppstackId(), Is.EqualTo("outer"));
        }

        private sealed class RecordingNativeBridge : IAppstackNativeBridge
        {
            public bool ReportsConfigurationStatus { get; set; }
            public string ApiKey { get; private set; }
            public int LogLevel { get; private set; }
            public string CustomerUserId { get; private set; }
            public string EventType { get; private set; }
            public string EventName { get; private set; }
            public string ParametersJson { get; private set; }
            public int EnableAppleAdsCalls { get; private set; }
            public string AppstackId { get; set; }
            public bool SdkDisabled { get; set; }
            public Dictionary<string, object> AttributionResult { get; set; }

            public void Configure(string apiKey, int logLevel, string customerUserId)
            {
                ApiKey = apiKey;
                LogLevel = logLevel;
                CustomerUserId = customerUserId;
            }

            public void SendEvent(string eventType, string eventName, string parametersJson)
            {
                EventType = eventType;
                EventName = eventName;
                ParametersJson = parametersJson;
            }

            public void EnableAppleAdsAttribution()
            {
                EnableAppleAdsCalls++;
            }

            public string GetAppstackId()
            {
                return AppstackId;
            }

            public bool IsSdkDisabled()
            {
                return SdkDisabled;
            }

            public void GetAttributionParams(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError)
            {
                onSuccess(AttributionResult);
            }
        }
    }
}
