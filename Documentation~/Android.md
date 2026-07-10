# Android Setup for Appstack Unity SDK

The Java Unity bridge depends on the **Appstack Android SDK**. Resolve it with EDM4U.

## Option 1: External Dependency Manager (EDM4U)

If you use [External Dependency Manager for Unity (EDM4U)](https://github.com/google-unity/external-dependency-manager):

1. Ensure EDM4U is installed and resolves dependencies from this package, or  
2. Run **Assets → External Dependency Manager → Android Resolver → Resolve**.

## Option 2: Manual Gradle

If you do **not** use EDM4U, add the dependency and repository to your Unity Android Gradle config:

1. **mainTemplate.gradle** (or **baseProjectTemplate.gradle**, depending on your Unity version)  
   - In `allprojects.repositories`, add:
     ```gradle
     maven { url "https://central.sonatype.com/repository/maven-public" }
     ```
   - In `dependencies` (or in the `dependencies` block of the `unityProject` subproject that compiles your plugins), add:
     ```gradle
     implementation 'tech.appstack.android-sdk:appstack-android-sdk:1.5.0-rc0'
     ```

2. Rebuild your Android project.

## Requirements

- **minSdkVersion:** 21 (Android 5.0)  
- **targetSdkVersion:** 34+  
- **Java:** 17+

The C# bridge obtains `UnityPlayer.currentActivity.applicationContext` and
passes that application context to the native SDK.
