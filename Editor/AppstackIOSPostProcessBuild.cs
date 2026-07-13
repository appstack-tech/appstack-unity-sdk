#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Appstack.Editor
{
    internal sealed class AppstackIOSPostProcessBuild : IPostprocessBuildWithReport
    {
        private const string PackageUrl =
            "https://github.com/appstack-tech/ios-appstack-sdk.git";
        private const string PackageVersion = "4.4.0-rc0";
        private const string PackageProduct = "AppstackSDK";

        public int callbackOrder => 999;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            var packageGuid = project.AddRemotePackageReferenceAtVersion(
                PackageUrl,
                PackageVersion);

            // The bridge is compiled into UnityFramework, so that target must link
            // AppstackSDK. The application target must also own the dynamic Swift
            // package product so Xcode embeds and signs it in the final .app.
            project.AddRemotePackageFrameworkToProject(
                frameworkTarget,
                PackageProduct,
                packageGuid,
                false);
            project.AddRemotePackageFrameworkToProject(
                mainTarget,
                PackageProduct,
                packageGuid,
                false);
            project.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(
                mainTarget,
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES",
                "YES");
            project.WriteToFile(projectPath);
        }
    }
}
#endif
