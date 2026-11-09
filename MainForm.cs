using JyPrecomp.Core;

namespace JyPrecomp;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "JyPrecomp · 剪映预合成";
        Size = new Size(780, 640);
        MinimumSize = new Size(640, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.BgDeep;
        Font = Theme.FontBody;
        DoubleBuffered = true;

        Controls.Add(new MainPage(new AppService()) { Dock = DockStyle.Fill });
    }
}
