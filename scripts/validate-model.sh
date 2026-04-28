#!/bin/bash
# Validate a 3D model file without building an APK.
# Usage: ./scripts/validate-model.sh /path/to/model.glb
#
# Runs the Unity ValidateModel method in batch mode.
# Checks: file exists, format, GLB header, size warnings for Quest 3.

set -e

if [ -z "$1" ]; then
    echo "Usage: ./scripts/validate-model.sh /path/to/model.glb"
    exit 1
fi

MODEL_PATH="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"

if [ ! -f "$MODEL_PATH" ]; then
    echo "ERROR: File not found: $MODEL_PATH"
    exit 1
fi

UNITY="/Applications/Unity/Hub/Editor/6000.0.72f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(pwd)/unity"

echo "Validating: $MODEL_PATH"
echo ""

$UNITY -projectPath "$PROJECT" -batchmode -quit \
    -executeMethod QuestBase.Editor.QuestBuildTools.ValidateModel \
    -modelPath "$MODEL_PATH" \
    -logFile - 2>&1 | grep -E "\[QuestBase\]"
