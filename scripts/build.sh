#!/bin/bash
# Full build + deploy pipeline. One command to go from code to Quest.
# Usage: ./scripts/build.sh [--skip-configure] [--skip-scene]
#
# Steps: configure XR → setup scene → build APK → sideload
# Each step early-exits on failure.

set -e

export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"

UNITY="/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(pwd)/unity"

SKIP_CONFIGURE=false
SKIP_SCENE=false

for arg in "$@"; do
    case $arg in
        --skip-configure) SKIP_CONFIGURE=true ;;
        --skip-scene) SKIP_SCENE=true ;;
    esac
done

echo "=== Quest Build Pipeline ==="

if [ "$SKIP_CONFIGURE" = false ]; then
    echo ""
    echo "Step 1/3: Configure XR settings..."
    $UNITY -projectPath "$PROJECT" -batchmode -quit \
        -executeMethod QuestBase.Editor.QuestBuildTools.ConfigureXRSettings \
        -logFile - 2>&1 | grep -E "\[QuestBase\]|error|Error"
    echo "  Done"
fi

if [ "$SKIP_SCENE" = false ]; then
    echo ""
    echo "Step 2/3: Setup scene..."
    $UNITY -projectPath "$PROJECT" -batchmode -quit \
        -executeMethod QuestBase.Editor.QuestBuildTools.SetupLauncherScene \
        -logFile - 2>&1 | grep -E "\[QuestBase\]|error|Error"
    echo "  Done"
fi

echo ""
echo "Step 3/3: Build APK..."
$UNITY -projectPath "$PROJECT" -batchmode -quit \
    -executeMethod QuestBase.Editor.QuestBuildTools.BuildAPK \
    -logFile - 2>&1 | grep -E "\[QuestBase\]|error|Error"

APK="build/quest-app.apk"
if [ -f "$APK" ]; then
    SIZE=$(du -h "$APK" | cut -f1)
    echo ""
    echo "Build succeeded: $APK ($SIZE)"
    echo ""

    # Auto-deploy if Quest connected
    if adb devices | grep -q "device$"; then
        echo "Quest detected — installing..."
        adb install -r "$APK"
        echo "Deployed! Launch from Quest app library."
    else
        echo "No Quest connected. Run manually:"
        echo "  adb install -r $APK"
    fi
else
    echo "Build FAILED — check logs above"
    exit 1
fi
