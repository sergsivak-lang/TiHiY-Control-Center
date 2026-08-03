using System.Drawing.Drawing2D;

namespace TiHiY.SystemOptimizer.UI;

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 18;
    public Color BorderColor { get; set; } = Color.Transparent;
    public float BorderThickness { get; set; } = 1f;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rectangle = ClientRectangle;
        rectangle.Width = Math.Max(1, rectangle.Width - 1);
        rectangle.Height = Math.Max(1, rectangle.Height - 1);

        using var path = GetPath(rectangle, Radius);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);

        if (BorderColor != Color.Transparent && BorderThickness > 0)
        {
            using var pen = new Pen(BorderColor, BorderThickness);
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 1 || Height <= 1)
        {
            return;
        }

        using var path = GetPath(ClientRectangle, Radius);
        Region?.Dispose();
        Region = new Region(path);
        Invalidate();
    }

    private static GraphicsPath GetPath(Rectangle rectangle, int radius)
    {
        var safeRadius = Math.Max(1, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2));
        var diameter = safeRadius * 2;
        var path = new GraphicsPath();

        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
