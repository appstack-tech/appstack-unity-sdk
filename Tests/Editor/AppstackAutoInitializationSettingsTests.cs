using Appstack.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Appstack.Tests
{
    public sealed class AppstackAutoInitializationSettingsTests
    {
        private AppstackAutoInitializationSettings settings;
        private SerializedObject serialized;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AppstackAutoInitializationSettings>();
            serialized = new SerializedObject(settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void DefaultsMatchDocumentedAutoInitializationDefaults()
        {
            Assert.That(settings.AutoInitialize, Is.True);
            Assert.That(settings.IOSEnabled, Is.True);
            Assert.That(settings.AndroidEnabled, Is.True);
            Assert.That(settings.EnvironmentMode, Is.EqualTo(AppstackEnvironmentMode.Automatic));
            Assert.That(settings.AllowProductionFallback, Is.False);
            Assert.That(settings.LogLevel, Is.EqualTo(1));
            Assert.That(settings.EnableAppleAdsAttribution, Is.False);
        }

        [TestCase((int)AppstackTargetPlatform.IOS, true, (int)AppstackResolvedEnvironment.Development, "ios-dev")]
        [TestCase((int)AppstackTargetPlatform.IOS, false, (int)AppstackResolvedEnvironment.Production, "ios-prod")]
        [TestCase((int)AppstackTargetPlatform.Android, true, (int)AppstackResolvedEnvironment.Development, "android-dev")]
        [TestCase((int)AppstackTargetPlatform.Android, false, (int)AppstackResolvedEnvironment.Production, "android-prod")]
        public void AutomaticModeResolvesByPlatformAndBuildType(
            int platformValue,
            bool developmentBuild,
            int expectedEnvironmentValue,
            string expectedKey)
        {
            SetAllKeys();
            var platform = (AppstackTargetPlatform)platformValue;
            var expectedEnvironment =
                (AppstackResolvedEnvironment)expectedEnvironmentValue;

            var resolved = settings.Resolve(platform, developmentBuild);

            Assert.That(resolved.Environment, Is.EqualTo(expectedEnvironment));
            Assert.That(resolved.ApiKey, Is.EqualTo(expectedKey));
            Assert.That(resolved.UsedProductionFallback, Is.False);
        }

        [TestCase((int)AppstackEnvironmentMode.Development, false, (int)AppstackResolvedEnvironment.Development, "ios-dev")]
        [TestCase((int)AppstackEnvironmentMode.Production, true, (int)AppstackResolvedEnvironment.Production, "ios-prod")]
        public void ExplicitModeOverridesBuildType(
            int modeValue,
            bool developmentBuild,
            int expectedEnvironmentValue,
            string expectedKey)
        {
            SetAllKeys();
            var mode = (AppstackEnvironmentMode)modeValue;
            var expectedEnvironment =
                (AppstackResolvedEnvironment)expectedEnvironmentValue;
            SetEnum("environmentMode", (int)mode);

            var resolved = settings.Resolve(AppstackTargetPlatform.IOS, developmentBuild);

            Assert.That(resolved.Environment, Is.EqualTo(expectedEnvironment));
            Assert.That(resolved.ApiKey, Is.EqualTo(expectedKey));
        }

        [Test]
        public void DevelopmentUsesExplicitProductionFallbackOnlyWhenDevelopmentKeyIsMissing()
        {
            SetString("iosProductionApiKey", " ios-prod ");

            var withoutFallback = settings.Resolve(AppstackTargetPlatform.IOS, true);
            Assert.That(withoutFallback.HasApiKey, Is.False);

            SetBool("allowProductionFallback", true);
            var withFallback = settings.Resolve(AppstackTargetPlatform.IOS, true);
            Assert.That(withFallback.ApiKey, Is.EqualTo("ios-prod"));
            Assert.That(withFallback.UsedProductionFallback, Is.True);

            SetString("iosDevelopmentApiKey", "ios-dev");
            var developmentPreferred = settings.Resolve(AppstackTargetPlatform.IOS, true);
            Assert.That(developmentPreferred.ApiKey, Is.EqualTo("ios-dev"));
            Assert.That(developmentPreferred.UsedProductionFallback, Is.False);
        }

        [Test]
        public void ProductionNeverFallsBackToDevelopment()
        {
            SetString("iosDevelopmentApiKey", "ios-dev");
            SetBool("allowProductionFallback", true);

            var resolved = settings.Resolve(AppstackTargetPlatform.IOS, false);

            Assert.That(resolved.HasApiKey, Is.False);
        }

        [Test]
        public void DisabledAndUnsupportedPlatformsDoNotResolveKeys()
        {
            SetAllKeys();
            SetBool("androidEnabled", false);

            Assert.That(
                settings.Resolve(AppstackTargetPlatform.Android, true).PlatformEnabled,
                Is.False);
            Assert.That(
                settings.Resolve(AppstackTargetPlatform.Unsupported, true).PlatformEnabled,
                Is.False);
        }

        [Test]
        public void BuildValidationChecksOnlyCurrentEnabledPlatform()
        {
            SetString("iosProductionApiKey", "ios-prod");

            Assert.DoesNotThrow(() => AppstackBuildValidation.Validate(
                settings,
                AppstackTargetPlatform.IOS,
                false));
            Assert.Throws<BuildFailedException>(() => AppstackBuildValidation.Validate(
                settings,
                AppstackTargetPlatform.Android,
                false));

            SetBool("androidEnabled", false);
            Assert.DoesNotThrow(() => AppstackBuildValidation.Validate(
                settings,
                AppstackTargetPlatform.Android,
                false));
        }

        [Test]
        public void BuildValidationAllowsMissingDisabledOrManualSettings()
        {
            Assert.DoesNotThrow(() => AppstackBuildValidation.Validate(
                null,
                AppstackTargetPlatform.IOS,
                false));

            SetBool("autoInitialize", false);
            Assert.DoesNotThrow(() => AppstackBuildValidation.Validate(
                settings,
                AppstackTargetPlatform.IOS,
                false));
        }

        [Test]
        public void BuildValidationAcceptsConfiguredDevelopmentFallback()
        {
            SetString("androidProductionApiKey", "android-prod");
            SetBool("allowProductionFallback", true);

            Assert.DoesNotThrow(() => AppstackBuildValidation.Validate(
                settings,
                AppstackTargetPlatform.Android,
                true));
        }

        [TestCase("Assets/Appstack/Resources/AppstackSettings.asset", true)]
        [TestCase("Assets/Configuration/Resources/AppstackSettings.asset", true)]
        [TestCase(@"Assets\Appstack\Resources\AppstackSettings.asset", true)]
        [TestCase("Assets/Appstack/AppstackSettings.asset", false)]
        [TestCase("Assets/Resources/Appstack/AppstackSettings.asset", false)]
        [TestCase("Assets/Appstack/Resources/RenamedSettings.asset", false)]
        [TestCase("", false)]
        public void RuntimeSettingsPathMustMatchResourcesLookup(
            string path,
            bool expected)
        {
            Assert.That(
                AppstackSettingsAsset.IsRuntimeLoadableAssetPath(path),
                Is.EqualTo(expected));
        }

        private void SetAllKeys()
        {
            SetString("iosDevelopmentApiKey", "ios-dev");
            SetString("iosProductionApiKey", "ios-prod");
            SetString("androidDevelopmentApiKey", "android-dev");
            SetString("androidProductionApiKey", "android-prod");
        }

        private void SetBool(string property, bool value)
        {
            serialized.FindProperty(property).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
        }

        private void SetString(string property, string value)
        {
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
        }

        private void SetEnum(string property, int value)
        {
            serialized.FindProperty(property).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
        }
    }
}
