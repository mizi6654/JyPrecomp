using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace JyPrecomp.Core;

/// <summary>
/// 悬浮球 2.4 的导入方式（从 exe 字符串还原）：
/// videocut://com.ies.videocut/uganchor/anchor_point/import_material_for_editing?featureEntry=&lt;urlencode(json)&gt;
/// json 内 material_uri = file:///绝对路径
/// 这是剪映自己的「导入素材」深链，不是资源管理器拖文件。
/// </summary>
public static class FileImportHelper
{
    public const string VideocutImportBase =
        "videocut://com.ies.videocut/uganchor/anchor_point/import_material_for_editing?featureEntry=";

    /// <summary>
    /// 通过剪映 URI 协议导入本地文件到编辑器。
    /// </summary>
    public static bool ImportViaVideocutProtocol(string filePath, Action<string>? log = null, bool newDraft = false)
    {
        if (!File.Exists(filePath))
        {
            log?.Invoke("文件不存在: " + filePath);
            return false;
        }

        try
        {
            // file:///E:/path/to/file.mp4
            string fileUri = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;

            // 与 exe 中片段对齐的 JSON（sence 拼写保持和原版一致）
            var payload = new Dictionary<string, object?>
            {
                ["enter_from"] = "file_open_with",
                ["extension"] = new Dictionary<string, object>(),
                ["feature"] = "import_material_for_editing",
                ["feature_context"] = new Dictionary<string, object?>
                {
                    ["material_import"] = true,
                    ["material_uri"] = fileUri
                },
                ["sence"] = "editor",
                ["sence_context"] = new Dictionary<string, object?>
                {
                    ["new_draft"] = newDraft
                }
            };

            string json = JsonSerializer.Serialize(payload);
            string url = VideocutImportBase + Uri.EscapeDataString(json);

            log?.Invoke("调用剪映导入协议…");
            log?.Invoke("material_uri: " + fileUri);
            log?.Invoke("URL 长度: " + url.Length);

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
            log?.Invoke("已 ShellExecute videocut:// 导入协议");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("协议导入失败: " + ex.Message);
            return false;
        }
    }

    public static void OpenFolderSelect(string filePath, Action<string>? log = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
            log?.Invoke("备用：已打开目录选中文件");
        }
        catch (Exception ex)
        {
            log?.Invoke(ex.Message);
        }
    }

    public static void SetClipboardFileDrop(string filePath, Action<string>? log = null)
    {
        if (!File.Exists(filePath)) return;
        try
        {
            var col = new System.Collections.Specialized.StringCollection { filePath };
            Clipboard.Clear();
            Clipboard.SetFileDropList(col);
            log?.Invoke("已写入剪贴板 FileDrop");
        }
        catch (Exception ex)
        {
            log?.Invoke("剪贴板失败: " + ex.Message);
        }
    }
}
