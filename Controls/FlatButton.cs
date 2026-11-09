namespace JyPrecomp.Controls;

/// <summary>扁平按钮：小圆角、无焦点虚线、无系统高亮色。</summary>
public class FlatButton : Button
{
    private bool _primary = true;
    private bool _hover;

    public int CornerRadius { get; set; } = 4;

    public bool IsPrimary
    {
        get => _primary;
        set { _primary = value; Invalidate(); }
    }

    public FlatButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        FlatAppearance.CheckedBackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.FontBody;
        Height = 34;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override bool ShowFocusCues => false;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(Parent?.BackColor ?? Theme.BgCard);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.BgCard);

        Color fill, text;
        if (_primary)
        {
            fill = _hover ? Theme.AccentHover : Theme.Accent;
            text = Theme.TextOnAccent;
        }
        else
        {
            fill = _hover ? Theme.BgCardAlt : Theme.BgCard;
            text = Theme.TextPrimary;
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect(rect, CornerRadius);
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        if (!_primary)
        {
            using var pen = new Pen(Theme.Border, 1);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        int d = Math.Max(1, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
