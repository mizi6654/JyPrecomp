namespace JyPrecomp;

/// <summary>深色主题配色</summary>
public static class Theme
{
    public static readonly Color BgDeep     = Color.FromArgb(18, 18, 22);
    public static readonly Color BgMain     = Color.FromArgb(24, 24, 30);
    public static readonly Color BgCard     = Color.FromArgb(32, 32, 40);
    public static readonly Color BgCardAlt  = Color.FromArgb(38, 38, 48);
    public static readonly Color BgSidebar  = Color.FromArgb(20, 20, 26);
    public static readonly Color BgInput    = Color.FromArgb(28, 28, 36);
    public static readonly Color BgHover    = Color.FromArgb(255, 255, 255, 18);

    public static readonly Color Border     = Color.FromArgb(50, 50, 60);
    public static readonly Color BorderFocus= Color.FromArgb(124, 92, 255);

    public static readonly Color Accent     = Color.FromArgb(124, 92, 255);
    public static readonly Color AccentHover= Color.FromArgb(148, 120, 255);
    public static readonly Color AccentDark = Color.FromArgb(98, 70, 220);
    public static readonly Color AccentSoft = Color.FromArgb(45, 40, 70);   // 侧栏选中底色（不透明）

    public static readonly Color TextPrimary  = Color.FromArgb(235, 235, 242);
    public static readonly Color TextSecondary= Color.FromArgb(155, 155, 170);
    public static readonly Color TextMuted    = Color.FromArgb(105, 105, 120);
    public static readonly Color TextOnAccent = Color.White;

    public static readonly Color Success = Color.FromArgb(52, 199, 89);
    public static readonly Color Warning = Color.FromArgb(255, 159, 10);
    public static readonly Color Error   = Color.FromArgb(255, 69, 58);

    public static Font FontTitle    => new("Microsoft YaHei UI", 15f, FontStyle.Bold);
    public static Font FontSubtitle => new("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
    public static Font FontBody     => new("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
    public static Font FontSmall    => new("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
    public static Font FontNav      => new("Microsoft YaHei UI", 9.75f, FontStyle.Regular);
    public static Font FontNavBold  => new("Microsoft YaHei UI", 9.75f, FontStyle.Bold);
    public static Font FontMono     => new("Consolas", 9f, FontStyle.Regular);
}
