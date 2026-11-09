namespace JyPrecomp.Core;

/// <summary>
/// 监视草稿 Resources/combination 下新出现的成品 *_video.mp4。
/// 忽略 *_temp.mp4 / *.alpha.mp4。只读目录。
/// </summary>
public static class CombinationWatcher
{
    public static string? GetCombinationDir(string draftFolder)
    {
        string d = Path.Combine(draftFolder, "Resources", "combination");
        return Directory.Exists(d) ? d : null;
    }

    public static bool IsFinalProductName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        if (fileName.Contains(".alpha", StringComparison.OrdinalIgnoreCase)) return false;
        if (fileName.Contains("_temp", StringComparison.OrdinalIgnoreCase)) return false;
        if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return false;
        // 成品：GUID_video.mp4
        return fileName.EndsWith("_video.mp4", StringComparison.OrdinalIgnoreCase)
               || (fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                   && !fileName.Contains("_temp", StringComparison.OrdinalIgnoreCase));
    }

    public static HashSet<string> SnapshotVideoFiles(string draftFolder)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? dir = GetCombinationDir(draftFolder);
        if (dir == null) return set;
        foreach (var f in Directory.EnumerateFiles(dir, "*.mp4"))
        {
            string n = Path.GetFileName(f);
            if (IsFinalProductName(n))
                set.Add(n);
        }
        return set;
    }

    public static async Task<string?> WaitNewStableVideoAsync(
        string draftFolder,
        HashSet<string> before,
        int stableSeconds = 4,
        int timeoutMinutes = 40,
        CancellationToken cancel = default,
        Action<string>? log = null)
    {
        string dir = Path.Combine(draftFolder, "Resources", "combination");
        log?.Invoke($"监视 combination 成品（忽略 *_temp），超时 {timeoutMinutes} 分钟...");
        var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
        string? candidate = null;
        long lastSize = -1;
        DateTime stableSince = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            cancel.ThrowIfCancellationRequested();
            if (!Directory.Exists(dir))
            {
                await Task.Delay(1000, cancel);
                continue;
            }

            string? newest = null;
            DateTime newestTime = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.mp4"))
            {
                string n = Path.GetFileName(f);
                if (!IsFinalProductName(n)) continue;
                if (before.Contains(n)) continue;
                var t = File.GetLastWriteTimeUtc(f);
                if (t > newestTime) { newestTime = t; newest = f; }
            }

            // 仅日志：若只有 temp 在涨，提示仍在写
            if (newest == null)
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*_temp*.mp4"))
                {
                    try
                    {
                        long sz = new FileInfo(f).Length;
                        log?.Invoke($"  写入中(临时): {Path.GetFileName(f)} {sz:N0}");
                    }
                    catch { }
                    break;
                }
            }
            else
            {
                long size = 0;
                try { size = new FileInfo(newest).Length; } catch { }

                if (candidate != newest)
                {
                    candidate = newest;
                    lastSize = size;
                    stableSince = DateTime.UtcNow;
                    log?.Invoke($"发现成品: {Path.GetFileName(newest)} ({size:N0})，等待写完...");
                }
                else if (size != lastSize)
                {
                    lastSize = size;
                    stableSince = DateTime.UtcNow;
                    log?.Invoke($"  增长中: {size:N0}");
                }
                else if ((DateTime.UtcNow - stableSince).TotalSeconds >= stableSeconds && size > 1024)
                {
                    log?.Invoke($"成品已稳定: {Path.GetFileName(newest)} ({size:N0})");
                    return newest;
                }
            }

            await Task.Delay(1500, cancel);
        }

        log?.Invoke("等待预合成超时");
        return null;
    }
}
