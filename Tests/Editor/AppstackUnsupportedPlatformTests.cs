using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Appstack.Tests
{
    public sealed class AppstackUnsupportedPlatformTests
    {
        [Test]
        public void PublicApiIsSilentAndReturnsSafeDefaultsInEditor()
        {
            var previousHandler = Debug.unityLogger.logHandler;
            var recordingHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = recordingHandler;

            Dictionary<string, object> attribution = null;
            try
            {
                AppstackSDK.Configure("api-key");
                AppstackSDK.SendEvent(EventType.LOGIN);
                AppstackSDK.EnableAppleAdsAttribution();

                Assert.That(AppstackSDK.GetAppstackId(), Is.Null);
                Assert.That(AppstackSDK.IsSdkDisabled(), Is.True);
                AppstackSDK.GetAttributionParams(result => attribution = result);
            }
            finally
            {
                Debug.unityLogger.logHandler = previousHandler;
            }

            Assert.That(recordingHandler.LogCount, Is.Zero);
            Assert.That(attribution, Is.Not.Null.And.Empty);
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            public int LogCount { get; private set; }

            public void LogFormat(
                LogType logType,
                Object context,
                string format,
                params object[] args)
            {
                LogCount++;
            }

            public void LogException(System.Exception exception, Object context)
            {
                LogCount++;
            }
        }
    }
}
