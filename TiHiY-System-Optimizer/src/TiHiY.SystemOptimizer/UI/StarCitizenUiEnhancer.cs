using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.UI;

internal static class StarCitizenUiEnhancer
{
    private static readonly ConditionalWeakTable<MainForm, ToolTip> ToolTips = new();

    public static void Attach(MainForm form)
    {
        var button = FindButton(form, "Star Citizen");
        if (button is not null)
        {
            button.Click += (_, _) => Enhance(form);
        }
    }

    public static void Enhance(MainForm form)
    {
        var snapshot = GetPrivateField<SystemSnapshot>(form, "_snapshot");
        var panel = GetPrivateField<TableLayoutPanel>(form, "_scChannelsPanel");
        if (snapshot is null || panel is null) return;

        var tooltip = ToolTips.GetValue(form, _ => new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 150,
            ShowAlways = true
        });

        foreach (Control child in panel.Controls)
        {
            var grid = child.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (grid is null || grid.ColumnCount < 3) continue;

            if (grid.ColumnStyles.Count >= 3 && grid.ColumnStyles[2].SizeType == SizeType.Absolute)
            {
                grid.ColumnStyles[2].Width = 260F;
            }

            if (grid.GetControlFromPosition(0, 0) is not Label channelLabel
                || grid.GetControlFromPosition(2, 0) is not Label cfgLabel)
            {
                continue;
            }

            var installation = snapshot.StarCitizenInstallations.FirstOrDefault(item =>
                string.Equals(item.Channel, channelLabel.Text, StringComparison.OrdinalIgnoreCase));
            if (installation is null)
            {
                continue;
            }

            if (!installation.UserCfgExists)
            {
                cfgLabel.Text = "USER.cfg відсутній";
                cfgLabel.ForeColor = Theme.Subtle;
                cfgLabel.Cursor = Cursors.Default;
                tooltip.SetToolTip(cfgLabel, "Файл USER.cfg у цьому каналі не знайдено.");
                continue;
            }

            var parts = new List<string> { "USER.cfg" };
            if (installation.UserCfgActiveLines > 0)
            {
                parts.Add($"{installation.UserCfgActiveLines} параметрів");
            }
            if (!string.IsNullOrWhiteSpace(installation.UserCfgLanguage))
            {
                parts.Add($"мова: {installation.UserCfgLanguage}");
            }

            cfgLabel.Text = string.Join(" • ", parts);
            cfgLabel.ForeColor = Theme.Good;
            cfgLabel.Cursor = Cursors.Hand;
            var modified = installation.UserCfgLastWriteTime is { } time
                ? $"\nЗмінено: {time:dd.MM.yyyy HH:mm}"
                : string.Empty;
            tooltip.SetToolTip(cfgLabel, $"{installation.UserCfgPath}{modified}\nНатисніть, щоб відкрити файл.");

            if (!Equals(cfgLabel.Tag, "TiHiY.UserCfgOpen"))
            {
                cfgLabel.Tag = "TiHiY.UserCfgOpen";
                cfgLabel.Click += (_, _) => OpenFile(installation.UserCfgPath);
            }
        }

        var summary = GetPrivateField<Label>(form, "_scSummaryLabel");
        if (summary is not null && snapshot.StarCitizenInstallations.Count > 0)
        {
            var withCfg = snapshot.StarCitizenInstallations.Count(item => item.UserCfgExists);
            summary.Text = $"Знайдено каналів: {snapshot.StarCitizenInstallations.Count} • USER.cfg: {withCfg}/{snapshot.StarCitizenInstallations.Count}.";
        }
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося відкрити USER.cfg", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Button? FindButton(Control root, string text)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button && string.Equals(button.Text, text, StringComparison.Ordinal)) return button;
            var nested = FindButton(control, text);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static T? GetPrivateField<T>(MainForm form, string name) where T : class
        => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form) as T;
}
