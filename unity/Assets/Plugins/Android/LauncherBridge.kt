package ai.jamesisan.questlauncher

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import org.json.JSONArray
import org.json.JSONObject

object LauncherBridge {

    @JvmStatic
    fun listApps(context: Context): String {
        val pm = context.packageManager

        val launcherIntent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER)
        val vrIntent = Intent(Intent.ACTION_MAIN).addCategory("com.oculus.intent.category.VR")

        val launcherApps = pm.queryIntentActivities(launcherIntent, 0)
        val vrPkgs = pm.queryIntentActivities(vrIntent, 0).map { it.activityInfo.packageName }.toSet()

        val arr = JSONArray()
        for (info in launcherApps) {
            val pkg = info.activityInfo.packageName
            val appInfo = info.activityInfo.applicationInfo
            val obj = JSONObject().apply {
                put("package", pkg)
                put("label", info.loadLabel(pm).toString())
                put("isVR", vrPkgs.contains(pkg))
                put("isSystem", (appInfo.flags and android.content.pm.ApplicationInfo.FLAG_SYSTEM) != 0)
            }
            arr.put(obj)
        }
        return arr.toString()
    }

    /**
     * Returns icon as packed [width, height, ...ARGB-pixels] IntArray.
     * Fixed 128×128 to keep the JNI hop predictable. Skips PNG encode
     * and the UnityEngine.ImageConversionModule reference dance — C#
     * rebuilds the Texture2D via SetPixels32 which is in core UnityEngine.dll.
     */
    @JvmStatic
    fun getIconArgb(context: Context, packageName: String): IntArray? {
        val pm = context.packageManager
        return try {
            val drawable = pm.getApplicationIcon(packageName)
            val w = 128
            val h = 128
            val bitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
            val canvas = Canvas(bitmap)
            drawable.setBounds(0, 0, w, h)
            drawable.draw(canvas)
            val pixels = IntArray(w * h)
            bitmap.getPixels(pixels, 0, w, 0, 0, w, h)
            IntArray(2 + pixels.size).also {
                it[0] = w
                it[1] = h
                System.arraycopy(pixels, 0, it, 2, pixels.size)
            }
        } catch (e: PackageManager.NameNotFoundException) {
            null
        }
    }

    @JvmStatic
    fun launch(context: Context, packageName: String): Boolean {
        val pm = context.packageManager
        val intent = pm.getLaunchIntentForPackage(packageName) ?: return false
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(intent)
        return true
    }
}
