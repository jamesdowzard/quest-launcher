package ai.jamesisan.questlauncher

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.drawable.BitmapDrawable
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayOutputStream

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

    @JvmStatic
    fun getIcon(context: Context, packageName: String): ByteArray? {
        val pm = context.packageManager
        return try {
            val drawable = pm.getApplicationIcon(packageName)
            val bitmap = if (drawable is BitmapDrawable) {
                drawable.bitmap
            } else {
                val w = drawable.intrinsicWidth.coerceAtLeast(128)
                val h = drawable.intrinsicHeight.coerceAtLeast(128)
                Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888).also { bmp ->
                    val canvas = Canvas(bmp)
                    drawable.setBounds(0, 0, w, h)
                    drawable.draw(canvas)
                }
            }
            val out = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)
            out.toByteArray()
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
