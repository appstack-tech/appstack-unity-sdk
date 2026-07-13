#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Appstack.Editor
{
    internal sealed class AppstackIOSPostProcessBuild : IPostprocessBuildWithReport
    {
        private const string SwiftBridgePath =
            "Runtime/Plugins/iOS/AppstackUnityBridge.swift";

        public int callbackOrder => 999;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AppstackIOSPostProcessBuild).Assembly);
            if (packageInfo == null)
            {
                Debug.LogWarning("Appstack iOS bridge: unable to resolve package path.");
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            project.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(
                mainTarget,
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES",
                "YES");
            AddSwiftBridge(project, frameworkTarget, packageInfo.resolvedPath);
            project.WriteToFile(projectPath);
        }

        private static void AddSwiftBridge(
            PBXProject project,
            string targetGuid,
            string packagePath)
        {
            var sourcePath = Path.Combine(packagePath, SwiftBridgePath);
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"Appstack iOS bridge missing at {sourcePath}.");
                return;
            }

            const string projectPath = "Libraries/Appstack/AppstackUnityBridge.swift";
            var existingGuid = project.FindFileGuidByProjectPath(projectPath);
            var fileGuid = string.IsNullOrEmpty(existingGuid)
                ? project.AddFile(sourcePath, projectPath)
                : existingGuid;
            project.AddFileToBuild(targetGuid, fileGuid);
        }
    }
}
#endif
