namespace TiHiY.SystemOptimizer.UI;

internal static class SidebarResponsiveFix
{
    public static void Attach(MainForm form)
    {
        Apply(form);
        form.SizeChanged += (_, _) => Apply(form);
        form.Shown += (_, _) => Apply(form);
    }

    private static void Apply(MainForm form)
    {
        var nav = FindNavigationPanel(form);
        if (nav is null || nav.Parent is not TableLayoutPanel grid) return;

        var compact = form.ClientSize.Height <= 740;
        nav.Margin = compact ? new Padding(0, 6, 0, 0) : new Padding(0, 10, 0, 0);

        foreach (var button in nav.Controls.OfType<Button>())
        {
            button.Height = compact ? 38 : 44;
            button.Margin = compact ? new Padding(0, 0, 0, 4) : new Padding(0, 0, 0, 8);
            button.Width = Math.Max(120, nav.ClientSize.Width);
            button.Font = new Font("Segoe UI Semibold", compact ? 8.5F : 9F, FontStyle.Bold);
        }

        if (grid.RowStyles.Count >= 4)
        {
            grid.RowStyles[0].SizeType = SizeType.Absolute;
            grid.RowStyles[0].Height = compact ? 70F : 82F;
            grid.RowStyles[1].SizeType = SizeType.Absolute;
            grid.RowStyles[1].Height = compact ? 22F : 26F;
            grid.RowStyles[3].SizeType = SizeType.Absolute;
            grid.RowStyles[3].Height = compact ? 96F : 116F;
        }

        var safe = grid.Controls
            .OfType<RoundedPanel>()
            .FirstOrDefault(panel => panel.Controls.OfType<Label>().Any(label => label.Text.StartsWith("ЯК ЦЕ ПРАЦЮЄ", StringComparison.Ordinal)));
        if (safe is null) return;

        safe.Margin = compact ? new Padding(0, 8, 0, 0) : new Padding(0, 14, 0, 0);
        safe.Padding = compact ? new Padding(10, 7, 10, 7) : new Padding(14, 10, 14, 10);
        foreach (var label in safe.Controls.OfType<Label>())
        {
            label.Font = new Font("Segoe UI", compact ? 7.1F : 7.7F);
        }
    }

    private static FlowLayoutPanel? FindNavigationPanel(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is FlowLayoutPanel panel && panel.Controls.OfType<Button>().Any(button => button.Text == "Огляд системи"))
            {
                return panel;
            }
            var nested = FindNavigationPanel(control);
            if (nested is not null) return nested;
        }
        return null;
    }
}
