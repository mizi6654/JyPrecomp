using Microsoft.Win32;

namespace JyPrecomp.Core;

public static class DraftScanner
{
    public record DraftItem(string Name, string Modified, string FolderPath);
    public record ComboFile(string FileName, string SizeText, string Modified, string FullPath);

    /// <summary>
    /// 尽量找到草稿根目录。支持自定义安装（如 E:\DownSoft\JianyingPro）与「JianyingPro Drafts」。
    /// </summary>
    public static string? FindDefaultDraftRoot()
    {
        var tried = new List<string>();

        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            p = p.Trim().TrimEnd('\\', '/');
            if (!tried.Contains(p, StringComparer.OrdinalIgnoreCase))
                tried.Add(p);
        }

        // 1) 注册表：自定义草稿路径
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bytedance\JianyingPro");
            if (key != null)
            {
                foreach (var name in new[] { "currentCustomDraftPath", "DraftPath", "draft_path" })
                {
                    var v = key.GetValue(name) as string;
                    Add(v);
                }
            }
        }
        catch { }

        // 2) 历史记录里可能带草稿绝对路径
        try
        {
            using var hist = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Bytedance\JianyingPro\GlobalSettings\History");
            if (hist != null)
            {
                foreach (var vn in hist.GetValueNames())
                {
                    var v = hist.GetValue(vn)?.ToString();
                    if (string.IsNullOrEmpty(v)) continue;
                    // 可能是文件路径，取目录向上找
                    if (v.Contains("Draft", StringComparison.OrdinalIgnoreCase) ||
                        v.Contains("draft", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(v)) Add(Path.GetDirectoryName(Path.GetDirectoryName(v)));
                        if (Directory.Exists(v)) Add(v);
                    }
                }
            }
        }
        catch { }

        // 3) 本机常见 / 用户曾用路径
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Add(Path.Combine(local, "JianyingPro", "User Data", "Projects", "com.lveditor.draft"));
        Add(Path.Combine(local, "CapCut", "User Data", "Projects", "com.lveditor.draft"));

        // 4) 自定义安装盘：扫描常见盘符下的 JianyingPro Drafts / 安装目录旁
        foreach (var drive in Environment.GetLogicalDrives())
        {
            Add(Path.Combine(drive, "DownSoft", "JianyingPro Drafts"));
            Add(Path.Combine(drive, "JianyingPro Drafts"));
            Add(Path.Combine(drive, "DownSoft", "JianyingPro", "User Data", "Projects", "com.lveditor.draft"));
            Add(Path.Combine(drive, "JianyingPro", "User Data", "Projects", "com.lveditor.draft"));
        }

        // 5) 若设置过剪映 exe，在其旁边推
        // （由外部传入更准，这里不强制）

        // 选第一个「存在且下面有子文件夹」的
        foreach (var p in tried)
        {
            if (!Directory.Exists(p)) continue;
            try
            {
                if (Directory.EnumerateDirectories(p).Any())
                    return p;
                // 空目录也返回，方便用户
                return p;
            }
            catch { }
        }

        return tried.FirstOrDefault(Directory.Exists) ?? tried.FirstOrDefault();
    }

    /// <summary>
    /// 根据 JianyingPro.exe 路径推测草稿目录。
    /// </summary>
    public static string? GuessDraftRootFromExe(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;
        try
        {
            string? dir = Path.GetDirectoryName(exePath);
            if (dir == null) return null;
            // E:\DownSoft\JianyingPro\... → E:\DownSoft\JianyingPro Drafts
            string parent = Path.GetDirectoryName(dir) ?? dir;
            string[] cands =
            {
                Path.Combine(parent, "JianyingPro Drafts"),
                Path.Combine(dir, "JianyingPro Drafts"),
                Path.Combine(parent, "JianyingPro", "User Data", "Projects", "com.lveditor.draft"),
                Path.Combine(dir, "User Data", "Projects", "com.lveditor.draft"),
            };
            foreach (var c in cands)
                if (Directory.Exists(c)) return c;

            // 同级再搜一层带 Draft 的目录
            if (Directory.Exists(parent))
            {
                foreach (var d in Directory.EnumerateDirectories(parent, "*Draft*"))
                    if (Directory.EnumerateDirectories(d).Any()) return d;
            }
        }
        catch { }
        return null;
    }

    public static DraftItem? FindMostRecentDraft(string? root = null)
    {
        root ??= FindDefaultDraftRoot();
        var list = ScanDrafts(root ?? "");
        return list.FirstOrDefault();
    }

    public static List<DraftItem> ScanDrafts(string root)
    {
        var result = new List<DraftItem>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return result;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                bool isDraft =
                    File.Exists(Path.Combine(dir, "draft_content.json")) ||
                    File.Exists(Path.Combine(dir, "draft_info.json")) ||
                    File.Exists(Path.Combine(dir, "draft_meta_info.json")) ||
                    File.Exists(Path.Combine(dir, "draft_cover.jpg")) ||
                    File.Exists(Path.Combine(dir, "draft_cover.webp")) ||
                    Directory.Exists(Path.Combine(dir, "Resources"));

                // 宽松：有任何 json 也算
                if (!isDraft)
                {
                    try
                    {
                        isDraft = Directory.EnumerateFiles(dir, "*.json").Any();
                    }
                    catch { }
                }
                if (!isDraft) continue;

                var info = new DirectoryInfo(dir);
                result.Add(new DraftItem(info.Name, info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), info.FullName));
            }
        }
        catch { }

        return result
            .OrderByDescending(d =>
            {
                try { return Directory.GetLastWriteTimeUtc(d.FolderPath); }
                catch { return DateTime.MinValue; }
            })
            .ToList();
    }

    public static List<ComboFile> ScanCombinationFiles(string draftFolder)
    {
        var result = new List<ComboFile>();
        string dir = Path.Combine(draftFolder, "Resources", "combination");
        if (!Directory.Exists(dir)) return result;
        foreach (var f in Directory.EnumerateFiles(dir, "*.mp4"))
        {
            string n = Path.GetFileName(f);
            if (n.Contains("_temp", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Contains(".alpha", StringComparison.OrdinalIgnoreCase)) continue;
            var fi = new FileInfo(f);
            result.Add(new ComboFile(n, $"{fi.Length / 1024.0 / 1024.0:F1} MB",
                fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), fi.FullName));
        }
        return result;
    }
}
