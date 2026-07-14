using System;
using System.Collections.Generic;

namespace Appstack
{
    /// <summary>
    /// Internal boundary between the public C# facade and the active platform bridge.
    /// </summary>
    internal interface IAppstackNativeBridge
    {
        bool ReportsConfigurationStatus { get; }

        void Configure(string apiKey, int logLevel, string customerUserId);

        void SendEvent(string eventType, string eventName, string parametersJson);

        void EnableAppleAdsAttribution();

        string GetAppstackId();

        bool IsSdkDisabled();

        void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError);
    }
}
