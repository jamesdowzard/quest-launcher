#!/bin/bash
# Capture a screenshot from a running Quest app.
# Usage: ./scripts/screenshot.sh [output-name]
#
# Examples:
#   ./scripts/screenshot.sh                    # saves to screenshots/quest-TIMESTAMP.png
#   ./scripts/screenshot.sh model-test         # saves to screenshots/model-test.png

mkdir -p screenshots

TIMESTAMP=$(date +%Y%m%d-%H%M%S)
NAME="${1:-quest-$TIMESTAMP}"
OUTPUT="screenshots/${NAME}.png"
REMOTE="/sdcard/screenshot.png"

echo "Capturing screenshot from Quest..."
adb shell screencap -p "$REMOTE"
adb pull "$REMOTE" "$OUTPUT" 2>/dev/null

if [ -f "$OUTPUT" ]; then
    SIZE=$(du -h "$OUTPUT" | cut -f1)
    echo "Saved: $OUTPUT ($SIZE)"
else
    echo "ERROR: Failed to capture screenshot. Is the Quest connected?"
    echo "Check: adb devices -l"
    exit 1
fi
