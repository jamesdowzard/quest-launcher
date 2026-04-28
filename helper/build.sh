#!/bin/bash
# Build the Quest Helper APK from scratch using Android SDK tools.
# Produces helper/build/helper.apk ready for adb install.

set -e
cd "$(dirname "$0")"

SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
BUILD_TOOLS="$SDK/build-tools/36.0.0"
PLATFORM="$SDK/platforms/android-32/android.jar"
JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home}"

AAPT2="$BUILD_TOOLS/aapt2"
D8="$BUILD_TOOLS/d8"
APKSIGNER="$BUILD_TOOLS/apksigner"
ZIPALIGN="$BUILD_TOOLS/zipalign"
JAVAC="$JAVA_HOME/bin/javac"

for tool in "$AAPT2" "$D8" "$APKSIGNER" "$ZIPALIGN" "$JAVAC"; do
    if [ ! -x "$tool" ]; then
        echo "ERROR: Missing tool: $tool"
        exit 1
    fi
done
if [ ! -f "$PLATFORM" ]; then
    echo "ERROR: Missing Android platform jar: $PLATFORM"
    exit 1
fi

rm -rf build
mkdir -p build/obj build/apk

echo "[1/5] Compiling Java..."
"$JAVAC" -source 1.8 -target 1.8 \
    -bootclasspath "$PLATFORM" -classpath "$PLATFORM" \
    -d build/obj \
    src/com/jhg/questhelper/*.java

echo "[2/5] Dexing classes..."
"$D8" --lib "$PLATFORM" --output build/apk \
    $(find build/obj -name '*.class')

echo "[3/5] Packaging resources + manifest..."
"$AAPT2" link \
    -I "$PLATFORM" \
    --manifest AndroidManifest.xml \
    --min-sdk-version 23 \
    --target-sdk-version 32 \
    -o build/helper.unsigned.apk

echo "[4/5] Adding classes.dex..."
# aapt2 link produces a zip — add classes.dex to it
cd build/apk
zip -q ../helper.unsigned.apk classes.dex
cd ../..

echo "[4.5/5] Aligning APK..."
"$ZIPALIGN" -f 4 build/helper.unsigned.apk build/helper.aligned.apk

echo "[5/5] Signing..."
KEYSTORE="$HOME/.android/debug.keystore"
if [ ! -f "$KEYSTORE" ]; then
    mkdir -p "$HOME/.android"
    "$JAVA_HOME/bin/keytool" -genkey -v \
        -keystore "$KEYSTORE" \
        -alias androiddebugkey \
        -dname "CN=Android Debug,O=Android,C=US" \
        -storepass android -keypass android \
        -keyalg RSA -keysize 2048 -validity 10000
fi

"$APKSIGNER" sign \
    --ks "$KEYSTORE" \
    --ks-pass pass:android \
    --key-pass pass:android \
    --out build/helper.apk \
    build/helper.aligned.apk

rm -f build/helper.unsigned.apk build/helper.aligned.apk

SIZE=$(du -h build/helper.apk | cut -f1)
echo ""
echo "✓ Built: helper/build/helper.apk ($SIZE)"
echo ""
echo "Install:"
echo "  adb install -r helper/build/helper.apk"
echo ""
echo "First-time launch (from Quest Library → Unknown Sources → Quest Helper)"
echo "then use from adb:"
echo "  adb shell am start -n com.jhg.questhelper/.HelperActivity \\"
echo "    --es target com.example.questapp/com.unity3d.player.UnityPlayerGameActivity"
