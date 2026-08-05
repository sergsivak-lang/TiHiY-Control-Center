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
        var compact = form.ClientSize.Height <= 720;
        foreach (var panel in EnumerateControls(form).OfType<TableLayoutPanel>())
        {
            if (panel.RowCount != 2 || panel.ColumnCount != 1) continue;
            var labels = panel.Controls.OfType<Label>().ToArray();
            if (labels.Length != 2) continue;

            var title = labels.FirstOrDefault(label => label.Font.Bold && label.Font.Size >= 8.5F && label.Font.Size <= 9.5F);
            var description = labels.FirstOrDefault(label => !label.Font.Bold && label.Font.Size >= 7F && label.Font.Size <= 8F);
            if (title is null || description is null) continue;

            if (compact)
            {
                panel.RowStyles[0].SizeType = SizeType.Percent;
                panel.RowStyles[0].Height = 100F;
                panel.RowStyles[1].SizeType = SizeType.Absolute;
                panel.RowStyles[1].Height = 0F;
                description.Visible = false;
                title.TextAlign = ContentAlignment.MiddleLeft;
            }
            else
            {
                panel.RowStyles[0].SizeType = SizeType.Percent;
                panel.RowStyles[0].Height = 48F;
                panel.RowStyles[1].SizeType = SizeType.Percent;
                panel.RowStyles[1].Height = 52F;
                description.Visible = true;
                title.TextAlign = ContentAlignment.BottomLeft;
            }
        }

        foreach (var label in EnumerateControls(form).OfType<Label>())
        {
            if (label.Font.Bold
                && label.Font.Size >= 7.8F
                && label.Font.Size <= 8.5F
                && label.ForeColor.ToArgb() == Theme.Accent.ToArgb())
            {
                label.AutoSize = true;
                label.Dock = DockStyle.None;
                label.Anchor = AnchorStyles.Right;
                label.TextAlign = ContentAlignment.MiddleRight;
                label.Margin = new Padding(0, 0, 2, 0);
            }
        }
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
