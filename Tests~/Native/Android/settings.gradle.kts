pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }

    plugins {
        id("com.android.library") version "8.12.0"
        kotlin("jvm") version "1.9.24"
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        // Native SDK release candidates go to the Central Portal snapshot repository,
        // which mavenCentral() does not serve. Scoped so only Appstack snapshots
        // resolve from here.
        maven {
            name = "appstackSnapshots"
            url = uri("https://central.sonatype.com/repository/maven-snapshots/")
            mavenContent { snapshotsOnly() }
            content { includeGroup("tech.appstack.android-sdk") }
        }
    }
}

rootProject.name = "appstack-unity-android-contract"
include(":real-artifact", ":contract-tests")
