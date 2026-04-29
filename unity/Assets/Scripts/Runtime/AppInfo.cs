using System;

[Serializable]
public class AppInfo {
    public string package;
    public string label;
    public bool isVR;
    public bool isSystem;
}

[Serializable]
public class AppInfoList {
    public AppInfo[] apps;
}
