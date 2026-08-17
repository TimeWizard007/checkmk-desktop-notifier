using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace CheckmkDesktopNotifier.App;

internal sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private static readonly Color Background = Color.FromArgb(255, 37, 42, 51);
    private static readonly Color Border = Color.FromArgb(255, 58, 65, 80);
    private static readonly Color Hover = Color.FromArgb(255, 58, 65, 80);

    public DarkTrayMenuRenderer()
        : base(new DarkTrayMenuColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        var bounds = e.AffectedBounds;
        using var pen = new Pen(Border);
        e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
    {
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var fill = e.Item.Selected ? Hover : Background;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillRectangle(brush, bounds);
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        var y = Math.Max(0, e.Item.Height / 2);
        using var pen = new Pen(Color.FromArgb(140, 58, 65, 80), 1f);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.DrawLine(pen, 8, y, Math.Max(8, e.Item.Width - 8), y);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    private sealed class DarkTrayMenuColorTable : Forms.ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuStripGradientBegin => Background;
        public override Color MenuStripGradientEnd => Background;
        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemBorder => Background;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
