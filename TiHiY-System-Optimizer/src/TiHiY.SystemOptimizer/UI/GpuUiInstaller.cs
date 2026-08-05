using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TiHiY.SystemOptimizer.Core;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.UI;

internal static class GpuUiInstaller
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
        state.NavButton = CreateNavButton("NVIDIA / GPU");
        States.Add(form, state);
        pageHost.Controls.Add(state.Page);
        state.Page.Visible = false;
        state.Page.SendToBack();
        nav.Controls.Add(state.NavButton);

        var startup = nav.Controls.OfType<Button>().FirstOrDefault(button => button.Text == "Автозапуск");
        if (startup is not null)
        {
            var index = nav.Controls.GetChildIndex(startup);
            nav.Controls.SetChildIndex(state.NavButton, Math.Min(index + 1, nav.Controls.Count - 1));
        }

        state.NavButton.Click += (_, _) => ShowPage(form);
        foreach (var button in nav.Controls.OfType<Button>().Where(button => !ReferenceEquals(button, state.NavButton)))
        {
            button.Click += (_, _) =>
            {
                state.Page.Visible = false;
                SetNavActive(state.NavButton, false);
            };
        }

        state.RefreshButton.Click += async (_, _) => await RefreshAsync(state);
        state.GraphicsSettingsButton.Click += (_, _) => OpenGraphicsSettings();
    }

    public static void ShowPage(MainForm form)
    {
        if (!States.TryGetValue(form, out var state)) return;
        foreach (Control control in state.PageHost.Controls) control.Visible = false;
        state.Page.Visible = true;
        state.Page.BringToFront();
        SetNavActive(state.NavButton, true);
        _ = RefreshAsync(state);
    }

    private static async Task RefreshAsync(State state)
    {
        if (state.Busy) return;
        state.Busy = true;
        state.RefreshButton.Enabled = false;
        state.StatusLabel.Text = "Читаємо драйвер NVIDIA та поточний стан GPU…";
        state.StatusLabel.ForeColor = Theme.Muted;
        try
        {
            var snapshot = await state.Service.ScanAsync();
            UpdatePage(state, snapshot);
        }
        catch (Exception exception)
        {
            state.StatusLabel.Text = $"Не вдалося завершити аудит GPU: {exception.Message}";
            state.StatusLabel.ForeColor = Theme.Warning;
        }
        finally
        {
            state.Busy = false;
            state.RefreshButton.Enabled = true;
        }
    }

    private static Panel CreatePage(State state)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(26, 16, 26, 18),
            BackColor = Theme.Window
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 166F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 172F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateHeader(state), 0, 0);
        layout.Controls.Add(CreateGpuCard(state), 0, 1);
        layout.Controls.Add(CreateLiveCard(state), 0, 2);
        layout.Controls.Add(CreateWindowsCard(state), 0, 3);
        host.Controls.Add(layout);
        return host;
    }

    private static Control CreateHeader(State state)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 41F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.Controls.Add(new Label
        {
            Text = "NVIDIA / GPU",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 21F, FontStyle.Bold),
            BackColor = Theme.Window
        }, 0, 0);
        state.RefreshButton.Text = "Оновити";
        state.RefreshButton.Dock = DockStyle.Fill;
        state.RefreshButton.Margin = new Padding(12, 4, 0, 2);
        ConfigureSecondaryButton(state.RefreshButton);
        grid.Controls.Add(state.RefreshButton, 1, 0);
        state.StatusLabel.Text = "Показуємо реальні дані драйвера й GPU без сумнівних автоматичних твиків.";
        state.StatusLabel.Dock = DockStyle.Fill;
        state.StatusLabel.TextAlign = ContentAlignment.TopLeft;
        state.StatusLabel.ForeColor = Theme.Muted;
        state.StatusLabel.Font = new Font("Segoe UI", 8.8F);
        state.StatusLabel.AutoEllipsis = true;
        state.StatusLabel.BackColor = Theme.Window;
        grid.Controls.Add(state.StatusLabel, 0, 1);
        grid.SetColumnSpan(state.StatusLabel, 2);
        return grid;
    }

    private static Control CreateGpuCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 10);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(new Label
        {
            Text = "Відеокарта та драйвер",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        state.GpuNameLabel.Dock = DockStyle.Fill;
        state.GpuNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.GpuNameLabel.ForeColor = Theme.Accent;
        state.GpuNameLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        state.GpuNameLabel.AutoEllipsis = true;
        state.GpuNameLabel.BackColor = Theme.Card;
        layout.Controls.Add(state.GpuNameLabel, 0, 1);
        var metrics = NewMetrics(3);
        metrics.Controls.Add(CreateMetric("ДРАЙВЕР", state.DriverLabel), 0, 0);
        metrics.Controls.Add(CreateMetric("ДАТА ДРАЙВЕРА", state.DriverDateLabel), 1, 0);
        metrics.Controls.Add(CreateMetric("ВІДЕОПАМ’ЯТЬ", state.VramLabel), 2, 0);
        layout.Controls.Add(metrics, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateLiveCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 10);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.Controls.Add(new Label
        {
            Text = "Стан зараз",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        var metrics = NewMetrics(3);
        metrics.Controls.Add(CreateMetric("НАВАНТАЖЕННЯ GPU", state.UtilizationLabel), 0, 0);
        metrics.Controls.Add(CreateMetric("ТЕМПЕРАТУРА", state.TemperatureLabel), 1, 0);
        metrics.Controls.Add(CreateMetric("ВИКОРИСТАНО VRAM", state.MemoryUsageLabel), 2, 0);
        layout.Controls.Add(metrics, 0, 1);
        state.LiveHintLabel.Dock = DockStyle.Fill;
        state.LiveHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.LiveHintLabel.ForeColor = Theme.Subtle;
        state.LiveHintLabel.Font = new Font("Segoe UI", 7.8F);
        state.LiveHintLabel.BackColor = Theme.Card;
        layout.Controls.Add(state.LiveHintLabel, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateWindowsCard(State state)
    {
        var card = NewCard();
        card.Padding = new Padding(18, 14, 18, 14);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.Controls.Add(new Label
        {
            Text = "Графіка Windows",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        state.HagsLabel.Dock = DockStyle.Fill;
        state.HagsLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.HagsLabel.ForeColor = Theme.Good;
        state.HagsLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        state.HagsLabel.BackColor = Theme.Card;
        grid.Controls.Add(state.HagsLabel, 0, 1);
        state.NoteLabel.Text = "Resizable BAR тут не «вгадуємо» по розміру BAR1 або інших непрямих ознаках. Якщо не можемо визначити параметр надійно — показуємо це чесно, а не видаємо випадковий результат.";
        state.NoteLabel.Dock = DockStyle.Fill;
        state.NoteLabel.TextAlign = ContentAlignment.TopLeft;
        state.NoteLabel.ForeColor = Theme.Muted;
        state.NoteLabel.Font = new Font("Segoe UI", 8.2F);
        state.NoteLabel.AutoEllipsis = true;
        state.NoteLabel.BackColor = Theme.Card;
        grid.Controls.Add(state.NoteLabel, 0, 2);
        state.GraphicsSettingsButton.Text = "Параметри графіки Windows";
        state.GraphicsSettingsButton.Dock = DockStyle.Fill;
        state.GraphicsSettingsButton.Margin = new Padding(12, 4, 0, 4);
        ConfigureSecondaryButton(state.GraphicsSettingsButton);
        grid.Controls.Add(state.GraphicsSettingsButton, 1, 0);
        grid.SetRowSpan(state.GraphicsSettingsButton, 2);
        card.Controls.Add(grid);
        return card;
    }

    private static TableLayoutPanel NewMetrics(int count)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = count,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        for (var index = 0; index < count; index++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / count));
        return grid;
    }

    private static Control CreateMetric(string caption, Label value)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 3, 12, 3),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.TopLeft;
        value.ForeColor = Theme.Text;
        value.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        value.AutoEllipsis = true;
        value.BackColor = Theme.Card;
        grid.Controls.Add(value, 0, 1);
        return grid;
    }

    private static void UpdatePage(State state, GpuAuditSnapshot snapshot)
    {
        state.GpuNameLabel.Text = snapshot.GpuName;
        state.DriverLabel.Text = snapshot.DriverVersion;
        state.DriverDateLabel.Text = snapshot.DriverDate;
        state.VramLabel.Text = snapshot.Vram;
        state.UtilizationLabel.Text = snapshot.Utilization;
        state.TemperatureLabel.Text = snapshot.Temperature;
        state.MemoryUsageLabel.Text = snapshot.MemoryUsage;
        state.HagsLabel.Text = $"Апаратне планування GPU: {snapshot.HagsState}";
        state.HagsLabel.ForeColor = snapshot.HagsState.Contains("Увімк", StringComparison.OrdinalIgnoreCase) ? Theme.Good : Theme.Warning;
        state.LiveHintLabel.Text = snapshot.NvidiaSmiAvailable
            ? "Поточні значення отримані безпосередньо від NVIDIA через nvidia-smi."
            : "nvidia-smi не знайдено — драйвер читається через Windows, а живі показники не відображаються.";
        state.StatusLabel.Text = snapshot.NvidiaFound
            ? "NVIDIA знайдено. Дані оновлено. Нічого в драйвері автоматично не змінюємо."
            : "NVIDIA не знайдено. Сторінка залишиться інформаційною.";
        state.StatusLabel.ForeColor = snapshot.NvidiaFound ? Theme.Muted : Theme.Warning;
    }

    private static void OpenGraphicsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:display-advancedgraphics") { UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }); } catch { }
        }
    }

    private static RoundedPanel NewCard() => new()
    {
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = new Padding(18, 14, 18, 14),
        Radius = 18,
        BackColor = Theme.Card,
        BorderColor = Theme.Border
    };

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
        button.Font = new Font("Segoe UI Semibold", 8.4F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
    }

    private static void SetNavActive(Button button, bool active)
    {
        button.BackColor = active ? Theme.AccentSoft : Theme.Sidebar;
        button.ForeColor = active ? Theme.Accent : Theme.Muted;
    }

    private static FlowLayoutPanel? FindNavigationPanel(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is FlowLayoutPanel panel && panel.Controls.OfType<Button>().Any(button => button.Text == "Огляд системи")) return panel;
            var nested = FindNavigationPanel(control);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static T? GetPrivateField<T>(MainForm form, string name) where T : class
        => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form) as T;

    private sealed class State
    {
        public State(Panel pageHost) => PageHost = pageHost;
        public GpuAuditService Service { get; } = new();
        public Panel PageHost { get; }
        public Panel Page { get; set; } = null!;
        public Button NavButton { get; set; } = null!;
        public Button RefreshButton { get; } = new();
        public Button GraphicsSettingsButton { get; } = new();
        public Label StatusLabel { get; } = new();
        public Label GpuNameLabel { get; } = new();
        public Label DriverLabel { get; } = new();
        public Label DriverDateLabel { get; } = new();
        public Label VramLabel { get; } = new();
        public Label UtilizationLabel { get; } = new();
        public Label TemperatureLabel { get; } = new();
        public Label MemoryUsageLabel { get; } = new();
        public Label HagsLabel { get; } = new();
        public Label LiveHintLabel { get; } = new();
        public Label NoteLabel { get; } = new();
        public bool Busy { get; set; }
    }
}
