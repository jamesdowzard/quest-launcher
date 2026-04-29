using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AppRepository {

    private readonly List<AppInfo> _all = new List<AppInfo>();

    public IReadOnlyList<AppInfo> All => _all;

    public void Refresh() {
        _all.Clear();
        var raw = LauncherBridge.ListApps();
        var filtered = raw
            .Where(a => !a.isSystem || a.isVR)
            .OrderBy(a => a.label, System.StringComparer.OrdinalIgnoreCase);
        _all.AddRange(filtered);
        Debug.Log($"[QuestLauncher] Repository refreshed: {_all.Count} apps ({raw.Count} raw, {raw.Count - _all.Count} system filtered)");
    }
}
