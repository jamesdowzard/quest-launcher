#!/bin/bash
# Stream Quest app logs filtered to QuestBase tags.
# Usage: ./scripts/logcat.sh [additional-tag]
#
# Examples:
#   ./scripts/logcat.sh                   # default tags
#   ./scripts/logcat.sh MyCustomTag       # add custom tag

TAGS="QuestBase|ModelLoader|ModelPlatform|AppBootstrap|OrbitController|Unity"

if [ -n "$1" ]; then
    TAGS="$TAGS|$1"
fi

echo "Streaming logs for: $TAGS"
echo "Press Ctrl+C to stop"
echo "---"

adb logcat -v time | grep -E "($TAGS)"
