# Android Setup for Appstack Unity SDK

The Unity bridge (`AppstackUnityBridge.kt`) depends on the **Appstack Android SDK**. You must add it to your Android build.

## Option 1: External Dependency Manager (EDM4U)

If you use [External Dependency Manager for Unity (EDM4U)](https://github.com/google-unity/external-dependency-manager):

1. Ensure EDM4U is installed and resolves dependencies from this package, or  
2. Copy `Assets/AppstackSDK/Editor/AppstackDependencies.xml` into  
   `Assets/ExternalDependencyManager/Editor/` (or merge its content into your existing Dependencies.xml).
3. Run **Assets → External Dependency Manager → Android Resolver → Resolve**.

## Option 2: Manual Gradle

If you do **not** use EDM4U, add the dependency and repository to your Unity Android Gradle config:

1. **mainTemplate.gradle** (or **baseProjectTemplate.gradle**, depending on your Unity version)  
   - In `allprojects.repositories`, add:
     ```gradle
     maven { url "https://central.sonatype.com/repository/maven-public" }
     ```
   - In `dependencies` (or in the `dependencies` block of the `unityProject` subproject that compiles your plugins), add:
     ```gradle
     implementation 'tech.appstack.android-sdk:appstack-android-sdk:1.3.1'
     ```

2. Rebuild your Android project.

## Requirements

- **minSdkVersion:** 21 (Android 5.0)  
- **targetSdkVersion:** 34+  
- **Java:** 17+

The Kotlin bridge uses `UnityPlayer.currentActivity`; the Unity build provides this at runtime.
