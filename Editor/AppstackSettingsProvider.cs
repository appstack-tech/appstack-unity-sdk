using System;
using UnityEditor;
using UnityEngine;

namespace Appstack.Editor
{
    internal sealed class AppstackSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Appstack";
        private SerializedObject serializedSettings;

        private AppstackSettingsProvider()
            : base(SettingsPath, SettingsScope.Project, new[] { "Appstack", "SDK", "API key" })
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new AppstackSettingsProvider();
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            LoadSettings();
        }

        public override void OnGUI(string searchContext)
        {
            if (serializedSettings == null || serializedSettings.targetObject == null)
            {
                DrawCreateSettings();
                return;
            }

            serializedSettings.Update();
            EditorGUILayout.LabelField("Auto-Initialization", EditorStyles.boldLabel);
            DrawProperty("autoInitialize", "Auto Initialize");
            DrawProperty("environmentMode", "Environment");
            DrawProperty("allowProductionFallback", "Allow Production Fallback");
            EditorGUILayout.HelpBox(
                "When enabled, a development build may use its production key only when " +
                "the development key is empty. Production builds never fall back to a " +
                "development key.",
                MessageType.Info);
            DrawProperty("logLevel", "Log Level");

            EditorGUILayout.Space();
            DrawPlatform(
                "iOS",
                "iosEnabled",
                "iosDevelopmentApiKey",
                "iosProductionApiKey");
            using (new EditorGUI.DisabledScope(
                       !serializedSettings.FindProperty("iosEnabled").boolValue))
            {
                DrawProperty("enableAppleAdsAttribution", "Enable Apple Ads Attribution");
            }

            EditorGUILayout.Space();
            DrawPlatform(
                "Android",
                "androidEnabled",
                "androidDevelopmentApiKey",
                "androidProductionApiKey");

            serializedSettings.ApplyModifiedProperties();
            DrawResolutionStatus();
        }

        private void DrawCreateSettings()
        {
            EditorGUILayout.HelpBox(
                "No Appstack auto-initialization settings exist. The SDK remains in " +
                "manual mode until settings are created.",
                MessageType.Info);
            if (GUILayout.Button("Create Appstack Settings", GUILayout.Width(220)))
            {
                var settings = AppstackSettingsAsset.Create();
                serializedSettings = new SerializedObject(settings);
            }
        }

        private void DrawPlatform(
            string label,
            string enabledProperty,
            string developmentKeyProperty,
            string productionKeyProperty)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            DrawProperty(enabledProperty, $"Enable {label}");
            using (new EditorGUI.DisabledScope(
                       !serializedSettings.FindProperty(enabledProperty).boolValue))
            {
                DrawSecretProperty(developmentKeyProperty, "Development API Key");
                DrawSecretProperty(productionKeyProperty, "Production API Key");
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            EditorGUILayout.PropertyField(
                serializedSettings.FindProperty(propertyName),
                new GUIContent(label));
        }

        private void DrawSecretProperty(string propertyName, string label)
        {
            var property = serializedSettings.FindProperty(propertyName);
            property.stringValue = EditorGUILayout.PasswordField(label, property.stringValue);
        }

        private void DrawResolutionStatus()
        {
            var target = AppstackBuildValidation.ToTargetPlatform(
                EditorUserBuildSettings.activeBuildTarget);
            if (target == AppstackTargetPlatform.Unsupported)
            {
                EditorGUILayout.HelpBox(
                    $"The active target {EditorUserBuildSettings.activeBuildTarget} is not " +
                    "supported by Appstack auto-initialization.",
                    MessageType.Info);
                return;
            }

            var settings = (AppstackAutoInitializationSettings)serializedSettings.targetObject;
            var resolved = settings.Resolve(target, EditorUserBuildSettings.development);
            if (!settings.AutoInitialize)
            {
                EditorGUILayout.HelpBox("Auto-initialization is disabled.", MessageType.Info);
            }
            else if (!resolved.PlatformEnabled)
            {
                EditorGUILayout.HelpBox($"Appstack is disabled for {target}.", MessageType.Info);
            }
            else if (!resolved.HasApiKey)
            {
                EditorGUILayout.HelpBox(
                    $"No {resolved.Environment} API key resolves for the active {target} " +
                    "target. Building this target will fail until it is configured or disabled.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Active target resolves to {target} / {resolved.Environment}" +
                    (resolved.UsedProductionFallback ? " using the production fallback." : "."),
                    MessageType.Info);
            }
        }

        private void LoadSettings()
        {
            var settings = AppstackSettingsAsset.Load();
            serializedSettings = settings == null ? null : new SerializedObject(settings);
        }
    }

    internal static class AppstackSettingsAsset
    {
        internal static AppstackAutoInitializationSettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<AppstackAutoInitializationSettings>(
                AppstackAutoInitializationSettings.AssetPath);
        }

        internal static AppstackAutoInitializationSettings Create()
        {
            var existing = Load();
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets", "Appstack");
            EnsureFolder("Assets/Appstack", "Resources");
            var settings = ScriptableObject.CreateInstance<AppstackAutoInitializationSettings>();
            AssetDatabase.CreateAsset(settings, AppstackAutoInitializationSettings.AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            return settings;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    [CustomEditor(typeof(AppstackAutoInitializationSettings))]
    internal sealed class AppstackSettingsAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This asset is managed through Edit > Project Settings > Appstack.",
                MessageType.Info);
            if (GUILayout.Button("Open Appstack Project Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Appstack");
            }
        }
    }
}
