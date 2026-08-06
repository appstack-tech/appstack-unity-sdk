plugins {
    id("com.android.library")
}

android {
    namespace = "com.appstack.unity.contract.real"
    compileSdk = 35

    defaultConfig {
        minSdk = 21
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    sourceSets.named("main") {
        java.srcDir("../../../../Runtime/Plugins/Android")
    }
}

dependencies {
    // Must match Editor/AppstackDependencies.xml: this module exists to compile the
    // real bridge against the real artifact the package resolves at runtime.
    implementation("tech.appstack.android-sdk:appstack-android-sdk:1.7.0-SNAPSHOT")
}
