using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TiHiY.SystemOptimizer.Core;

namespace TiHiY.SystemOptimizer.UI;

internal static class StarCitizenMaintenanceUiInstaller
{
    private static readonly ConditionalWeakTable<MainForm, State> States = new();
    private static readonly string[] TargetOrder =
    [
        "sc-shaders",
        "rsi-launcher-cache",
        "sc-logs",
        "windows-dx-cache",
        "gpu-driver-cache"
    ];

    public static void Attach(MainForm form)
    {
        if (States.TryGetValue(form, out _)) return;

        var pageHost = GetPrivateField<Panel>(form, "_pageHost");
        var nav = FindNavigationPanel(form);
        if (pageHost is null || nav is null) return;

        var state = new State(pageHost);
        state.Page = CreatePage(state);
        state.NavButton = CreateNavButton("Очищення гри");
        States.Add(form, state);

        pageHost.Controls.Add(state.Page);
        state.Page.Visible = false;
        state.Page.SendToBack();

        state.NavButton.Click += (_, _) => ShowMaintenancePage(form);
        nav.Controls.Add(state.NavButton);

        var starCitizenButton = nav.Controls.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Text, "Star Citizen", StringComparison.OrdinalIgnoreCase));
        if (starCitizenButton is not null)
        {
            var starIndex = nav.Controls.GetChildIndex(starCitizenButton);
            nav.Controls.SetChildIndex(state.NavButton, Math.Min(starIndex + 1, nav.Controls.Count - 1));
        }

        foreach (var button in nav.Controls.OfType<Button>().Where(button => !ReferenceEquals(button, state.NavButton)))
        {
            button.Click += (_, _) =>
            {
                state.Page.Visible = false;
                SetNavActive(state.NavButton, false);
            };
        }

        state.RefreshButton.Click += async (_, _) => await RefreshAsync(state);
        state.BrowseButton.Click += async (_, _) => await BrowseForGameAsync(state);
        state.ResetPathButton.Click += async (_, _) => await ResetManualPathAsync(state);
        state.CleanupButton.Click += async (_, _) => await CleanAsync(state);
        state.LauncherButton.Click += (_, _) => OpenLauncher(state);

        foreach (var row in state.TargetRows.Values)
        {
            row.Check.CheckedChanged += (_, _) => UpdateSelectedSummary(state);
        }
    }

    public static void ShowMaintenancePage(MainForm form)
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
        SetBusy(state, true, "Аналізуємо папки Star Citizen та безпечні кеші…");
        try
        {
            var snapshot = await state.Service.AnalyzeAsync();
            state.Snapshot = snapshot;
            UpdatePage(state, snapshot);
        }
        catch (Exception exception)
        {
            state.StatusLabel.Text = $"Не вдалося завершити аналіз: {exception.Message}";
            state.StatusLabel.ForeColor = Theme.Warning;
        }
        finally
        {
            SetBusy(state, false);
        }
    }

    private static async Task BrowseForGameAsync(State state)
    {
        if (state.Busy) return;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Оберіть папку LIVE / PTU / EPTU / TECH-PREVIEW або папку StarCitizen, що містить ці канали.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(state.Snapshot?.ManualPath) && Directory.Exists(state.Snapshot.ManualPath))
        {
            dialog.InitialDirectory = state.Snapshot.ManualPath;
        }

        if (dialog.ShowDialog() != DialogResult.OK) return;

        var validation = state.Service.ValidateManualPath(dialog.SelectedPath);
        if (!validation.Success)
        {
            MessageBox.Show(
                validation.Message,
                "Star Citizen не знайдено",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            state.Service.SaveManualPath(dialog.SelectedPath);
            MessageBox.Show(
                $"{validation.Message}\n\nШлях збережено. Програма використовуватиме його й після наступного запуску.",
                "Шлях Star Citizen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await RefreshAsync(state);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося зберегти шлях", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static async Task ResetManualPathAsync(State state)
    {
        if (state.Busy) return;
        state.Service.ClearManualPath();
        await RefreshAsync(state);
    }

    private static async Task CleanAsync(State state)
    {
        if (state.Busy || state.Snapshot is null) return;

        var selectedIds = state.TargetRows
            .Where(pair => pair.Value.Check.Checked && pair.Value.Check.Enabled)
            .Select(pair => pair.Key)
            .ToArray();
        if (selectedIds.Length == 0)
        {
            MessageBox.Show("Не вибрано жодної категорії для очищення.", "Очищення Star Citizen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedTargets = selectedIds
            .Select(id => state.Snapshot.Targets.First(target => string.Equals(target.Id, id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var total = selectedTargets.Sum(target => target.EstimatedBytes);
        var names = string.Join(Environment.NewLine, selectedTargets.Select(target => $"• {target.Title} — {FormatBytes(target.EstimatedBytes)}"));
        var globalWarning = selectedTargets.Any(target => target.AffectsAllGames)
            ? "\n\nУ виборі є глобальний кеш Windows/GPU — він стосується також інших ігор."
            : string.Empty;

        var confirmation = MessageBox.Show(
            $"Буде очищено:\n\n{names}\n\nОрієнтовно: {FormatBytes(total)}.{globalWarning}\n\nTiHiY НЕ видаляє USER.cfg, Controls/Mappings, профілі керування, Data.p4k, виконувані файли гри, Cookies/авторизацію RSI Launcher чи бібліотеку ігор.\n\nПродовжити?",
            "Глибоке безпечне очищення",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;

        SetBusy(state, true, "Очищення…");
        try
        {
            var progress = new Progress<string>(message =>
            {
                state.StatusLabel.Text = $"Очищаємо: {message}";
                state.StatusLabel.ForeColor = Theme.Muted;
            });
            var result = await state.Service.CleanAsync(state.Snapshot, selectedIds, progress);
            var icon = result.FailedItems == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(
                result.FailedItems == 0
                    ? $"Очищення завершено.\n\nВидалено: {FormatBytes(result.RemovedBytes)}\nОб'єктів: {result.RemovedItems}\n\nЖурнал:\n{result.LogPath}"
                    : $"Очищення завершено з попередженнями.\n\nВидалено: {FormatBytes(result.RemovedBytes)}\nУспішно: {result.RemovedItems}\nНе вдалося: {result.FailedItems}\n\nЖурнал:\n{result.LogPath}",
                "TiHiY System Optimizer",
                MessageBoxButtons.OK,
                icon);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося очистити кеш", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(state, false);
            await RefreshAsync(state);
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        layout.Controls.Add(CreateHeader(state), 0, 0);
        layout.Controls.Add(CreatePathCard(state), 0, 1);
        layout.Controls.Add(CreateCleanupCard(state), 0, 2);
        layout.Controls.Add(CreateFooter(state), 0, 3);
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
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 41F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(new Label
        {
            Text = "Очищення Star Citizen",
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
        header.Controls.Add(state.RefreshButton, 1, 0);
        state.StatusLabel.Text = "Безпечне очищення без скидання налаштувань гри.";
        state.StatusLabel.Dock = DockStyle.Fill;
        state.StatusLabel.TextAlign = ContentAlignment.TopLeft;
        state.StatusLabel.ForeColor = Theme.Muted;
        state.StatusLabel.Font = new Font("Segoe UI", 8.8F);
        state.StatusLabel.AutoEllipsis = true;
        state.StatusLabel.BackColor = Theme.Window;
        header.Controls.Add(state.StatusLabel, 0, 1);
        header.SetColumnSpan(state.StatusLabel, 2);
        return header;
    }

    private static Control CreatePathCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 10);
        card.Padding = new Padding(17, 12, 17, 12);

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
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));

        grid.Controls.Add(new Label
        {
            Text = "ПАПКА ГРИ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7.3F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);

        state.PathStatusLabel.Dock = DockStyle.Fill;
        state.PathStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.PathStatusLabel.ForeColor = Theme.Text;
        state.PathStatusLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        state.PathStatusLabel.AutoEllipsis = true;
        state.PathStatusLabel.BackColor = Theme.Card;
        grid.Controls.Add(state.PathStatusLabel, 0, 1);

        state.PathValueLabel.Dock = DockStyle.Fill;
        state.PathValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.PathValueLabel.ForeColor = Theme.Muted;
        state.PathValueLabel.Font = new Font("Segoe UI", 8F);
        state.PathValueLabel.AutoEllipsis = true;
        state.PathValueLabel.BackColor = Theme.Card;
        grid.Controls.Add(state.PathValueLabel, 0, 2);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(12, 0, 0, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        state.BrowseButton.Text = "Вибрати папку гри";
        state.BrowseButton.Dock = DockStyle.Fill;
        state.BrowseButton.Margin = new Padding(0, 0, 5, 4);
        ConfigurePrimaryButton(state.BrowseButton);
        actions.Controls.Add(state.BrowseButton, 0, 0);
        actions.SetColumnSpan(state.BrowseButton, 2);

        state.ResetPathButton.Text = "Скинути шлях";
        state.ResetPathButton.Dock = DockStyle.Fill;
        state.ResetPathButton.Margin = new Padding(0, 4, 5, 0);
        ConfigureSecondaryButton(state.ResetPathButton);
        actions.Controls.Add(state.ResetPathButton, 0, 1);

        state.LauncherButton.Text = "RSI Launcher / Verify";
        state.LauncherButton.Dock = DockStyle.Fill;
        state.LauncherButton.Margin = new Padding(5, 4, 0, 0);
        ConfigureSecondaryButton(state.LauncherButton);
        actions.Controls.Add(state.LauncherButton, 1, 1);

        grid.Controls.Add(actions, 1, 0);
        grid.SetRowSpan(actions, 3);
        card.Controls.Add(grid);
        return card;
    }

    private static Control CreateCleanupCard(State state)
    {
        var card = NewCard();
        card.Margin = new Padding(0, 0, 0, 8);
        card.Padding = new Padding(17, 11, 17, 11);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.Controls.Add(new Label
        {
            Text = "Що можна безпечно очистити",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);

        var targets = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = TargetOrder.Length,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        for (var index = 0; index < TargetOrder.Length; index++)
        {
            targets.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / TargetOrder.Length));
            var row = CreateTargetRow();
            state.TargetRows[TargetOrder[index]] = row;
            targets.Controls.Add(row.Root, 0, index);
        }
        layout.Controls.Add(targets, 0, 1);

        layout.Controls.Add(new Label
        {
            Text = "Окремий «netcode cache» не видаляємо: безпечного офіційного мережевого кешу Star Citizen для цього немає. DNS, Winsock і мережеві настройки Windows не скидаємо — це не покращує netcode і може створити проблеми.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI", 7.6F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        }, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private static TargetRow CreateTargetRow()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 1, 0, 1),
            BackColor = Theme.Card
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));

        var check = new CheckBox
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            CheckAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Theme.Card
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
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        var title = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.9F, FontStyle.Bold),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        var description = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 7.5F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        };
        text.Controls.Add(title, 0, 0);
        text.Controls.Add(description, 0, 1);
        root.Controls.Add(text, 1, 0);

        var size = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
            BackColor = Theme.Card
        };
        root.Controls.Add(size, 2, 0);
        return new TargetRow(root, check, title, description, size);
    }

    private static Control CreateFooter(State state)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 9, 0, 0),
            BackColor = Theme.Window
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265F));
        state.SelectedSizeLabel.Dock = DockStyle.Fill;
        state.SelectedSizeLabel.TextAlign = ContentAlignment.MiddleLeft;
        state.SelectedSizeLabel.ForeColor = Theme.Muted;
        state.SelectedSizeLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        state.SelectedSizeLabel.BackColor = Theme.Window;
        footer.Controls.Add(state.SelectedSizeLabel, 0, 0);
        state.CleanupButton.Text = "Глибоке безпечне очищення";
        state.CleanupButton.Dock = DockStyle.Fill;
        state.CleanupButton.Margin = new Padding(12, 0, 0, 0);
        ConfigurePrimaryButton(state.CleanupButton);
        footer.Controls.Add(state.CleanupButton, 1, 0);
        return footer;
    }

    private static void UpdatePage(State state, StarCitizenMaintenanceSnapshot snapshot)
    {
        var detected = snapshot.Installations.Count;
        state.PathStatusLabel.Text = detected == 0
            ? "Star Citizen не знайдено автоматично — виберіть папку вручну."
            : detected == 1
                ? $"Знайдено Star Citizen • {snapshot.Installations[0].Channel}"
                : $"Знайдено каналів Star Citizen: {detected}";
        state.PathStatusLabel.ForeColor = detected > 0 ? Theme.Good : Theme.Warning;

        if (!string.IsNullOrWhiteSpace(snapshot.ManualPath))
        {
            state.PathValueLabel.Text = $"Вручну: {snapshot.ManualPath}";
            state.ResetPathButton.Enabled = true;
        }
        else if (detected > 0)
        {
            state.PathValueLabel.Text = $"Автопошук: {snapshot.Installations[0].Path}";
            state.ResetPathButton.Enabled = false;
        }
        else
        {
            state.PathValueLabel.Text = "Можна вибрати LIVE/PTU/EPTU/TECH-PREVIEW або кореневу папку StarCitizen.";
            state.ResetPathButton.Enabled = false;
        }

        state.LauncherButton.Enabled = !string.IsNullOrWhiteSpace(snapshot.RsiLauncherPath) && File.Exists(snapshot.RsiLauncherPath);

        foreach (var id in TargetOrder)
        {
            var row = state.TargetRows[id];
            var target = snapshot.Targets.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                row.Title.Text = id;
                row.Description.Text = "Не вдалося проаналізувати цю категорію.";
                row.Size.Text = "—";
                row.Check.Checked = false;
                row.Check.Enabled = false;
                continue;
            }

            row.Title.Text = target.Title;
            row.Description.Text = target.Description + (target.AffectsAllGames ? " • Для всіх ігор" : string.Empty);
            row.Size.Text = target.Available ? FormatBytes(target.EstimatedBytes) : "чисто";
            row.Size.ForeColor = target.Available ? Theme.Accent : Theme.Good;
            row.Check.Enabled = target.Available;
            row.Check.Checked = target.Available && target.SelectedByDefault;
        }

        UpdateSelectedSummary(state);
        state.StatusLabel.Text = detected == 0
            ? "Автопошук не знайшов гру. Натисніть «Вибрати папку гри» — шлях буде запам'ятовано."
            : "Аналіз завершено. Виберіть категорії — перед видаленням програма покаже точний підсумок.";
        state.StatusLabel.ForeColor = detected > 0 ? Theme.Muted : Theme.Warning;
    }

    private static void UpdateSelectedSummary(State state)
    {
        if (state.Snapshot is null)
        {
            state.SelectedSizeLabel.Text = "Очікуємо аналіз…";
            state.CleanupButton.Enabled = false;
            return;
        }

        var selected = state.TargetRows
            .Where(pair => pair.Value.Check.Checked && pair.Value.Check.Enabled)
            .Select(pair => state.Snapshot.Targets.FirstOrDefault(target => string.Equals(target.Id, pair.Key, StringComparison.OrdinalIgnoreCase)))
            .Where(target => target is not null)
            .Cast<StarCitizenCleanupTarget>()
            .ToArray();
        var bytes = selected.Sum(target => target.EstimatedBytes);
        state.SelectedSizeLabel.Text = selected.Length == 0
            ? "Нічого не вибрано"
            : $"Вибрано категорій: {selected.Length} • приблизно {FormatBytes(bytes)}";
        state.CleanupButton.Enabled = !state.Busy && selected.Length > 0;
    }

    private static void SetBusy(State state, bool busy, string? text = null)
    {
        state.Busy = busy;
        state.RefreshButton.Enabled = !busy;
        state.BrowseButton.Enabled = !busy;
        state.ResetPathButton.Enabled = !busy && !string.IsNullOrWhiteSpace(state.Snapshot?.ManualPath);
        state.LauncherButton.Enabled = !busy && !string.IsNullOrWhiteSpace(state.Snapshot?.RsiLauncherPath) && File.Exists(state.Snapshot.RsiLauncherPath);
        foreach (var row in state.TargetRows.Values)
        {
            if (busy) row.Check.Enabled = false;
        }
        if (!string.IsNullOrWhiteSpace(text))
        {
            state.StatusLabel.Text = text;
            state.StatusLabel.ForeColor = Theme.Muted;
        }
        UpdateSelectedSummary(state);
    }

    private static void OpenLauncher(State state)
    {
        var path = state.Snapshot?.RsiLauncherPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show("RSI Launcher не знайдено.", "RSI Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            MessageBox.Show(
                "Лаунчер відкрито. Для повної перевірки Data.p4k та інших файлів використовуйте Verify у RSI Launcher. TiHiY навмисно не імітує недокументований механізм Verify, щоб не пошкодити інсталяцію.",
                "Verify файлів гри",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося запустити RSI Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private static void SetNavActive(Button button, bool active)
    {
        button.BackColor = active ? Theme.AccentSoft : Theme.Sidebar;
        button.ForeColor = active ? Theme.Accent : Theme.Muted;
    }

    private static void ConfigurePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
        button.FlatAppearance.MouseDownBackColor = Theme.AccentPressed;
        button.BackColor = Theme.Accent;
        button.ForeColor = Theme.Window;
        button.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
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

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 Б";
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return index == 0 ? $"{value:0} {units[index]}" : $"{value:0.##} {units[index]}";
    }

    private sealed class State
    {
        public State(Panel pageHost) => PageHost = pageHost;
        public StarCitizenMaintenanceService Service { get; } = new();
        public Panel PageHost { get; }
        public Panel Page { get; set; } = null!;
        public Button NavButton { get; set; } = null!;
        public Button RefreshButton { get; } = new();
        public Button BrowseButton { get; } = new();
        public Button ResetPathButton { get; } = new();
        public Button LauncherButton { get; } = new();
        public Button CleanupButton { get; } = new();
        public Label StatusLabel { get; } = new();
        public Label PathStatusLabel { get; } = new();
        public Label PathValueLabel { get; } = new();
        public Label SelectedSizeLabel { get; } = new();
        public Dictionary<string, TargetRow> TargetRows { get; } = new(StringComparer.OrdinalIgnoreCase);
        public StarCitizenMaintenanceSnapshot? Snapshot { get; set; }
        public bool Busy { get; set; }
    }

    private sealed record TargetRow(
        TableLayoutPanel Root,
        CheckBox Check,
        Label Title,
        Label Description,
        Label Size);
}
