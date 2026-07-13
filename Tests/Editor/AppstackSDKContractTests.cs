using NUnit.Framework;

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
    }
}
