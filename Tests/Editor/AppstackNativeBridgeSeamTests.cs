using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Appstack.Tests
{
    public sealed class AppstackNativeBridgeSeamTests
    {
        private RecordingNativeBridge bridge;
        private IDisposable bridgeOverride;
        private ILogHandler previousLogHandler;
        private RecordingLogHandler logHandler;

        private static IEnumerable AllEventTypes => Enum.GetValues(typeof(EventType));

        [SetUp]
        public void SetUp()
        {
            bridge = new RecordingNativeBridge();
            bridgeOverride = AppstackSDKNative.OverrideBridgeForTesting(bridge);
            previousLogHandler = Debug.unityLogger.logHandler;
            logHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = logHandler;
        }

        [TearDown]
        public void TearDown()
        {
            Debug.unityLogger.logHandler = previousLogHandler;
            bridgeOverride.Dispose();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConfigureRejectsMissingApiKey(string apiKey)
        {
            Assert.Throws<ArgumentException>(() => AppstackSDK.Configure(apiKey));
            Assert.That(bridge.ConfigureCalls, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConfigureAcceptsDocumentedLogLevels(int logLevel)
        {
            AppstackSDK.Configure("api-key", logLevel);

            Assert.That(bridge.LogLevel, Is.EqualTo(logLevel));
            Assert.That(bridge.ConfigureCalls, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void ConfigureRejectsLogLevelsOutsideDocumentedRange(int logLevel)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AppstackSDK.Configure("api-key", logLevel));
            Assert.That(bridge.ConfigureCalls, Is.Zero);
        }

        [Test]
        public void ConfigureNormalizesAndForwardsArguments()
        {
            AppstackSDK.Configure("  api-key  ", 2, "  customer-123  ");

            Assert.That(bridge.ApiKey, Is.EqualTo("api-key"));
            Assert.That(bridge.LogLevel, Is.EqualTo(2));
            Assert.That(bridge.CustomerUserId, Is.EqualTo("customer-123"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConfigureNormalizesMissingCustomerIdToEmptyString(string customerUserId)
        {
            AppstackSDK.Configure("api-key", customerUserId: customerUserId);

            Assert.That(bridge.CustomerUserId, Is.Empty);
        }

        [Test]
        public void ConfigureRethrowsBridgeFailure()
        {
            bridge.ConfigureException = new InvalidOperationException("configure failed");

            var exception = Assert.Throws<InvalidOperationException>(
                () => AppstackSDK.Configure("api-key"));

            Assert.That(exception, Is.SameAs(bridge.ConfigureException));
            Assert.That(logHandler.Messages, Has.Some.Contains("configure failed"));
        }

        [Test]
        public void ConfigureChecksStatusOnlyWhenBridgeReportsIt()
        {
            bridge.ReportsConfigurationStatus = true;

            AppstackSDK.Configure("api-key");

            Assert.That(bridge.IsSdkDisabledCalls, Is.EqualTo(1));
        }

        [Test]
        public void ConfigureIgnoresStatusReportingFailure()
        {
            bridge.ReportsConfigurationStatus = true;
            bridge.IsSdkDisabledException = new InvalidOperationException("status failed");

            Assert.DoesNotThrow(() => AppstackSDK.Configure("api-key"));
            Assert.That(logHandler.Messages, Has.None.Contains("status failed"));
        }

        [TestCaseSource(nameof(AllEventTypes))]
        public void SendEventMapsEveryEventAndDropsStandardEventNames(EventType eventType)
        {
            var suppliedName = eventType == EventType.CUSTOM ? "custom_event" : "ignored";

            AppstackSDK.SendEvent(eventType, suppliedName);

            Assert.That(bridge.EventType, Is.EqualTo(eventType.ToString()));
            Assert.That(
                bridge.EventName,
                Is.EqualTo(eventType == EventType.CUSTOM ? suppliedName : string.Empty));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void SendEventRequiresCustomEventName(string eventName)
        {
            Assert.Throws<ArgumentException>(
                () => AppstackSDK.SendEvent(EventType.CUSTOM, eventName));
            Assert.That(bridge.SendEventCalls, Is.Zero);
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
        public void SendEventRethrowsBridgeFailure()
        {
            bridge.SendEventException = new InvalidOperationException("event failed");

            var exception = Assert.Throws<InvalidOperationException>(
                () => AppstackSDK.SendEvent(EventType.LOGIN));

            Assert.That(exception, Is.SameAs(bridge.SendEventException));
            Assert.That(logHandler.Messages, Has.Some.Contains("event failed"));
        }

        [Test]
        public void AppleAdsDelegatesAndRethrowsBridgeFailure()
        {
            AppstackSDK.EnableAppleAdsAttribution();
            Assert.That(bridge.EnableAppleAdsCalls, Is.EqualTo(1));

            bridge.EnableAppleAdsException = new InvalidOperationException("ads failed");
            var exception = Assert.Throws<InvalidOperationException>(
                AppstackSDK.EnableAppleAdsAttribution);

            Assert.That(exception, Is.SameAs(bridge.EnableAppleAdsException));
            Assert.That(logHandler.Messages, Has.Some.Contains("ads failed"));
        }

        [Test]
        public void PublicQueriesReturnBridgeValues()
        {
            bridge.AppstackId = "appstack-id";
            bridge.SdkDisabled = true;

            Assert.That(AppstackSDK.GetAppstackId(), Is.EqualTo("appstack-id"));
            Assert.That(AppstackSDK.IsSdkDisabled(), Is.True);
        }

        [Test]
        public void PublicQueriesRethrowBridgeFailures()
        {
            bridge.GetAppstackIdException = new InvalidOperationException("id failed");
            var idException = Assert.Throws<InvalidOperationException>(
                () => AppstackSDK.GetAppstackId());
            Assert.That(idException, Is.SameAs(bridge.GetAppstackIdException));

            bridge.IsSdkDisabledException = new InvalidOperationException("status failed");
            var statusException = Assert.Throws<InvalidOperationException>(
                () => AppstackSDK.IsSdkDisabled());
            Assert.That(statusException, Is.SameAs(bridge.IsSdkDisabledException));
        }

        [Test]
        public void AttributionRejectsNullSuccessCallback()
        {
            Assert.Throws<ArgumentNullException>(
                () => AppstackSDK.GetAttributionParams(null));
            Assert.That(bridge.AttributionCalls, Is.Zero);
        }

        [Test]
        public void AttributionForwardsSuccessAndError()
        {
            var expected = new Dictionary<string, object> { { "campaign", "summer" } };
            Dictionary<string, object> actual = null;
            string error = null;
            bridge.AttributionInvocation = (onSuccess, _) => onSuccess(expected);

            AppstackSDK.GetAttributionParams(result => actual = result, value => error = value);

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(error, Is.Null);

            bridge.AttributionInvocation = (_, onError) => onError("native failure");
            AppstackSDK.GetAttributionParams(_ => Assert.Fail(), value => error = value);
            Assert.That(error, Is.EqualTo("native failure"));
        }

        [Test]
        public void AttributionRoutesSynchronousBridgeExceptionToErrorCallback()
        {
            bridge.AttributionException = new InvalidOperationException("callback failed");
            string error = null;

            Assert.DoesNotThrow(
                () => AppstackSDK.GetAttributionParams(_ => Assert.Fail(), value => error = value));

            Assert.That(error, Is.EqualTo("callback failed"));
            Assert.That(logHandler.Messages, Has.Some.Contains("callback failed"));
        }

        [Test]
        public void AttributionCompletesExactlyOnceWhenBridgeCallsBothCallbacks()
        {
            var successCalls = 0;
            var errorCalls = 0;
            bridge.AttributionInvocation = (onSuccess, onError) =>
            {
                onSuccess(new Dictionary<string, object>());
                onError("late error");
            };

            AppstackSDK.GetAttributionParams(_ => successCalls++, _ => errorCalls++);

            Assert.That(successCalls, Is.EqualTo(1));
            Assert.That(errorCalls, Is.Zero);

            bridge.AttributionInvocation = (onSuccess, onError) =>
            {
                onError("first error");
                onSuccess(new Dictionary<string, object>());
            };
            AppstackSDK.GetAttributionParams(_ => successCalls++, _ => errorCalls++);

            Assert.That(successCalls, Is.EqualTo(1));
            Assert.That(errorCalls, Is.EqualTo(1));
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
            public int ConfigureCalls { get; private set; }
            public string ApiKey { get; private set; }
            public int LogLevel { get; private set; }
            public string CustomerUserId { get; private set; }
            public Exception ConfigureException { get; set; }
            public int SendEventCalls { get; private set; }
            public string EventType { get; private set; }
            public string EventName { get; private set; }
            public string ParametersJson { get; private set; }
            public Exception SendEventException { get; set; }
            public int EnableAppleAdsCalls { get; private set; }
            public Exception EnableAppleAdsException { get; set; }
            public string AppstackId { get; set; }
            public Exception GetAppstackIdException { get; set; }
            public bool SdkDisabled { get; set; }
            public int IsSdkDisabledCalls { get; private set; }
            public Exception IsSdkDisabledException { get; set; }
            public int AttributionCalls { get; private set; }
            public Exception AttributionException { get; set; }
            public Action<Action<Dictionary<string, object>>, Action<string>>
                AttributionInvocation { get; set; }

            public void Configure(string apiKey, int logLevel, string customerUserId)
            {
                ConfigureCalls++;
                ApiKey = apiKey;
                LogLevel = logLevel;
                CustomerUserId = customerUserId;
                if (ConfigureException != null) throw ConfigureException;
            }

            public void SendEvent(string eventType, string eventName, string parametersJson)
            {
                SendEventCalls++;
                EventType = eventType;
                EventName = eventName;
                ParametersJson = parametersJson;
                if (SendEventException != null) throw SendEventException;
            }

            public void EnableAppleAdsAttribution()
            {
                EnableAppleAdsCalls++;
                if (EnableAppleAdsException != null) throw EnableAppleAdsException;
            }

            public string GetAppstackId()
            {
                if (GetAppstackIdException != null) throw GetAppstackIdException;
                return AppstackId;
            }

            public bool IsSdkDisabled()
            {
                IsSdkDisabledCalls++;
                if (IsSdkDisabledException != null) throw IsSdkDisabledException;
                return SdkDisabled;
            }

            public void GetAttributionParams(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError)
            {
                AttributionCalls++;
                if (AttributionException != null) throw AttributionException;
                AttributionInvocation?.Invoke(onSuccess, onError);
            }
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            public List<string> Messages { get; } = new List<string>();

            public void LogFormat(
                LogType logType,
                UnityEngine.Object context,
                string format,
                params object[] args)
            {
                Messages.Add(string.Format(format, args));
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                Messages.Add(exception.Message);
            }
        }
    }
}
