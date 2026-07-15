# Android setup

## Requirements

- Android API level 21 or newer
- Target API level 34 or newer
- Java 17 or newer

The Unity package contains the Appstack C# and JNI bridge code, but it does not
bundle the native Appstack Android SDK. Before building for Android, choose one
of the following dependency setup options.

## Option 1 (recommended): EDM4U

Install [External Dependency Manager for Unity
(EDM4U)](https://github.com/google-unity/external-dependency-manager). Appstack's
Android dependency declaration is then discovered and resolved automatically
when Unity resolves Android dependencies. The Appstack Unity package does not
install EDM4U automatically.

If automatic resolution is disabled, run **Assets → External Dependency Manager
→ Android Resolver → Resolve**.

## Option 2: manual Gradle configuration

If your project does not use EDM4U, add the following repository and dependency
to the Gradle templates used by your Unity project:

```gradle
repositories {
    mavenCentral()
    maven { url "https://central.sonatype.com/repository/maven-public" }
}

dependencies {
    implementation "tech.appstack.android-sdk:appstack-android-sdk:1.5.0-rc1"
}
```

The exact template location depends on your Unity build configuration. The
dependency must be available to the `unityLibrary` module that compiles Android
plugins.

## Minification

No manual R8 or ProGuard configuration is required. Appstack adds its bridge
keep rules to the generated Android project automatically, and the native SDK
provides its own consumer rules.

You do not need to enable Unity's **Custom Proguard File** setting for Appstack.

## Troubleshooting

If Gradle cannot resolve the Appstack Android SDK:

1. Run the EDM4U Android resolver again.
2. Confirm Maven Central is available in the generated Gradle repositories.
3. Check that your build uses Java 17 or newer.
4. Inspect the Unity Console and Gradle output for the original resolution error.
