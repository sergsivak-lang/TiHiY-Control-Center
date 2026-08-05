namespace TiHiY.SystemOptimizer.UI;

internal static class StarCitizenMaintenanceResponsiveFix
{
    public static void Attach(MainForm form)
    {
        Apply(form);
        form.SizeChanged += (_, _) => Apply(form);
        form.Shown += (_, _) =>
        {
            Apply(form);
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Apply(form);
                timer.Dispose();
            };
            timer.Start();
        };
    }

    private static void Apply(MainForm form)
    {
        var page = FindMaintenancePage(form);
        if (page is null) return;

        var compact = form.ClientSize.Height <= 720;
        var outerLayout = page.Controls.OfType<TableLayoutPanel>().FirstOrDefault(panel => panel.RowCount == 4 && panel.ColumnCount == 1);
        if (outerLayout is not null && outerLayout.RowStyles.Count >= 4)
        {
            outerLayout.RowStyles[0].SizeType = SizeType.Absolute;
            outerLayout.RowStyles[0].Height = compact ? 62F : 76F;
            outerLayout.RowStyles[1].SizeType = SizeType.Absolute;
            outerLayout.RowStyles[1].Height = compact ? 104F : 126F;
            outerLayout.RowStyles[3].SizeType = SizeType.Absolute;
            outerLayout.RowStyles[3].Height = compact ? 58F : 66F;
        }

        var cleanupTitle = EnumerateControls(page).OfType<Label>()
            .FirstOrDefault(label => string.Equals(label.Text, "Що можна безпечно очистити", StringComparison.Ordinal));
        if (cleanupTitle?.Parent is TableLayoutPanel cleanupLayout && cleanupLayout.RowStyles.Count >= 3)
        {
            cleanupLayout.RowStyles[2].SizeType = SizeType.Absolute;
            cleanupLayout.RowStyles[2].Height = compact ? 28F : 38F;
        }

        foreach (var panel in EnumerateControls(page).OfType<TableLayoutPanel>())
        {
            if (panel.RowCount != 2 || panel.ColumnCount != 1) continue;
            var labels = panel.Controls.OfType<Label>().ToArray();
            if (labels.Length != 2) continue;
            var title = labels.FirstOrDefault(label => label.Font.Bold && label.Font.Size >= 8.5F && label.Font.Size <= 9.5F);
            var description = labels.FirstOrDefault(label => !label.Font.Bold && label.Font.Size >= 7F && label.Font.Size <= 8F);
            if (title is null || description is null) continue;

            panel.RowStyles[0].SizeType = SizeType.Percent;
            panel.RowStyles[0].Height = 48F;
            panel.RowStyles[1].SizeType = SizeType.Percent;
            panel.RowStyles[1].Height = 52F;
            description.Visible = true;
            title.TextAlign = ContentAlignment.BottomLeft;
        }

        foreach (var label in EnumerateControls(page).OfType<Label>())
        {
            if (label.Font.Bold
                && label.Font.Size >= 7.8F
                && label.Font.Size <= 8.5F
                && label.ForeColor.ToArgb() == Theme.Accent.ToArgb())
            {
                label.AutoSize = false;
                label.Dock = DockStyle.Fill;
                label.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                label.TextAlign = ContentAlignment.MiddleRight;
                label.Margin = Padding.Empty;
            }
        }

        var launcherButton = EnumerateControls(page).OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Text, "RSI Launcher / Verify", StringComparison.Ordinal));
        if (launcherButton is not null)
        {
            launcherButton.Text = "Відкрити RSI Launcher";
        }
    }

    private static Control? FindMaintenancePage(Control root)
    {
        var title = EnumerateControls(root).OfType<Label>()
            .FirstOrDefault(label => string.Equals(label.Text, "Очищення Star Citizen", StringComparison.Ordinal));
        if (title is null) return null;

        Control? current = title;
        while (current is not null)
        {
            if (current is Panel panel
                && panel.Dock == DockStyle.Fill
                && panel.Padding.Left == 26
                && panel.Padding.Top == 16)
            {
                return panel;
            }
            current = current.Parent;
        }
        return null;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in EnumerateControls(child)) yield return nested;
        }
    }
}
