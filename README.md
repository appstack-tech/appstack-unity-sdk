<p align="center">
  <a href="https://www.appstack.tech">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://docs.appstack.tech/images/appstack_logo_white_wordmark.png">
      <img alt="Appstack" src="https://docs.appstack.tech/images/appstack_logo_black_wordmark.png" width="280">
    </picture>
  </a>
</p>

<p align="center">
  Mobile attribution and ad-network optimization for Unity games and apps.
</p>

<p align="center">
  <a href="https://openupm.com/packages/com.appstack.unity-sdk/"><img alt="OpenUPM" src="https://img.shields.io/npm/v/com.appstack.unity-sdk?label=openupm&registry_uri=https://package.openupm.com"></a>
  <img alt="Unity" src="https://img.shields.io/badge/Unity-6000.0%2B-black.svg">
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-iOS%2015%2B%20%7C%20Android%205.0%2B-blue.svg">
  <a href="https://github.com/appstack-tech/appstack-unity-sdk/blob/main/LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-MIT-lightgrey.svg"></a>
</p>

<p align="center">
  <a href="https://docs.appstack.tech/SDKs/unity"><b>Documentation</b></a>
  &nbsp;·&nbsp;
  <a href="https://docs.appstack.tech/reference/unity">API reference</a>
  &nbsp;·&nbsp;
  <a href="https://github.com/appstack-tech/appstack-unity-sdk/blob/main/CHANGELOG.md">Changelog</a>
  &nbsp;·&nbsp;
  <a href="https://www.appstack.tech/contact">Support</a>
</p>

---

The Appstack Unity SDK tracks installs and in-app events, attributes them to your ad campaigns, and sends conversions back to Meta, Google, TikTok, Apple Ads and other networks. It wraps the native Appstack iOS and Android SDKs.

## Installation

Install `com.appstack.unity-sdk` from [OpenUPM](https://openupm.com/packages/com.appstack.unity-sdk/):

```sh
openupm add com.appstack.unity-sdk
```

Or add `https://package.openupm.com` as a scoped registry for `com.appstack` and install the package from **Window ▸ Package Manager**. Android builds also need [EDM4U](https://github.com/googlesamples/unity-jar-resolver) or the manual Gradle setup described in the [documentation](https://docs.appstack.tech/SDKs/unity).

## Quick start

Open **Edit ▸ Project Settings ▸ Appstack**, select **Create Appstack Settings** and enter your API keys. The SDK then initializes automatically before the first scene.

To initialize it yourself instead (for example after a consent prompt):

```csharp
using System.Collections.Generic;
using Appstack;

// For example in a MonoBehaviour's Start()
AppstackSDK.Configure("your_api_key");

AppstackSDK.SendEvent(
    EventType.PURCHASE,
    parameters: new Dictionary<string, object> { { "revenue", 29.99 }, { "currency", "USD" } });
```

Setup, event types, Apple Ads attribution, integrations (RevenueCat, Superwall) and troubleshooting are covered in the **[official documentation](https://docs.appstack.tech/SDKs/unity)**.

## Documentation

- **[Unity SDK guide](https://docs.appstack.tech/SDKs/unity)**: installation, configuration and event tracking
- **[API reference](https://docs.appstack.tech/reference/unity)**: every public method and type
- **[Apple Ads](https://docs.appstack.tech/Integrations/apple-ads)**: Apple Ads attribution setup
- **[RevenueCat](https://docs.appstack.tech/Integrations/revenuecat)** and **[Superwall](https://docs.appstack.tech/Integrations/superwall)**: subscription platform integrations
- **[iOS setup](Documentation~/iOS.md)** and **[Android setup](Documentation~/Android.md)**: platform notes shipped with the package
- **[Changelog](https://github.com/appstack-tech/appstack-unity-sdk/blob/main/CHANGELOG.md)**: release notes for every version

## Sample and contributing

Import the **Basic Integration** sample from the Unity Package Manager. For package architecture and contribution guidance see [DEVELOPMENT.md](DEVELOPMENT.md); release maintainers should use [RELEASING.md](RELEASING.md).

## Other platforms

[iOS](https://docs.appstack.tech/SDKs/swift) · [Android](https://docs.appstack.tech/SDKs/kotlin) · [React Native](https://docs.appstack.tech/SDKs/react-native) · [Flutter](https://docs.appstack.tech/SDKs/flutter)

## Support

Questions or issues? [Open an issue](https://github.com/appstack-tech/appstack-unity-sdk/issues) or [contact us](https://www.appstack.tech/contact).

## License

Released under the [MIT License](https://github.com/appstack-tech/appstack-unity-sdk/blob/main/LICENSE.md).
