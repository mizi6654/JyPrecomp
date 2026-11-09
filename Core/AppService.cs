namespace JyPrecomp.Core;

public class AppService
{
    public string DraftRoot { get; set; } = DraftScanner.FindDefaultDraftRoot() ?? "";
    public string? JianyingExePath { get; set; }
    public string? CurrentDraftFolder { get; set; }

    public List<DraftScanner.DraftItem> GetDrafts() =>
        DraftScanner.ScanDrafts(DraftRoot);
}
