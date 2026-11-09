using JyPrecomp.Controls;
using JyPrecomp.Core;

namespace JyPrecomp;

public class MainPage : UserControl
{
    private readonly AppService _svc;
    private readonly TextBox _txtRoot = new();
    private readonly ListBox _lstDrafts = new();
    private readonly TextBox _log = new();
    private readonly FlatButton _btnStart = new() { Text = "一键预合成导出", IsPrimary = true };
    private readonly FlatButton _btnRestore = new() { Text = "还原", IsPrimary = false };
    private readonly FlatButton _btnClearLog = new() { Text = "清空日志", IsPrimary = false };
    private readonly Label _status = new();

    private PrecompPipeline? _pipeline;
    private CancellationTokenSource? _cts;
    private List<DraftScanner.DraftItem> _draftMap = new();

    public MainPage(AppService svc)
    {
        _svc = svc;
        Dock = DockStyle.Fill;
        BackColor = Theme.BgMain;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Theme.BgMain
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "JyPrecomp · 剪映预合成",
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        // 草稿区
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.BgCard,
            Padding = new Padding(10)
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtRoot.Dock = DockStyle.Fill;
        _txtRoot.BackColor = Theme.BgInput;
        _txtRoot.ForeColor = Theme.TextPrimary;
        _txtRoot.BorderStyle = BorderStyle.FixedSingle;

        var btnBrowse = new FlatButton { Text = "浏览", Dock = DockStyle.Fill, IsPrimary = false };
        var btnRefresh = new FlatButton { Text = "刷新", Dock = DockStyle.Fill, IsPrimary = true };
        btnBrowse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog
            {
                Description = "选择剪映草稿根目录（里面是各个草稿文件夹，如「11月8日」）",
                UseDescriptionForTitle = true
            };
            if (!string.IsNullOrEmpty(_txtRoot.Text) && Directory.Exists(_txtRoot.Text))
                d.InitialDirectory = _txtRoot.Text;
            if (d.ShowDialog() == DialogResult.OK)
            {
                _txtRoot.Text = d.SelectedPath;
                _svc.DraftRoot = d.SelectedPath;
                RefreshList();
            }
        };
        btnRefresh.Click += (_, _) =>
        {
            _svc.DraftRoot = _txtRoot.Text.Trim();
            RefreshList();
        };

        grid.Controls.Add(_txtRoot, 0, 0);
        grid.Controls.Add(btnBrowse, 1, 0);
        grid.Controls.Add(btnRefresh, 2, 0);

        _lstDrafts.Dock = DockStyle.Fill;
        _lstDrafts.BackColor = Theme.BgInput;
        _lstDrafts.ForeColor = Theme.TextPrimary;
        _lstDrafts.BorderStyle = BorderStyle.None;
        _lstDrafts.IntegralHeight = false;
        _lstDrafts.SelectedIndexChanged += (_, _) =>
        {
            int i = _lstDrafts.SelectedIndex;
            if (i >= 0 && i < _draftMap.Count)
            {
                _svc.CurrentDraftFolder = _draftMap[i].FolderPath;
                Log($"当前草稿: {_draftMap[i].Name}");
            }
        };
        grid.SetColumnSpan(_lstDrafts, 3);
        grid.Controls.Add(_lstDrafts, 0, 1);
        card.Controls.Add(grid);
        root.Controls.Add(card, 0, 1);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        _btnStart.Size = new Size(150, 34);
        _btnRestore.Size = new Size(100, 34);
        _btnClearLog.Size = new Size(100, 34);
        _btnRestore.Enabled = false;
        _btnStart.Click += async (_, _) => await StartAsync();
        _btnRestore.Click += (_, _) => DoRestore();
        _btnClearLog.Click += (_, _) => { _log.Clear(); };
        bar.Controls.Add(_btnStart);
        bar.Controls.Add(_btnRestore);
        bar.Controls.Add(_btnClearLog);
        root.Controls.Add(bar, 0, 2);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.BackColor = Theme.BgInput;
        _log.ForeColor = Theme.TextPrimary;
        _log.Font = new Font("Consolas", 9f);
        _log.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_log, 0, 3);

        _status.Dock = DockStyle.Fill;
        _status.ForeColor = Theme.TextMuted;
        _status.Text = "选择草稿后点「一键预合成导出」";
        root.Controls.Add(_status, 0, 4);

        Log("请选择草稿根目录（例如 E:\\DownSoft\\JianyingPro Drafts），再选中具体草稿。");
        InitRoot();
    }

    private void InitRoot()
    {
        // 优先：用户曾用路径、exe 旁、自动发现
        string? root = null;
        if (!string.IsNullOrEmpty(_svc.DraftRoot) && Directory.Exists(_svc.DraftRoot))
            root = _svc.DraftRoot;
        root ??= DraftScanner.GuessDraftRootFromExe(_svc.JianyingExePath);
        // 常见：安装在 E:\DownSoft\JianyingPro 时草稿在同级 Drafts
        if (root == null && File.Exists(@"E:\JianyingPro\JianyingPro.exe"))
            root = DraftScanner.GuessDraftRootFromExe(@"E:\JianyingPro\JianyingPro.exe");
        if (root == null && File.Exists(@"E:\DownSoft\JianyingPro\JianyingPro.exe"))
            root = DraftScanner.GuessDraftRootFromExe(@"E:\DownSoft\JianyingPro\JianyingPro.exe");
        if (Directory.Exists(@"E:\DownSoft\JianyingPro Drafts"))
            root ??= @"E:\DownSoft\JianyingPro Drafts";
        root ??= DraftScanner.FindDefaultDraftRoot();

        _txtRoot.Text = root ?? "";
        _svc.DraftRoot = root ?? "";
        if (!string.IsNullOrEmpty(root))
            RefreshList();
        else
            Log("未能自动找到草稿根目录，请点「浏览」选择（目录内应是各个草稿文件夹）。");
    }

    private void RefreshList()
    {
        _lstDrafts.Items.Clear();
        _draftMap = new List<DraftScanner.DraftItem>();
        string r = _txtRoot.Text.Trim();
        _svc.DraftRoot = r;
        if (string.IsNullOrEmpty(r) || !Directory.Exists(r))
        {
            Log("路径无效或不存在: " + r);
            return;
        }

        var list = DraftScanner.ScanDrafts(r);
        _draftMap = list;
        foreach (var d in list)
            _lstDrafts.Items.Add($"{d.Name}    {d.Modified}");

        Log($"草稿根: {r}");
        Log($"共 {list.Count} 个草稿");
        if (list.Count == 0)
        {
            Log("提示：应选择「草稿根目录」，例如里面直接是「11月8日」「10月29日」这种文件夹，");
            Log("不要选到某一个草稿的内部（不要选到 Resources 那一层）。");
        }
        else
        {
            _lstDrafts.SelectedIndex = 0;
        }
    }

    private async Task StartAsync()
    {
        if (string.IsNullOrEmpty(_svc.CurrentDraftFolder))
        {
            Log("请先在列表中选中一个草稿");
            return;
        }

        _btnStart.Enabled = false;
        _btnRestore.Enabled = false;
        _status.Text = "运行中…";
        _cts = new CancellationTokenSource();
        _pipeline = new PrecompPipeline
        {
            DraftFolder = _svc.CurrentDraftFolder,
            JianyingExe = _svc.JianyingExePath
        };

        bool ok = false;
        try
        {
            ok = await _pipeline.RunAsync(SafeLog, _cts.Token);
        }
        finally
        {
            _btnStart.Enabled = true;
            _btnRestore.Enabled = _pipeline?.LastBackupDir != null;
            _status.Text = ok ? "导完后点「还原」" : "已结束";
        }
    }

    private void DoRestore()
    {
        if (_pipeline == null)
        {
            Log("没有可还原的备份");
            return;
        }
        _pipeline.Restore(SafeLog);
        _status.Text = "已还原（无需关草稿，看时间线是否已闪回）";
    }

    private void Log(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        if (_log.InvokeRequired)
            _log.BeginInvoke(() => _log.AppendText(line + Environment.NewLine));
        else
            _log.AppendText(line + Environment.NewLine);
    }

    private void SafeLog(string msg) => Log(msg);
}
