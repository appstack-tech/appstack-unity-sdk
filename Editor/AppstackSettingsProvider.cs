using System;
using System.Collections.Generic;
using System.Linq;
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

            EditorGUILayout.HelpBox(
                "API keys are masked only in this UI. They remain plaintext in the " +
                "settings asset and version control, and every configured key—including " +
                "development and other-platform keys—may be included in player builds.",
                MessageType.Warning);

            if (serializedSettings.ApplyModifiedProperties())
            {
                AssetDatabase.SaveAssetIfDirty(serializedSettings.targetObject);
            }
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
            var settings = (AppstackAutoInitializationSettings)serializedSettings.targetObject;
            var assetError = AppstackSettingsAsset.GetRuntimeLocationError(settings);
            if (assetError != null)
            {
                EditorGUILayout.HelpBox(assetError, MessageType.Error);
                return;
            }

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
            var canonical = AssetDatabase.LoadAssetAtPath<AppstackAutoInitializationSettings>(
                AppstackAutoInitializationSettings.AssetPath);
            if (canonical != null)
            {
                return canonical;
            }

            return FindAll()
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .FirstOrDefault();
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

        internal static string GetRuntimeLocationError(
            AppstackAutoInitializationSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            var allSettings = FindAll();
            if (allSettings.Count > 1)
            {
                return "Multiple Appstack settings assets exist. Keep exactly one asset " +
                       "named AppstackSettings.asset directly inside a Resources folder.";
            }

            var path = AssetDatabase.GetAssetPath(settings);
            if (!IsRuntimeLoadableAssetPath(path))
            {
                return $"The Appstack settings asset at '{path}' cannot be loaded at " +
                       "runtime. Move it directly into a Resources folder and keep the " +
                       "name AppstackSettings.asset.";
            }

            return null;
        }

        internal static bool IsRuntimeLoadableAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var normalized = path.Replace('\\', '/');
            return normalized.EndsWith(
                "/Resources/" + AppstackAutoInitializationSettings.ResourceName + ".asset",
                StringComparison.Ordinal);
        }

        private static List<AppstackAutoInitializationSettings> FindAll()
        {
            var settings = new List<AppstackAutoInitializationSettings>();
            foreach (var guid in AssetDatabase.FindAssets(
                         $"t:{nameof(AppstackAutoInitializationSettings)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AppstackAutoInitializationSettings>(path);
                if (asset != null)
                {
                    settings.Add(asset);
                }
            }

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
