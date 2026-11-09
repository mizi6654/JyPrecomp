namespace JyPrecomp.Core;

/// <summary>
/// 备份/还原草稿工程元数据。
/// 悬浮球可在不关草稿时还原：写回文件后剪映热加载（界面闪一下）。
/// 因此写回必须用共享模式，并尽量触发窗口刷新。
/// </summary>
public static class DraftBackup
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "resources", ".recycle_bin", ".backup", "cache", "temp", ".DS_Store"
    };

    private static readonly HashSet<string> SkipExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".ts", ".aac", ".mp3", ".wav",
        ".flac", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".psd",
        ".aep", ".prproj", ".zip", ".rar"
    };

    public static string DefaultBackupRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "剪映预合成导出", "_备份");

    public static string Backup(string draftFolder, string? backupRoot = null, Action<string>? log = null)
    {
        if (!Directory.Exists(draftFolder))
            throw new DirectoryNotFoundException(draftFolder);

        backupRoot ??= DefaultBackupRoot;
        string name = Path.GetFileName(draftFolder.TrimEnd('\\', '/'));
        string target = Path.Combine(backupRoot, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(target);

        int count = 0;
        long bytes = 0;
        foreach (var file in EnumerateBackupFiles(draftFolder))
        {
            string rel = Path.GetRelativePath(draftFolder, file);
            string dest = Path.Combine(target, rel);
            string? destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            try
            {
                CopyShared(file, dest);
                count++;
                try { bytes += new FileInfo(file).Length; } catch { }
            }
            catch (Exception ex)
            {
                log?.Invoke($"备份跳过 {rel}: {ex.Message}");
            }
        }

        File.WriteAllText(Path.Combine(target, "_backup_info.txt"),
            $"draft={draftFolder}\ntime={DateTime.Now:O}\nfiles={count}\n",
            System.Text.Encoding.UTF8);

        log?.Invoke($"已备份 {count} 个工程文件（{bytes / 1024.0:F0} KB）→ {target}");
        return target;
    }

    public static void Restore(string backupDir, string draftFolder, Action<string>? log = null)
    {
        if (!Directory.Exists(backupDir))
            throw new DirectoryNotFoundException(backupDir);
        if (!Directory.Exists(draftFolder))
            throw new DirectoryNotFoundException(draftFolder);

        int count = 0, fail = 0;
        var restored = new List<string>();

        foreach (var file in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(backupDir, file);
            if (rel.Contains("..")) continue;
            if (string.Equals(Path.GetFileName(file), "_backup_info.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            string dest = Path.Combine(draftFolder, rel);
            string? destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            bool ok = false;
            for (int attempt = 0; attempt < 5 && !ok; attempt++)
            {
                try
                {
                    if (File.Exists(dest))
                    {
                        try { File.SetAttributes(dest, FileAttributes.Normal); } catch { }
                    }
                    CopyShared(file, dest);
                    // 碰一下时间戳，便于剪映文件监视器感知变化
                    try { File.SetLastWriteTimeUtc(dest, DateTime.UtcNow); } catch { }
                    ok = true;
                    count++;
                    restored.Add(rel);
                }
                catch (Exception ex)
                {
                    if (attempt == 4)
                    {
                        fail++;
                        log?.Invoke($"还原失败 {rel}: {ex.Message}");
                    }
                    else
                        Thread.Sleep(200 * (attempt + 1));
                }
            }
        }

        log?.Invoke($"已写回 {count} 个工程文件" + (fail > 0 ? $"，失败 {fail}" : ""));
        if (count > 0)
        {
            log?.Invoke("正在尝试让剪映热加载（不关草稿）…");
            TryNudgeJianyingReload(log);
            log?.Invoke("若时间线未变：点一下时间线空白处，或切换一下素材页签再回来。");
        }
    }

    /// <summary>
    /// 共享读写拷贝，剪映占用文件时仍可能写成功。
    /// </summary>
    private static void CopyShared(string src, string dest)
    {
        using var inStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var outStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        inStream.CopyTo(outStream);
        outStream.Flush(true);
    }

    /// <summary>
    /// 轻量「闪一下」：置前窗口，模拟悬浮球还原后的界面刷新感。
    /// 不杀进程、不强制取消最大化。
    /// </summary>
    private static void TryNudgeJianyingReload(Action<string>? log)
    {
        try
        {
            if (!JianyingWindow.IsRunning()) return;
            JianyingWindow.Activate(log);
            Thread.Sleep(200);
            var h = JianyingWindow.FindMainWindow();
            if (h != IntPtr.Zero)
            {
                // 再点一下客户区，促使编辑器处理消息队列
                InputSender.ClickWindowCenter(h, null);
            }
        }
        catch (Exception ex)
        {
            log?.Invoke("刷新提示: " + ex.Message);
        }
    }

    private static IEnumerable<string> EnumerateBackupFiles(string draftFolder)
    {
        var stack = new Stack<string>();
        stack.Push(draftFolder);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            IEnumerable<string> subDirs;
            try { subDirs = Directory.EnumerateDirectories(dir); }
            catch { continue; }

            foreach (var sub in subDirs)
            {
                string name = Path.GetFileName(sub);
                if (SkipDirNames.Contains(name)) continue;
                stack.Push(sub);
            }

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); }
            catch { continue; }
            foreach (var f in files)
            {
                if (SkipExt.Contains(Path.GetExtension(f))) continue;
                yield return f;
            }
        }
    }
}
