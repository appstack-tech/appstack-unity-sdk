plugins {
    kotlin("jvm")
}

kotlin {
    jvmToolchain(17)
}

sourceSets.named("main") {
    java.srcDir("../../../../Runtime/Plugins/Android")
}

dependencies {
    implementation(kotlin("stdlib"))
    implementation("org.json:json:20240303")

    testImplementation(kotlin("test-junit5"))
    testImplementation("org.junit.jupiter:junit-jupiter:5.10.3")
}

tasks.test {
    useJUnitPlatform()
}
