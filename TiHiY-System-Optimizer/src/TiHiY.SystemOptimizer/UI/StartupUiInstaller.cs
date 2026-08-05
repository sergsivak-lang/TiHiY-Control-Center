using System.Reflection;
using System.Runtime.CompilerServices;
using TiHiY.SystemOptimizer.Core;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.UI;

internal static class StartupUiInstaller
{
    private const int ItemsPerPage = 5;
    private static readonly ConditionalWeakTable<MainForm, State> States = new();

    public static void Attach(MainForm form)
    {
        if (States.TryGetValue(form, out _)) return;
        var pageHost = GetPrivateField<Panel>(form, "_pageHost");
        var nav = FindNavigationPanel(form);
        if (pageHost is null || nav is null) return;

        var state = new State(pageHost);
        state.Page = CreatePage(state);
        state.NavButton = CreateNavButton("Автозапуск");
        States.Add(form, state);

        pageHost.Controls.Add(state.Page);
        state.Page.Visible = false;
        state.Page.SendToBack();
        nav.Controls.Add(state.NavButton);

        var maintenance = nav.Controls.OfType<Button>().FirstOrDefault(button => button.Text == "Очищення гри");
        if (maintenance is not null)
        {
            var index = nav.Controls.GetChildIndex(maintenance);
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
        state.ApplyButton.Click += async (_, _) => await ApplyAsync(state);
        state.RestoreButton.Click += async (_, _) => await RestoreAsync(state);
        state.PreviousButton.Click += (_, _) => ChangePage(state, -1);
        state.NextButton.Click += (_, _) => ChangePage(state, +1);
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
        SetBusy(state, true, "Перевіряємо, що запускається разом із Windows…");
        try
        {
            state.Snapshot = await state.Service.ScanAsync();
            state.PageIndex = Math.Clamp(state.PageIndex, 0, PageCount(state) - 1);
            UpdatePage(state);
        }
        catch (Exception exception)
        {
            state.StatusLabel.Text = $"Не вдалося прочитати автозапуск: {exception.Message}";
            state.StatusLabel.ForeColor = Theme.Warning;
        }
        finally
        {
            SetBusy(state, false);
        }
    }

    private static async Task ApplyAsync(State state)
    {
        if (state.Busy || state.Snapshot is null) return;
        SaveVisibleSelections(state);
        var selected = state.Snapshot.Items.Where(item => state.SelectedIds.Contains(item.Id)).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show("Не вибрано жодної програми.", "Автозапуск", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var names = string.Join(Environment.NewLine, selected.Select(item => $"• {item.Name}"));
        var confirmation = MessageBox.Show(
            $"Ці програми більше не запускатимуться автоматично після входу в Windows:\n\n{names}\n\nПрограми НЕ видаляються. TiHiY створить резервну копію, щоб усе можна було повернути. Продовжити?",
            "Вимкнути автозапуск",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;

        SetBusy(state, true, "Зберігаємо резервну копію та вимикаємо вибране…");
        try
        {
            var result = await state.Service.DisableAsync(selected, new Progress<string>(name => state.StatusLabel.Text = $"Обробляємо: {name}"));
            MessageBox.Show(
                result.Failed == 0
                    ? $"Готово. Вимкнено з автозапуску: {result.Changed}.\n\nЗа потреби натисніть «Повернути останні зміни»."
                    : $"Готово з попередженнями.\n\nУспішно: {result.Changed}\nНе вдалося: {result.Failed}\n\nЖурнал: {result.BackupFolder}",
                "TiHiY System Optimizer",
                MessageBoxButtons.OK,
                result.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            state.SelectedIds.Clear();
            state.PageIndex = 0;
            await RefreshAsync(state);
        }
        finally
        {
            SetBusy(state, false);
        }
    }

    private static async Task RestoreAsync(State state)
    {
        if (state.Busy || state.Service.GetLatestBackupFolder() is null) return;
        var confirmation = MessageBox.Show(
            "Повернути пункти автозапуску з останньої резервної копії?",
            "Відновлення автозапуску",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;

        SetBusy(state, true, "Відновлюємо автозапуск…");
        try
        {
            var result = await state.Service.RestoreLatestAsync(new Progress<string>(name => state.StatusLabel.Text = $"Повертаємо: {name}"));
            MessageBox.Show(
                result.Failed == 0
                    ? $"Відновлено пунктів: {result.Changed}."
                    : $"Відновлення завершено з попередженнями. Успішно: {result.Changed}; помилок: {result.Failed}.",
                "Автозапуск відновлено",
                MessageBoxButtons.OK,
                result.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            await RefreshAsync(state);
        }
        finally
        {
            SetBusy(state, false);
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        layout.Controls.Add(CreateHeader(state), 0, 0);
        layout.Controls.Add(CreateSummaryCard(state), 0, 1);
        layout.Controls.Add(CreateItemsCard(state), 0, 2);
        layout.Controls.Add(CreateFooter(state), 0, 3);
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
            Text = "Автозапуск Windows",
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
        state.StatusLabel.Text = "Показуємо лише те, що реально запускається разом із Windows.";
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

    private static Control CreateSummaryCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 10);
        card.Padding = new Padding(18, 12, 18, 12);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        grid.Controls.Add(CreateMetric("ЗАПУСКАЄТЬСЯ З WINDOWS", state.TotalLabel), 0, 0);
        grid.Controls.Add(CreateMetric("МОЖНА РОЗГЛЯНУТИ", state.OptionalLabel), 1, 0);
        grid.Controls.Add(CreateMetric("КРАЩЕ ЗАЛИШИТИ", state.KeepLabel), 2, 0);
        card.Controls.Add(grid);
        return card;
    }

    private static Control CreateItemsCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 8);
        card.Padding = new Padding(16, 10, 16, 10);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        header.Controls.Add(new Label
        {
            Text = "Що запускається автоматично",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        state.PreviousButton.Text = "‹";
        state.NextButton.Text = "›";
        ConfigureSecondaryButton(state.PreviousButton);
        ConfigureSecondaryButton(state.NextButton);
        state.PreviousButton.Dock = DockStyle.Fill;
        state.NextButton.Dock = DockStyle.Fill;
        state.PreviousButton.Margin = new Padding(4, 0, 4, 0);
        state.NextButton.Margin = new Padding(4, 0, 0, 0);
        header.Controls.Add(state.PreviousButton, 1, 0);
        header.Controls.Add(state.NextButton, 2, 0);
        layout.Controls.Add(header, 0, 0);

        state.RowsPanel.Dock = DockStyle.Fill;
        state.RowsPanel.ColumnCount = 1;
        state.RowsPanel.RowCount = ItemsPerPage;
        state.RowsPanel.Margin = Padding.Empty;
        state.RowsPanel.Padding = Padding.Empty;
        state.RowsPanel.BackColor = Theme.Card;
        for (var index = 0; index < ItemsPerPage; index++)
        {
            state.RowsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / ItemsPerPage));
            var row = CreateRow();
            state.Rows.Add(row);
            state.RowsPanel.Controls.Add(row.Root, 0, index);
        }
        layout.Controls.Add(state.RowsPanel, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static RowView CreateRow()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 2),
            BackColor = Theme.Card
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        var check = new CheckBox
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            CheckAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.Card,
            Cursor = Cursors.Hand
        };
        root.Controls.Add(check, 0, 0);

        var text = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        var title = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        var detail = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 7.5F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        text.Controls.Add(title, 0, 0);
        text.Controls.Add(detail, 0, 1);
        root.Controls.Add(text, 1, 0);

        var source = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI", 7.5F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        root.Controls.Add(source, 2, 0);
        var advice = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        root.Controls.Add(advice, 3, 0);
        return new RowView(root, check, title, detail, source, advice);
    }

    private static Control CreateFooter(State state)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 9, 0, 0),
            BackColor = Theme.Window
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
        state.PageLabel.Dock = DockStyle.Fill;
        state.PageLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.PageLabel.ForeColor = Theme.Muted;
        state.PageLabel.Font = new Font("Segoe UI Semibold", 8.7F, FontStyle.Bold);
        state.PageLabel.BackColor = Theme.Window;
        grid.Controls.Add(state.PageLabel, 0, 0);
        state.RestoreButton.Text = "Повернути останні";
        state.RestoreButton.Dock = DockStyle.Fill;
        ConfigureSecondaryButton(state.RestoreButton);
        grid.Controls.Add(state.RestoreButton, 1, 0);
        state.ApplyButton.Text = "Вимкнути вибране з автозапуску";
        state.ApplyButton.Dock = DockStyle.Fill;
        ConfigurePrimaryButton(state.ApplyButton);
        grid.Controls.Add(state.ApplyButton, 3, 0);
        return grid;
    }

    private static Control CreateMetric(string caption, Label value)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 12, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 19F));
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
        value.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        value.BackColor = Theme.Card;
        grid.Controls.Add(value, 0, 1);
        return grid;
    }

    private static void UpdatePage(State state)
    {
        if (state.Snapshot is null) return;
        state.TotalLabel.Text = state.Snapshot.Items.Count.ToString();
        state.OptionalLabel.Text = state.Snapshot.OptionalCount.ToString();
        state.KeepLabel.Text = state.Snapshot.KeepCount.ToString();

        var pages = PageCount(state);
        state.PageIndex = Math.Clamp(state.PageIndex, 0, pages - 1);
        var visible = state.Snapshot.Items.Skip(state.PageIndex * ItemsPerPage).Take(ItemsPerPage).ToArray();
        for (var index = 0; index < state.Rows.Count; index++)
        {
            var row = state.Rows[index];
            if (index >= visible.Length)
            {
                row.ItemId = null;
                row.Root.Visible = false;
                continue;
            }
            var item = visible[index];
            row.Root.Visible = true;
            row.ItemId = item.Id;
            row.Title.Text = item.Name;
            row.Detail.Text = item.AdviceText;
            row.Source.Text = string.IsNullOrWhiteSpace(item.Company) ? item.SourceLabel : $"{item.SourceLabel}\n{item.Company}";
            row.Check.Enabled = item.Advice != StartupAdvice.Keep;
            if (!state.InitializedIds.Contains(item.Id))
            {
                if (item.Advice == StartupAdvice.Optional) state.SelectedIds.Add(item.Id);
                state.InitializedIds.Add(item.Id);
            }
            row.Check.Checked = row.Check.Enabled && state.SelectedIds.Contains(item.Id);
            switch (item.Advice)
            {
                case StartupAdvice.Optional:
                    row.Advice.Text = "МОЖНА ВИМКНУТИ";
                    row.Advice.ForeColor = Theme.Warning;
                    break;
                case StartupAdvice.Keep:
                    row.Advice.Text = "ЗАЛИШИТИ";
                    row.Advice.ForeColor = Theme.Good;
                    break;
                default:
                    row.Advice.Text = "НА ВАШ РОЗСУД";
                    row.Advice.ForeColor = Theme.Muted;
                    break;
            }
        }

        state.PreviousButton.Enabled = state.PageIndex > 0;
        state.NextButton.Enabled = state.PageIndex < pages - 1;
        state.RestoreButton.Enabled = !state.Busy && state.Service.GetLatestBackupFolder() is not null;
        state.PageLabel.Text = $"Сторінка {state.PageIndex + 1} з {pages} • вибрано: {state.SelectedIds.Count}";
        state.ApplyButton.Enabled = !state.Busy && state.SelectedIds.Count > 0;
        state.StatusLabel.Text = state.Snapshot.Items.Count == 0
            ? "Автозапуск порожній — додаткових дій не потрібно."
            : $"Знайдено {state.Snapshot.Items.Count} пунктів. TiHiY автоматично відмічає лише відомі необов'язкові програми.";
        state.StatusLabel.ForeColor = Theme.Muted;
    }

    private static void ChangePage(State state, int delta)
    {
        if (state.Snapshot is null) return;
        SaveVisibleSelections(state);
        state.PageIndex = Math.Clamp(state.PageIndex + delta, 0, PageCount(state) - 1);
        UpdatePage(state);
    }

    private static void SaveVisibleSelections(State state)
    {
        foreach (var row in state.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.ItemId) || !row.Check.Enabled) continue;
            if (row.Check.Checked) state.SelectedIds.Add(row.ItemId);
            else state.SelectedIds.Remove(row.ItemId);
        }
    }

    private static int PageCount(State state) => Math.Max(1, (int)Math.Ceiling((state.Snapshot?.Items.Count ?? 0) / (double)ItemsPerPage));

    private static void SetBusy(State state, bool busy, string? text = null)
    {
        state.Busy = busy;
        state.RefreshButton.Enabled = !busy;
        state.PreviousButton.Enabled = !busy && state.PageIndex > 0;
        state.NextButton.Enabled = !busy && state.PageIndex < PageCount(state) - 1;
        state.ApplyButton.Enabled = !busy && state.SelectedIds.Count > 0;
        state.RestoreButton.Enabled = !busy && state.Service.GetLatestBackupFolder() is not null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            state.StatusLabel.Text = text;
            state.StatusLabel.ForeColor = Theme.Muted;
        }
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

    private static void ConfigurePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
        button.FlatAppearance.MouseDownBackColor = Theme.AccentPressed;
        button.BackColor = Theme.Accent;
        button.ForeColor = Theme.Window;
        button.Font = new Font("Segoe UI Semibold", 8.6F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
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
        public StartupAuditService Service { get; } = new();
        public Panel PageHost { get; }
        public Panel Page { get; set; } = null!;
        public Button NavButton { get; set; } = null!;
        public Button RefreshButton { get; } = new();
        public Button ApplyButton { get; } = new();
        public Button RestoreButton { get; } = new();
        public Button PreviousButton { get; } = new();
        public Button NextButton { get; } = new();
        public Label StatusLabel { get; } = new();
        public Label TotalLabel { get; } = new();
        public Label OptionalLabel { get; } = new();
        public Label KeepLabel { get; } = new();
        public Label PageLabel { get; } = new();
        public TableLayoutPanel RowsPanel { get; } = new();
        public List<RowView> Rows { get; } = [];
        public HashSet<string> SelectedIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> InitializedIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public StartupAuditSnapshot? Snapshot { get; set; }
        public int PageIndex { get; set; }
        public bool Busy { get; set; }
    }

    private sealed class RowView
    {
        public RowView(TableLayoutPanel root, CheckBox check, Label title, Label detail, Label source, Label advice)
        {
            Root = root;
            Check = check;
            Title = title;
            Detail = detail;
            Source = source;
            Advice = advice;
            Check.CheckedChanged += (_, _) => { };
        }
        public TableLayoutPanel Root { get; }
        public CheckBox Check { get; }
        public Label Title { get; }
        public Label Detail { get; }
        public Label Source { get; }
        public Label Advice { get; }
        public string? ItemId { get; set; }
    }
}
