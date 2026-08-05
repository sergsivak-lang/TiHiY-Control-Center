using System.Reflection;
using System.Runtime.CompilerServices;
using TiHiY.SystemOptimizer.Core;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.UI;

internal static class StreamingUiInstaller
{
    private static readonly ConditionalWeakTable<MainForm, State> States = new();

    public static void Attach(MainForm form)
    {
        if (States.TryGetValue(form, out _)) return;

        var pageHost = GetPrivateField<Panel>(form, "_pageHost");
        var nav = FindNavigationPanel(form);
        if (pageHost is null || nav is null) return;

        var state = new State(pageHost);
        state.Page = CreatePage(state);
        state.NavButton = CreateNavButton("Стрім");
        States.Add(form, state);

        pageHost.Controls.Add(state.Page);
        state.Page.Visible = false;
        state.Page.SendToBack();

        state.NavButton.Click += (_, _) => ShowStreamingPage(form);
        nav.Controls.Add(state.NavButton);
        nav.Controls.SetChildIndex(state.NavButton, Math.Min(3, nav.Controls.Count - 1));

        foreach (var button in nav.Controls.OfType<Button>().Where(button => !ReferenceEquals(button, state.NavButton)))
        {
            button.Click += (_, _) =>
            {
                state.Page.Visible = false;
                SetNavActive(state.NavButton, false);
            };
        }

        state.RefreshButton.Click += async (_, _) => await RefreshAsync(form, state, true);
        state.OpenObsConfigButton.Click += (_, _) => OpenFolder(state.ObsConfigPath);
    }

    public static void ShowStreamingPage(MainForm form)
    {
        if (!States.TryGetValue(form, out var state)) return;

        foreach (Control control in state.PageHost.Controls) control.Visible = false;
        state.Page.Visible = true;
        state.Page.BringToFront();
        SetNavActive(state.NavButton, true);

        var snapshot = GetPrivateField<SystemSnapshot>(form, "_snapshot");
        if (snapshot is not null)
        {
            UpdatePage(state, snapshot);
        }
        else
        {
            _ = RefreshAsync(form, state, true);
        }
    }

    private static async Task RefreshAsync(MainForm form, State state, bool forceScan)
    {
        state.RefreshButton.Enabled = false;
        state.ObsStatusLabel.Text = "Читаємо конфігурацію OBS та перевіряємо запущені програми…";
        state.ObsStatusLabel.ForeColor = Theme.Muted;
        try
        {
            var snapshot = forceScan
                ? await new SystemScanner().ScanAsync()
                : GetPrivateField<SystemSnapshot>(form, "_snapshot") ?? await new SystemScanner().ScanAsync();
            UpdatePage(state, snapshot);
        }
        catch (Exception exception)
        {
            state.ObsStatusLabel.Text = $"Не вдалося завершити аудит: {exception.Message}";
            state.ObsStatusLabel.ForeColor = Theme.Warning;
        }
        finally
        {
            state.RefreshButton.Enabled = true;
        }
    }

    private static Panel CreatePage(State state)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(26, 18, 26, 22),
            BackColor = Theme.Window
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 272F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateHeader(state), 0, 0);
        layout.Controls.Add(CreateObsCard(state), 0, 1);
        layout.Controls.Add(CreateLowerCards(state), 0, 2);
        host.Controls.Add(layout);
        return host;
    }

    private static Control CreateHeader(State state)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(new Label
        {
            Text = "Стрім",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            BackColor = Theme.Window
        }, 0, 0);
        var description = new Label
        {
            Text = "Читаємо налаштування, але нічого в OBS, Discord або Sonar не змінюємо без окремого підтвердження.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 9F),
            AutoEllipsis = true,
            BackColor = Theme.Window
        };
        header.Controls.Add(description, 0, 1);
        header.SetColumnSpan(description, 2);
        state.RefreshButton.Text = "Оновити";
        state.RefreshButton.Dock = DockStyle.Fill;
        state.RefreshButton.Margin = new Padding(14, 5, 0, 2);
        ConfigureSecondaryButton(state.RefreshButton);
        header.Controls.Add(state.RefreshButton, 1, 0);
        return header;
    }

    private static Control CreateObsCard(State state)
    {
        var card = NewCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layout.Controls.Add(new Label
        {
            Text = "OBS Studio",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        state.ObsStatusLabel.Dock = DockStyle.Fill;
        state.ObsStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.ObsStatusLabel.Font = new Font("Segoe UI Semibold", 8.6F, FontStyle.Bold);
        state.ObsStatusLabel.AutoEllipsis = true;
        state.ObsStatusLabel.BackColor = Theme.Card;
        layout.Controls.Add(state.ObsStatusLabel, 0, 1);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        for (var index = 0; index < 3; index++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.Controls.Add(CreateMetric("ПРОФІЛЬ", state.ObsProfileLabel), 0, 0);
        metrics.Controls.Add(CreateMetric("ПОЛОТНО", state.ObsCanvasLabel), 1, 0);
        metrics.Controls.Add(CreateMetric("ВИХІД", state.ObsOutputLabel), 2, 0);
        metrics.Controls.Add(CreateMetric("FPS", state.ObsFpsLabel), 0, 1);
        metrics.Controls.Add(CreateMetric("ЕНКОДЕР", state.ObsEncoderLabel), 1, 1);
        metrics.Controls.Add(CreateMetric("АУДІО", state.ObsAudioLabel), 2, 1);
        layout.Controls.Add(metrics, 0, 2);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        state.ObsModeLabel.Dock = DockStyle.Fill;
        state.ObsModeLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.ObsModeLabel.ForeColor = Theme.Subtle;
        state.ObsModeLabel.Font = new Font("Segoe UI", 8.2F);
        state.ObsModeLabel.BackColor = Theme.Card;
        footer.Controls.Add(state.ObsModeLabel, 0, 0);
        state.OpenObsConfigButton.Text = "Відкрити конфігурацію";
        state.OpenObsConfigButton.Dock = DockStyle.Fill;
        state.OpenObsConfigButton.Margin = new Padding(10, 2, 0, 2);
        ConfigureSecondaryButton(state.OpenObsConfigButton);
        footer.Controls.Add(state.OpenObsConfigButton, 1, 0);
        layout.Controls.Add(footer, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateLowerCards(State state)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(CreateRuntimeCard(
            "Discord",
            "Перевіряємо лише наявність і чи запущений клієнт. Канали, голос та гучність не змінюємо.",
            state.DiscordStatusLabel), 0, 0);
        var sonar = CreateRuntimeCard(
            "SteelSeries GG / Sonar",
            "Перевіряємо GG і процеси Sonar. Поточну маршрутизацію мікрофона, гри й чату не переписуємо.",
            state.SonarStatusLabel);
        sonar.Margin = new Padding(6, 0, 0, 0);
        grid.Controls.Add(sonar, 1, 0);
        return grid;
    }

    private static RoundedPanel CreateRuntimeCard(string title, string description, Label status)
    {
        var card = NewCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.5F),
            BackColor = Theme.Card
        }, 0, 1);
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        status.AutoEllipsis = true;
        status.BackColor = Theme.Card;
        layout.Controls.Add(status, 0, 2);
        layout.Controls.Add(new Label
        {
            Text = "Аудит лише читає стан — автоматичних змін тут немає.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI", 7.8F),
            BackColor = Theme.Card
        }, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateMetric(string caption, Label value)
    {
        var cell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 2, 10, 2),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        cell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cell.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7.2F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.TopLeft;
        value.ForeColor = Theme.Text;
        value.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        value.AutoEllipsis = true;
        value.BackColor = Theme.Card;
        cell.Controls.Add(value, 0, 1);
        return cell;
    }

    private static RoundedPanel NewCard() => new()
    {
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = new Padding(18, 15, 18, 15),
        Radius = 18,
        BackColor = Theme.Card,
        BorderColor = Theme.Border
    };

    private static void UpdatePage(State state, SystemSnapshot snapshot)
    {
        var audit = snapshot.StreamAudit;
        if (!snapshot.ObsFound)
        {
            state.ObsStatusLabel.Text = "OBS Studio не знайдено.";
            state.ObsStatusLabel.ForeColor = Theme.Warning;
        }
        else if (!audit.ObsConfigFound)
        {
            state.ObsStatusLabel.Text = audit.ObsRunning
                ? "OBS запущено, але папку конфігурації не знайдено."
                : "OBS встановлено, папку конфігурації не знайдено.";
            state.ObsStatusLabel.ForeColor = Theme.Warning;
        }
        else
        {
            state.ObsStatusLabel.Text = audit.ObsRunning
                ? "OBS знайдено • зараз запущено • конфігурацію прочитано"
                : "OBS знайдено • зараз закрито • конфігурацію прочитано";
            state.ObsStatusLabel.ForeColor = Theme.Good;
        }

        state.ObsProfileLabel.Text = ValueOrDash(audit.ObsProfileName);
        state.ObsCanvasLabel.Text = ValueOrDash(audit.ObsCanvasResolution);
        state.ObsOutputLabel.Text = ValueOrDash(audit.ObsOutputResolution);
        state.ObsFpsLabel.Text = ValueOrDash(audit.ObsFps);
        state.ObsEncoderLabel.Text = ValueOrDash(audit.ObsStreamEncoder);
        state.ObsAudioLabel.Text = ValueOrDash(audit.ObsAudioSampleRate);
        state.ObsModeLabel.Text = $"Режим виводу: {HumanizeOutputMode(audit.ObsOutputMode)}";
        state.ObsConfigPath = audit.ObsConfigRoot;
        state.OpenObsConfigButton.Enabled = !string.IsNullOrWhiteSpace(audit.ObsConfigRoot) && Directory.Exists(audit.ObsConfigRoot);

        state.DiscordStatusLabel.Text = snapshot.DiscordFound
            ? audit.DiscordRunning ? "Встановлено • зараз запущено" : "Встановлено • зараз закрито"
            : "Discord не знайдено";
        state.DiscordStatusLabel.ForeColor = snapshot.DiscordFound ? Theme.Good : Theme.Subtle;

        if (!snapshot.SteelSeriesFound)
        {
            state.SonarStatusLabel.Text = "SteelSeries GG не знайдено";
            state.SonarStatusLabel.ForeColor = Theme.Subtle;
        }
        else
        {
            var gg = audit.SteelSeriesRunning ? "GG запущено" : "GG закрито";
            var sonar = audit.SonarRunning ? "Sonar процес виявлено" : "Sonar процес не виявлено";
            state.SonarStatusLabel.Text = $"Встановлено • {gg} • {sonar}";
            state.SonarStatusLabel.ForeColor = Theme.Good;
        }
    }

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "Не визначено" : value;

    private static string HumanizeOutputMode(string? mode)
    {
        if (string.Equals(mode, "Advanced", StringComparison.OrdinalIgnoreCase)) return "Розширений";
        if (string.Equals(mode, "Simple", StringComparison.OrdinalIgnoreCase)) return "Простий";
        return ValueOrDash(mode);
    }

    private static void SetNavActive(Button button, bool active)
    {
        button.BackColor = active ? Theme.AccentSoft : Theme.Sidebar;
        button.ForeColor = active ? Theme.Accent : Theme.Muted;
    }

    private static Button CreateNavButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 180,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Theme.Sidebar,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = Theme.Surface;
        return button;
    }

    private static void ConfigureSecondaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Theme.Border;
        button.FlatAppearance.MouseOverBackColor = Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = Theme.Surface;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Text;
        button.Font = new Font("Segoe UI Semibold", 8.7F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
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

    private static T? GetPrivateField<T>(MainForm form, string name) where T : class
        => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form) as T;

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private sealed class State
    {
        public State(Panel pageHost) => PageHost = pageHost;
        public Panel PageHost { get; }
        public Panel Page { get; set; } = null!;
        public Button NavButton { get; set; } = null!;
        public Button RefreshButton { get; } = new();
        public Label ObsStatusLabel { get; } = new();
        public Label ObsProfileLabel { get; } = new();
        public Label ObsCanvasLabel { get; } = new();
        public Label ObsOutputLabel { get; } = new();
        public Label ObsFpsLabel { get; } = new();
        public Label ObsEncoderLabel { get; } = new();
        public Label ObsAudioLabel { get; } = new();
        public Label ObsModeLabel { get; } = new();
        public Button OpenObsConfigButton { get; } = new();
        public Label DiscordStatusLabel { get; } = new();
        public Label SonarStatusLabel { get; } = new();
        public string? ObsConfigPath { get; set; }
    }
}
