using System.Reflection;
using TiHiY.SystemOptimizer.UI;

namespace TiHiY.SystemOptimizer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (TryRunSnapshotMode(args))
        {
            return;
        }

        var form = CreateMainForm();
        Application.Run(form);
    }

    private static MainForm CreateMainForm()
    {
        var form = new MainForm();
        StreamingUiInstaller.Attach(form);
        AttachDisabledButtonVisuals(form);
        return form;
    }

    private static bool TryRunSnapshotMode(string[] args)
    {
        if (args.Length < 2 || !string.Equals(args[0], "--snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var outputPath = Path.GetFullPath(args[1]);
        var width = args.Length >= 3 && int.TryParse(args[2], out var parsedWidth) ? Math.Max(1040, parsedWidth) : 1280;
        var height = args.Length >= 4 && int.TryParse(args[3], out var parsedHeight) ? Math.Max(700, parsedHeight) : 800;
        var page = args.Length >= 5 ? args[4] : "Overview";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);

        using var form = CreateMainForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(30, 30);
        form.ClientSize = new Size(width, height);
        form.ShowInTaskbar = false;

        var completed = false;
        var timer = new System.Windows.Forms.Timer { Interval = 6000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            SelectSnapshotPage(form, page);
            form.PerformLayout();
            Application.DoEvents();

            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            completed = true;
            form.Close();
        };

        form.Shown += (_, _) => timer.Start();
        Application.Run(form);
        timer.Dispose();

        return completed || File.Exists(outputPath);
    }

    private static void AttachDisabledButtonVisuals(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                var enabledBackColor = button.BackColor;
                var enabledForeColor = button.ForeColor;
                void UpdateAppearance()
                {
                    if (button.Enabled)
                    {
                        button.BackColor = enabledBackColor;
                        button.ForeColor = enabledForeColor;
                    }
                    else
                    {
                        button.BackColor = Theme.CardHover;
                        button.ForeColor = Theme.Subtle;
                    }
                }

                button.EnabledChanged += (_, _) => UpdateAppearance();
                UpdateAppearance();
            }

            AttachDisabledButtonVisuals(control);
        }
    }

    private static void SelectSnapshotPage(MainForm form, string page)
    {
        if (string.Equals(page, "Streaming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(page, "Stream", StringComparison.OrdinalIgnoreCase))
        {
            StreamingUiInstaller.ShowStreamingPage(form);
            return;
        }

        try
        {
            var pageType = typeof(MainForm).GetNestedType("PageKind", BindingFlags.NonPublic);
            var showPage = typeof(MainForm).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pageType is null || showPage is null)
            {
                return;
            }

            var value = Enum.Parse(pageType, page, ignoreCase: true);
            showPage.Invoke(form, [value]);
        }
        catch
        {
            // Overview remains selected when an unknown page name is supplied.
        }
    }
}
