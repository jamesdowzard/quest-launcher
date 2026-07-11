using UnityEngine;
using System.Collections.Generic;

public static class LauncherBridge {

    private const string KOTLIN_CLASS = "ai.jamesisan.questlauncher.LauncherBridge";

    private static AndroidJavaObject GetContext() {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    }

    public static List<AppInfo> ListApps() {
#if !UNITY_ANDROID || UNITY_EDITOR
        return new List<AppInfo>();
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        var json = bridge.CallStatic<string>("listApps", ctx);
        var wrapped = "{\"apps\":" + json + "}";
        return new List<AppInfo>(JsonUtility.FromJson<AppInfoList>(wrapped).apps);
#endif
    }

    public static Texture2D GetIcon(string packageName) {
#if !UNITY_ANDROID || UNITY_EDITOR
        return null;
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        var packed = bridge.CallStatic<int[]>("getIconArgb", ctx, packageName);
        if (packed == null || packed.Length < 2) return null;
        int w = packed[0];
        int h = packed[1];
        if (packed.Length != 2 + w * h) return null;

        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++) {
            int argb = packed[2 + i];
            pixels[i] = new Color32(
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF),
                (byte)((argb >> 24) & 0xFF));
        }
        // Bitmap pixels are top-down; Texture2D expects bottom-up.
        var flipped = new Color32[pixels.Length];
        for (int y = 0; y < h; y++) {
            System.Array.Copy(pixels, y * w, flipped, (h - 1 - y) * w, w);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(flipped);
        tex.Apply(false, false);
        return tex;
#endif
    }

    public static bool Launch(string packageName) {
#if !UNITY_ANDROID || UNITY_EDITOR
        Debug.Log($"[Launch] {packageName} (no-op in editor)");
        return false;
#else
        using var bridge = new AndroidJavaClass(KOTLIN_CLASS);
        var ctx = GetContext();
        return bridge.CallStatic<bool>("launch", ctx, packageName);
#endif
    }
}
