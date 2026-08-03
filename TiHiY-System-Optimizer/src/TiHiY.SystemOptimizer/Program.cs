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

        Application.Run(new MainForm());
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

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);

        using var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(30, 30),
            ClientSize = new Size(width, height),
            ShowInTaskbar = false
        };

        var completed = false;
        var timer = new System.Windows.Forms.Timer { Interval = 6000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
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
}
