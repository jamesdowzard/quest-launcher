package com.jhg.questhelper;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

/**
 * Minimal proxy activity for launching other apps from adb on Meta Quest.
 *
 * Horizon OS's VolumetricWindowManagerServiceImpl requires a valid parent
 * token when routing activity launches. Launches initiated via
 * `adb shell am start` have no parent and time out — especially for VR apps.
 *
 * This helper works around that by being an activity in its own 2D panel
 * (which the VolumetricWindowManager CAN place from adb, because the first
 * launch from Library gives it a vrshell parent token, and subsequent
 * singleTask invocations reuse it). When activated, it reads the `target`
 * extra and calls startActivity() to launch the real app — transferring
 * its vrshell context to the target in the process.
 *
 * Usage:
 *   1. Install the helper: adb install -r helper.apk
 *   2. Launch from Quest Library (one-time, required for first vrshell token):
 *      Library → Unknown Sources → Quest Helper
 *   3. From then on, any build can relaunch itself via:
 *      adb shell am start -n com.jhg.questhelper/.HelperActivity \
 *        --es target com.example.questapp/com.unity3d.player.UnityPlayerGameActivity
 */
public class HelperActivity extends Activity {
    private static final String TAG = "QuestHelper";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        handleIntent(getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handleIntent(intent);
    }

    private void handleIntent(Intent intent) {
        String target = intent.getStringExtra("target");
        if (target == null || target.isEmpty()) {
            Log.i(TAG, "No target extra. Usage: am start -n com.jhg.questhelper/.HelperActivity --es target <pkg>/<activity>");
            finish();
            return;
        }

        String[] parts = target.split("/", 2);
        if (parts.length != 2) {
            Log.e(TAG, "Invalid target format, expected pkg/activity: " + target);
            finish();
            return;
        }

        String pkg = parts[0];
        String activity = parts[1];
        if (activity.startsWith(".")) {
            activity = pkg + activity;
        }

        try {
            Intent launch = new Intent(Intent.ACTION_MAIN);
            launch.addCategory(Intent.CATEGORY_LAUNCHER);
            launch.addCategory("com.oculus.intent.category.VR");
            launch.setComponent(new ComponentName(pkg, activity));
            launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(launch);
            Log.i(TAG, "Launched: " + pkg + "/" + activity);
        } catch (Exception e) {
            Log.e(TAG, "Failed to launch " + target + ": " + e.getMessage(), e);
        }

        finish();
    }
}
