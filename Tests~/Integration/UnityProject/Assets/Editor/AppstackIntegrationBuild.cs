using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

internal static class AppstackIntegrationBuild
{
    private const string ScenePath = "Assets/AppstackIntegration.unity";
    private const string PackageId = "com.appstack.unity-sdk";
    private const string AndroidBridgePath =
        "Packages/com.appstack.unity-sdk/Runtime/Plugins/Android/com/appstack/unity/AppstackUnityBridge.java";
    private const string IOSBridgePath =
        "Packages/com.appstack.unity-sdk/Runtime/Plugins/iOS/AppstackUnityBridge.swift";
    private const string KeepBegin = "# BEGIN Appstack Unity SDK";
    private const string KeepEnd = "# END Appstack Unity SDK";

    public static void ValidateImport()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
            typeof(Appstack.AppstackSDK).Assembly);
        Require(package != null, "Unity Package Manager did not resolve the Appstack package.");
        Require(
            string.Equals(package.name, PackageId, StringComparison.Ordinal),
            $"Resolved unexpected package '{package.name}'.");

        ValidatePluginImporter(AndroidBridgePath, BuildTarget.Android, BuildTarget.iOS);
        ValidatePluginImporter(IOSBridgePath, BuildTarget.iOS, BuildTarget.Android);
        Debug.Log($"[Appstack Integration Validation] Imported {package.name}@{package.version} from {package.resolvedPath}.");
    }

    public static void BuildAndroidPlayers()
    {
        ValidateImport();
        ConfigureSharedProject(typeof(AppstackIntegrationProbe), "tech.appstack.unity.playervalidation");
        SwitchTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Android,
            ManagedStrippingLevel.Medium);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.minifyDebug = false;
        PlayerSettings.Android.minifyRelease = true;
        EditorUserBuildSettings.buildAppBundle = false;

        Build(
            BuildTarget.Android,
            "Builds/PlayerValidation/Android/appstack-player-validation-development.apk",
            BuildOptions.Development | BuildOptions.CleanBuildCache);
        Build(
            BuildTarget.Android,
            "Builds/PlayerValidation/Android/appstack-player-validation-release.apk",
            BuildOptions.CleanBuildCache);

        ValidateGeneratedKeepRules();
    }

    public static void ExportIOSPlayer()
    {
        ValidateImport();
        ConfigureSharedProject(typeof(AppstackIntegrationProbe), "tech.appstack.unity.playervalidation");
        SwitchTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.iOS,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.iOS,
            ManagedStrippingLevel.Medium);
        PlayerSettings.iOS.targetOSVersionString = "15.0";

        const string output = "Builds/PlayerValidation/iOS";
        Build(BuildTarget.iOS, output, BuildOptions.CleanBuildCache);
        ValidateIOSExport(output);
    }

    public static void BuildAndroidRuntimePlayer()
    {
        ValidateImport();
        ConfigureSharedProject(typeof(AppstackRuntimeProbe), "tech.appstack.unity.runtimevalidation");
        SwitchTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Android,
            ManagedStrippingLevel.Medium);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        EditorUserBuildSettings.buildAppBundle = false;

        Build(
            BuildTarget.Android,
            "Builds/RuntimeValidation/Android/appstack-runtime-validation.apk",
            BuildOptions.Development | BuildOptions.CleanBuildCache);
    }

#if UNITY_IOS
    public static void ExportIOSSimulatorRuntimePlayer()
    {
        ValidateImport();
        ConfigureSharedProject(typeof(AppstackRuntimeProbe), "tech.appstack.unity.runtimevalidation");
        SwitchTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.iOS,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.iOS,
            ManagedStrippingLevel.Medium);
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        var simulatorArchitecture = Environment.GetEnvironmentVariable(
            "APPSTACK_RUNTIME_IOS_ARCH");
        Require(
            simulatorArchitecture == "arm64" || simulatorArchitecture == "x86_64",
            "APPSTACK_RUNTIME_IOS_ARCH must be arm64 or x86_64.");
        PlayerSettings.iOS.simulatorSdkArchitecture = simulatorArchitecture == "arm64"
            ? AppleMobileArchitectureSimulator.ARM64
            : AppleMobileArchitectureSimulator.X86_64;

        const string output = "Builds/RuntimeValidation/iOS";
        Build(BuildTarget.iOS, output, BuildOptions.Development | BuildOptions.CleanBuildCache);
        ConfigureRuntimeInfoPlist(output);
        ValidateIOSExport(output);
    }
#endif

    private static void ConfigureSharedProject(Type probeType, string applicationIdentifier)
    {
        var validationName = probeType == typeof(AppstackRuntimeProbe)
            ? "Runtime Validation"
            : "Player Validation";
        PlayerSettings.companyName = "Appstack";
        PlayerSettings.productName = $"Appstack {validationName}";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(
            NamedBuildTarget.Android,
            applicationIdentifier);
        PlayerSettings.SetApplicationIdentifier(
            NamedBuildTarget.iOS,
            applicationIdentifier);

        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        var probe = new GameObject($"Appstack {validationName} Probe");
        probe.AddComponent(probeType);
        Require(
            EditorSceneManager.SaveScene(scene, ScenePath),
            $"Could not save integration scene at {ScenePath}.");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        AssetDatabase.SaveAssets();
    }

#if UNITY_IOS
    private static void ConfigureRuntimeInfoPlist(string output)
    {
        var proxyUrl = Environment.GetEnvironmentVariable("APPSTACK_RUNTIME_PROXY_URL");
        Require(
            !string.IsNullOrEmpty(proxyUrl),
            "APPSTACK_RUNTIME_PROXY_URL is required for the runtime player.");

        var plistPath = Path.Combine(output, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("APPSTACK_DEV_PROXY_URL", proxyUrl);
        var transport = plist.root.CreateDict("NSAppTransportSecurity");
        transport.SetBoolean("NSAllowsLocalNetworking", true);
        plist.WriteToFile(plistPath);
    }
#endif

    private static void Build(BuildTarget target, string output, BuildOptions options)
    {
        var directory = target == BuildTarget.iOS ? output : Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            target = target,
            locationPathName = output,
            options = options
        });
        Require(
            report.summary.result == BuildResult.Succeeded,
            $"{target} build failed with {report.summary.totalErrors} errors.");
        Debug.Log(
            $"[Appstack Integration Validation] {target} build succeeded: " +
            $"{report.summary.totalSize} bytes in {report.summary.totalTime}.");
    }

    private static void ValidatePluginImporter(
        string path,
        BuildTarget included,
        BuildTarget excluded)
    {
        var importer = AssetImporter.GetAtPath(path) as PluginImporter;
        Require(importer != null, $"Missing plugin importer for {path}.");
        Require(
            importer.GetCompatibleWithPlatform(included),
            $"{path} is not enabled for {included}.");
        Require(
            !importer.GetCompatibleWithPlatform(excluded),
            $"{path} is unexpectedly enabled for {excluded}.");
        Require(
            !importer.GetCompatibleWithEditor(),
            $"{path} is unexpectedly enabled in the Editor.");
    }

    private static void ValidateGeneratedKeepRules()
    {
        var candidates = Directory
            .GetFiles("Library/Bee", "proguard-unity.txt", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        Require(candidates.Length > 0, "Unity did not generate proguard-unity.txt.");

        var rules = File.ReadAllText(candidates[0]);
        Require(Count(rules, KeepBegin) == 1, "Appstack keep-rule begin marker is not unique.");
        Require(Count(rules, KeepEnd) == 1, "Appstack keep-rule end marker is not unique.");
        Require(
            rules.Contains("com.appstack.unity.AppstackUnityBridge", StringComparison.Ordinal),
            "Generated ProGuard rules do not keep the JNI bridge.");
        Require(
            rules.Contains("AppstackUnityBridge$AttributionParamsCallback", StringComparison.Ordinal),
            "Generated ProGuard rules do not keep the JNI callback interface.");
        Debug.Log($"[Appstack Player Validation] Validated generated keep rules at {candidates[0]}.");
    }

    private static void ValidateIOSExport(string output)
    {
        var projectPath = Path.Combine(output, "Unity-iPhone.xcodeproj/project.pbxproj");
        var copiedBridge = Path.Combine(
            output,
            "Libraries/com.appstack.unity-sdk/Runtime/Plugins/iOS/AppstackUnityBridge.swift");
        Require(File.Exists(projectPath), "Unity did not generate the Xcode project.");
        Require(File.Exists(copiedBridge), "Unity did not copy the Swift bridge into the Xcode export.");

        var project = File.ReadAllText(projectPath);
        Require(
            project.Contains("https://github.com/appstack-tech/ios-appstack-sdk.git", StringComparison.Ordinal),
            "Xcode project is missing the Appstack Swift package URL.");
        Require(
            project.Contains("version = 4.4.0;", StringComparison.Ordinal),
            "Xcode project is missing the exact Appstack Swift package version.");
        Require(
            Count(project, "productName = AppstackSDK;") == 2,
            "AppstackSDK must be linked to both UnityFramework and the application target.");
        Require(
            project.Contains("AppstackUnityBridge.swift in Sources", StringComparison.Ordinal),
            "Swift bridge is not a compiled Xcode source.");
        Require(
            project.Contains("SWIFT_VERSION = 5.0;", StringComparison.Ordinal),
            "UnityFramework is missing SWIFT_VERSION=5.0.");
        Require(
            project.Contains("ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES = YES;", StringComparison.Ordinal),
            "Application target is not configured to embed Swift standard libraries.");
        Debug.Log("[Appstack Player Validation] Validated generated Xcode package and bridge wiring.");
    }

    private static int Count(string value, string fragment)
    {
        var count = 0;
        var start = 0;
        while ((start = value.IndexOf(fragment, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += fragment.Length;
        }
        return count;
    }

    private static void SwitchTarget(BuildTargetGroup group, BuildTarget target)
    {
        Require(
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target),
            $"Could not switch the active build target to {target}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new BuildFailedException($"Appstack integration validation: {message}");
        }
    }
}
