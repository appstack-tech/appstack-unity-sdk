using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Appstack.Tests
{
    public sealed class AppstackSDKContractTests
    {
        [Test]
        public void NativeEventNamePreservesCustomEventName()
        {
            Assert.That(
                AppstackSDK.NativeEventName(EventType.CUSTOM, "level_complete"),
                Is.EqualTo("level_complete"));
        }

        [TestCase(EventType.PURCHASE)]
        [TestCase(EventType.LOGIN)]
        [TestCase(EventType.VIEW_CONTENT)]
        public void NativeEventNameDropsNameForStandardEvents(EventType eventType)
        {
            Assert.That(AppstackSDK.NativeEventName(eventType, "ignored"), Is.Null);
        }

        [Test]
        public void WrapperVersionMatchesPackageManifest()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(AppstackSDK).Assembly);

            Assert.That(packageInfo, Is.Not.Null);
            Assert.That(AppstackVersion.PackageVersion, Is.EqualTo(packageInfo.version));
            Assert.That(
                AppstackVersion.WrapperVersion,
                Is.EqualTo("unity-" + packageInfo.version));
        }
    }
}
