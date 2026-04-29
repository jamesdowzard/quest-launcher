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
        var bytes = bridge.CallStatic<byte[]>("getIcon", ctx, packageName);
        if (bytes == null) return null;
        var tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
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
