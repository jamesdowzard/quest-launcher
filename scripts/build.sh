#!/bin/bash
# Full build + deploy pipeline. One command to go from code to Quest.
#
# Usage: ./scripts/build.sh [--skip-configure] [--skip-scene] [--skip-tmp] [--no-deploy]
#
# Steps: configure XR -> TMP essentials -> setup scene -> build APK -> sideload
# Each step early-exits on failure. Full Unity logs are kept under build/logs/.

set -euo pipefail

export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO/unity"
LOGDIR="$REPO/build/logs"
mkdir -p "$LOGDIR"

# --- Unity discovery -------------------------------------------------------
# The editor has lived at both the Hub path and /Applications/Unity/Unity.app on
# this machine (a .pkg install lands at the latter). Hardcoding one of them cost
# a debugging session, so try the known locations in order and let UNITY_PATH win.
UNITY_VERSION="${UNITY_VERSION:-6000.0.72f1}"
find_unity() {
    local candidates=(
        "${UNITY_PATH:-}"
        "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
        "/Applications/Unity/Unity.app/Contents/MacOS/Unity"
    )
    for c in "${candidates[@]}"; do
        [ -n "$c" ] && [ -x "$c" ] && { echo "$c"; return 0; }
    done
    # Last resort: any installed editor of the right version.
    local found
    found=$(find /Applications/Unity -maxdepth 5 -type f -path "*$UNITY_VERSION*/Unity.app/Contents/MacOS/Unity" 2>/dev/null | head -1)
    [ -n "$found" ] && { echo "$found"; return 0; }
    return 1
}

if ! UNITY=$(find_unity); then
    echo "ERROR: Unity $UNITY_VERSION not found." >&2
    echo "  Looked in the Hub path and /Applications/Unity/Unity.app." >&2
    echo "  Set UNITY_PATH=/path/to/Unity.app/Contents/MacOS/Unity to override." >&2
    exit 1
fi

SKIP_CONFIGURE=false
SKIP_SCENE=false
SKIP_TMP=false
DEPLOY=true

for arg in "$@"; do
    case $arg in
        --skip-configure) SKIP_CONFIGURE=true ;;
        --skip-scene)     SKIP_SCENE=true ;;
        --skip-tmp)       SKIP_TMP=true ;;
        --no-deploy)      DEPLOY=false ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

# --- Unity step runner -----------------------------------------------------
# The old version piped Unity through `grep -E "\[QuestBase\]|error|Error"` and
# kept no log, so a failure surfaced as a bare "Build FAILED: Failed" with the
# actual exception discarded. Keep the full log; filter only what is printed.
run_unity() {
    local label="$1" method="$2"
    local log="$LOGDIR/${label}.log"
    local rc=0

    echo ""
    echo "$label: $method"
    echo "  log: $log"

    "$UNITY" -projectPath "$PROJECT" -batchmode -quit \
        -executeMethod "$method" -logFile "$log" || rc=$?

    grep -E "\[QuestBase\]" "$log" 2>/dev/null | sed 's/^/  /' || true

    if [ $rc -ne 0 ]; then
        echo ""
        echo "  FAILED (exit $rc). Real error follows — full log at $log" >&2
        grep -nE "Exception|error CS[0-9]+|Error building|BuildFailedException|Build Finished, Result: Failure|Aborting" \
            "$log" 2>/dev/null | tail -30 | sed 's/^/  /' >&2 || true
        echo "  --- last 30 lines ---" >&2
        tail -30 "$log" | sed 's/^/  /' >&2
        exit $rc
    fi
    echo "  Done"
}

echo "=== Quest Build Pipeline ==="
echo "Unity:   $UNITY"
echo "Project: $PROJECT"

if [ "$SKIP_CONFIGURE" = false ]; then
    run_unity "1-configure" QuestBase.Editor.QuestBuildTools.ConfigureXRSettings
fi

# Deliberately NOT gated behind --skip-scene. AssetDatabase.ImportPackage is
# async, so the essentials must be imported by a Unity process that then exits;
# doing it mid-build is unreliable. TMPEssentialsBuildGuard fails the build if
# this was skipped, rather than shipping an APK whose every label throws.
if [ "$SKIP_TMP" = false ]; then
    run_unity "2-tmp-essentials" QuestBase.Editor.QuestBuildTools.EnsureTMPEssentials
fi

if [ "$SKIP_SCENE" = false ]; then
    run_unity "3-scene" QuestBase.Editor.QuestBuildTools.SetupLauncherScene
fi

run_unity "4-build" QuestBase.Editor.QuestBuildTools.BuildAPK

APK="$REPO/build/quest-app.apk"
if [ ! -f "$APK" ]; then
    echo "Build reported success but $APK is missing" >&2
    exit 1
fi

SIZE=$(du -h "$APK" | cut -f1)
echo ""
echo "Build succeeded: $APK ($SIZE)"

[ "$DEPLOY" = false ] && { echo "Skipping deploy (--no-deploy)."; exit 0; }

# --- Deploy ----------------------------------------------------------------
# The old check was `adb devices | grep -q "device$"`, which matches ANY attached
# device — a phone or a TV box would satisfy it and then `adb install` with no
# -s would fail on "more than one device". Pick the Quest by model.
find_quest() {
    adb devices | awk '/\tdevice$/ {print $1}' | while read -r serial; do
        model=$(adb -s "$serial" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
        case "$model" in *Quest*) echo "$serial"; return 0 ;; esac
    done
}

QUEST=$(find_quest | head -1)
if [ -n "$QUEST" ]; then
    echo "Quest detected ($QUEST) — installing..."
    adb -s "$QUEST" install -r "$APK"
    echo "Deployed. Launch from the Quest app library, or:"
    echo "  adb -s $QUEST shell am start -n ai.jamesisan.questlauncher/com.unity3d.player.UnityPlayerGameActivity"
else
    echo "No Quest connected. Once it is awake:"
    echo "  adb install -r $APK"
fi
