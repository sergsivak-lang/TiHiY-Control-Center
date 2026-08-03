using System.Diagnostics;
using System.Runtime.InteropServices;
using TiHiY.SystemOptimizer.Core;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.UI;

public sealed class MainForm : Form
{
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private readonly SystemScanner _scanner = new();
    private readonly RecommendationEngine _engine = new();
    private readonly BackupService _backup = new();
    private readonly OptimizationService _optimizer = new();

    private readonly FlowLayoutPanel _cards = new();
    private readonly Label _statusLabel = new();
    private readonly Label _scoreLabel = new();
    private readonly Label _recommendationCountLabel = new();
    private readonly Label _cpuValue = new();
    private readonly Label _gpuValue = new();
    private readonly Label _ramValue = new();
    private readonly Label _windowsValue = new();
    private readonly Button _applyButton = new();
    private readonly Button _scanButton = new();
    private readonly Button _maximizeButton = new();

    private IReadOnlyList<OptimizationItem> _items = Array.Empty<OptimizationItem>();
    private bool _isBusy;

    public MainForm()
    {
        Text = "TiHiY System Optimizer";
        Name = nameof(MainForm);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 700);
        ClientSize = new Size(1240, 800);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        KeyPreview = true;

        BuildUi();
        Shown += async (_, _) => await ScanAsync();
        SizeChanged += (_, _) =>
        {
            _maximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
            ResizeRecommendationCards();
        };
    }

    private void BuildUi()
    {
        SuspendLayout();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(CreateTitleBar(), 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
        Controls.Add(root);

        ResumeLayout(true);
    }

    private Control CreateTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            Margin = Padding.Empty,
            Padding = new Padding(14, 0, 0, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));

        var mark = new Label
        {
            Text = "T",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Theme.Accent,
            Margin = new Padding(0, 4, 0, 4)
        };

        var title = new Label
        {
            Text = "TiHiY System Optimizer",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Theme.Text,
            Padding = new Padding(8, 0, 0, 0)
        };

        var windowButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        var minimize = CreateTitleButton("—", () => WindowState = FormWindowState.Minimized);
        _maximizeButton.Text = "□";
        ConfigureTitleButton(_maximizeButton, ToggleMaximize);
        var close = CreateTitleButton("×", Close, isClose: true);

        windowButtons.Controls.Add(minimize, 0, 0);
        windowButtons.Controls.Add(_maximizeButton, 1, 0);
        windowButtons.Controls.Add(close, 2, 0);

        layout.Controls.Add(mark, 0, 0);
        layout.Controls.Add(title, 1, 0);
        layout.Controls.Add(windowButtons, 2, 0);
        titleBar.Controls.Add(layout);

        void BeginWindowDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
        }

        titleBar.MouseDown += BeginWindowDrag;
        layout.MouseDown += BeginWindowDrag;
        title.MouseDown += BeginWindowDrag;
        mark.MouseDown += BeginWindowDrag;
        titleBar.DoubleClick += (_, _) => ToggleMaximize();
        layout.DoubleClick += (_, _) => ToggleMaximize();
        title.DoubleClick += (_, _) => ToggleMaximize();

        return titleBar;
    }

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        body.Controls.Add(CreateSidebar(), 0, 0);
        body.Controls.Add(CreateDashboard(), 1, 0);
        return body;
    }

    private Control CreateSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Sidebar,
            Padding = new Padding(20, 22, 20, 20),
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Sidebar,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));

        layout.Controls.Add(CreateBrand(), 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "НАВІГАЦІЯ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            ForeColor = Theme.Subtle
        }, 0, 1);

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Theme.Sidebar,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty
        };

        var home = CreateNavButton("Головна", true, async () => await ScanAsync());
        var scan = CreateNavButton("Повторити аналіз", false, async () => await ScanAsync());
        var backups = CreateNavButton("Резервні копії", false, OpenBackupsFolder);
        navigation.Controls.Add(home);
        navigation.Controls.Add(scan);
        navigation.Controls.Add(backups);
        navigation.Resize += (_, _) =>
        {
            foreach (Control control in navigation.Controls)
            {
                control.Width = Math.Max(120, navigation.ClientSize.Width);
            }
        };
        layout.Controls.Add(navigation, 0, 2);

        var footer = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            BorderColor = Theme.Border,
            Radius = 14,
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(14, 10, 14, 10)
        };
        footer.Controls.Add(new Label
        {
            Text = "Безпечний режим\nЖодних змін без підтвердження",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.5F)
        });
        layout.Controls.Add(footer, 0, 3);

        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private Control CreateBrand()
    {
        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54F));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var logo = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.AccentSoft,
            BorderColor = Color.FromArgb(70, Theme.Accent),
            Radius = 14,
            Margin = new Padding(0, 6, 10, 18)
        };
        logo.Controls.Add(new Label
        {
            Text = "T",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
        });

        var text = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 12)
        };
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        text.Controls.Add(new Label
        {
            Text = "TiHiY",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
        }, 0, 0);
        text.Controls.Add(new Label
        {
            Text = "SYSTEM OPTIMIZER",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold)
        }, 0, 1);

        brand.Controls.Add(logo, 0, 0);
        brand.Controls.Add(text, 1, 0);
        return brand;
    }

    private Control CreateDashboard()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            Padding = new Padding(28, 22, 28, 24),
            Margin = Padding.Empty
        };

        var dashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 178F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        dashboard.Controls.Add(CreateHeader(), 0, 0);
        dashboard.Controls.Add(CreateSummaryCard(), 0, 1);
        dashboard.Controls.Add(CreateRecommendationsHeader(), 0, 2);

        _cards.Dock = DockStyle.Fill;
        _cards.AutoScroll = true;
        _cards.FlowDirection = FlowDirection.TopDown;
        _cards.WrapContents = false;
        _cards.BackColor = Theme.Window;
        _cards.Margin = Padding.Empty;
        _cards.Padding = new Padding(0, 0, 8, 0);
        _cards.Resize += (_, _) => ResizeRecommendationCards();
        dashboard.Controls.Add(_cards, 0, 3);

        host.Controls.Add(dashboard);
        return host;
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));

        header.Controls.Add(new Label
        {
            Text = "Стан вашого ПК",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
            ForeColor = Theme.Text
        }, 0, 0);

        _statusLabel.Text = "Підготовка до аналізу…";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.TopLeft;
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Font = new Font("Segoe UI", 10F);
        _statusLabel.AutoEllipsis = true;
        header.Controls.Add(_statusLabel, 0, 1);

        return header;
    }

    private Control CreateSummaryCard()
    {
        var summary = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
            BorderColor = Theme.Border,
            BorderThickness = 1F,
            Radius = 20,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(22, 20, 22, 20)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224F));

        layout.Controls.Add(CreateScoreBlock(), 0, 0);
        layout.Controls.Add(CreateSystemMetrics(), 1, 0);
        layout.Controls.Add(CreateSummaryActions(), 2, 0);
        summary.Controls.Add(layout);
        return summary;
    }

    private Control CreateScoreBlock()
    {
        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        block.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

        _scoreLabel.Text = "—";
        _scoreLabel.Dock = DockStyle.Fill;
        _scoreLabel.TextAlign = ContentAlignment.BottomLeft;
        _scoreLabel.Font = new Font("Segoe UI Semibold", 36F, FontStyle.Bold);
        _scoreLabel.ForeColor = Theme.Accent;

        block.Controls.Add(_scoreLabel, 0, 0);
        block.Controls.Add(new Label
        {
            Text = "ОЦІНКА СИСТЕМИ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold)
        }, 0, 1);
        block.Controls.Add(new Label
        {
            Text = "Безпечний аналіз",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Good,
            Font = new Font("Segoe UI", 8.5F)
        }, 0, 2);
        return block;
    }

    private Control CreateSystemMetrics()
    {
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(6, 0, 16, 0),
            Padding = Padding.Empty
        };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        metrics.Controls.Add(CreateMetric("ПРОЦЕСОР", _cpuValue), 0, 0);
        metrics.Controls.Add(CreateMetric("ВІДЕОКАРТА", _gpuValue), 1, 0);
        metrics.Controls.Add(CreateMetric("ПАМ’ЯТЬ", _ramValue), 0, 1);
        metrics.Controls.Add(CreateMetric("WINDOWS", _windowsValue), 1, 1);
        return metrics;
    }

    private static Control CreateMetric(string caption, Label valueLabel)
    {
        var cell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8, 4, 8, 4),
            Padding = Padding.Empty
        };
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        cell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        cell.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold)
        }, 0, 0);

        valueLabel.Text = "—";
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.TextAlign = ContentAlignment.TopLeft;
        valueLabel.ForeColor = Theme.Text;
        valueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        valueLabel.AutoEllipsis = true;
        cell.Controls.Add(valueLabel, 0, 1);
        return cell;
    }

    private Control CreateSummaryActions()
    {
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(12, 0, 0, 0),
            Padding = Padding.Empty
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _applyButton.Text = "Оптимізувати вибране";
        ConfigurePrimaryButton(_applyButton);
        _applyButton.Dock = DockStyle.Fill;
        _applyButton.Enabled = false;
        _applyButton.Click += async (_, _) => await ApplyAsync();

        _scanButton.Text = "Повторити аналіз";
        ConfigureSecondaryButton(_scanButton);
        _scanButton.Dock = DockStyle.Fill;
        _scanButton.Click += async (_, _) => await ScanAsync();

        actions.Controls.Add(_applyButton, 0, 0);
        actions.Controls.Add(_scanButton, 0, 2);
        return actions;
    }

    private Control CreateRecommendationsHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 6)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));

        header.Controls.Add(new Label
        {
            Text = "Рекомендації",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            ForeColor = Theme.Text
        }, 0, 0);

        _recommendationCountLabel.Text = "Очікуємо аналіз";
        _recommendationCountLabel.Dock = DockStyle.Fill;
        _recommendationCountLabel.TextAlign = ContentAlignment.MiddleRight;
        _recommendationCountLabel.Font = new Font("Segoe UI", 9F);
        _recommendationCountLabel.ForeColor = Theme.Muted;
        header.Controls.Add(_recommendationCountLabel, 1, 0);
        return header;
    }

    private async Task ScanAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true, "Аналізуємо Windows та обладнання…");
        _cards.SuspendLayout();
        _cards.Controls.Clear();

        try
        {
            var snapshot = await _scanner.ScanAsync();
            _items = _engine.Analyze(snapshot);

            var good = _items.Count(item => item.Level == RecommendationLevel.Good);
            var score = 78 + (int)Math.Round(22D * good / Math.Max(1, _items.Count));
            var attention = _items.Count(item => item.Level != RecommendationLevel.Good);

            _scoreLabel.Text = $"{score}/100";
            _cpuValue.Text = NormalizeMetric(snapshot.Cpu);
            _gpuValue.Text = NormalizeMetric(snapshot.Gpu);
            _ramValue.Text = NormalizeMetric(snapshot.Ram);
            _windowsValue.Text = NormalizeMetric(snapshot.Windows);
            _statusLabel.Text = attention == 0
                ? "Система перевірена. Критичних рекомендацій немає."
                : $"Система перевірена. Знайдено рекомендацій: {attention}.";
            _recommendationCountLabel.Text = attention == 0
                ? "Усе налаштовано"
                : $"{attention} можна застосувати";

            foreach (var item in _items)
            {
                _cards.Controls.Add(CreateRecommendationCard(item));
            }
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Не вдалося завершити аналіз.";
            MessageBox.Show(
                exception.Message,
                "Помилка аналізу",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _cards.ResumeLayout(true);
            BeginInvoke(ResizeRecommendationCards);
            SetBusy(false);
        }
    }

    private Control CreateRecommendationCard(OptimizationItem item)
    {
        var card = new RoundedPanel
        {
            Width = GetRecommendationCardWidth(),
            Height = 118,
            BackColor = Theme.Card,
            BorderColor = Theme.Border,
            BorderThickness = 1F,
            Radius = 16,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(16, 14, 16, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));

        var check = new CheckBox
        {
            Checked = item.Selected,
            AutoSize = false,
            Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Card
        };
        check.CheckedChanged += (_, _) =>
        {
            item.Selected = check.Checked;
            UpdateApplyButtonState();
        };

        var text = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 12, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

        text.Controls.Add(new Label
        {
            Text = item.Title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
        }, 0, 0);

        text.Controls.Add(new Label
        {
            Text = item.Summary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 9F)
        }, 0, 1);

        text.Controls.Add(new Label
        {
            Text = item.Level == RecommendationLevel.Good
                ? $"Готово: {item.CurrentValue}"
                : $"{item.CurrentValue}  →  {item.RecommendedValue}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = item.Level == RecommendationLevel.Good ? Theme.Good : Theme.Warning,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        }, 0, 2);

        var details = new Button
        {
            Text = "Докладніше",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 20, 0, 20)
        };
        ConfigureSecondaryButton(details);
        details.Click += (_, _) => MessageBox.Show(
            item.Details,
            item.Title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        layout.Controls.Add(check, 0, 0);
        layout.Controls.Add(text, 1, 0);
        layout.Controls.Add(details, 2, 0);
        card.Controls.Add(layout);
        return card;
    }

    private async Task ApplyAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var selected = _items.Where(item => item.Selected).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Буде застосовано змін: {selected.Length}.\n\nПеред початком програма створить резервну копію. Продовжити?",
            "Підтвердження оптимізації",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "Створюємо резервну копію…");

        try
        {
            var folder = _backup.CreateBackup(selected.Select(item => item.Id));
            var log = await _optimizer.ApplyAsync(
                selected,
                new Progress<string>(message => _statusLabel.Text = $"Застосовуємо: {message}…"));

            await File.WriteAllLinesAsync(Path.Combine(folder, "apply.log"), log);
            var failed = log.Count(line => line.StartsWith("ПОМИЛКА", StringComparison.OrdinalIgnoreCase));
            var restartText = selected.Any(item => item.RequiresRestart)
                ? "\n\nДля завершення змін перезавантажте ПК."
                : string.Empty;

            MessageBox.Show(
                failed == 0
                    ? $"Оптимізацію завершено.\n\nРезервна копія:\n{folder}{restartText}"
                    : $"Оптимізацію завершено з попередженнями.\n\nПеревірте журнал:\n{Path.Combine(folder, "apply.log")}{restartText}",
                "TiHiY System Optimizer",
                MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            SetBusy(false);
            await ScanAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Помилка оптимізації",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _isBusy = busy;
        _scanButton.Enabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _statusLabel.Text = message;
        }

        UpdateApplyButtonState();
    }

    private void UpdateApplyButtonState()
    {
        _applyButton.Enabled = !_isBusy && _items.Any(item => item.Selected);
        _applyButton.BackColor = _applyButton.Enabled ? Theme.Accent : Theme.CardHover;
        _applyButton.ForeColor = _applyButton.Enabled ? Theme.Window : Theme.Subtle;
    }

    private void ResizeRecommendationCards()
    {
        if (_cards.IsDisposed || _cards.ClientSize.Width <= 0)
        {
            return;
        }

        var width = GetRecommendationCardWidth();
        foreach (Control control in _cards.Controls)
        {
            control.Width = width;
        }
    }

    private int GetRecommendationCardWidth()
    {
        var scrollbarAllowance = _cards.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth + 8
            : 8;
        return Math.Max(520, _cards.ClientSize.Width - scrollbarAllowance);
    }

    private static string NormalizeMetric(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Невідомо" : value.Trim();
    }

    private static Button CreateNavButton(string text, bool active, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 192,
            Height = 46,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 8, 0),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = active ? Theme.AccentSoft : Theme.Sidebar,
            ForeColor = active ? Theme.Accent : Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = Theme.Surface;
        button.Click += (_, _) => action();
        return button;
    }

    private static Button CreateNavButton(string text, bool active, Func<Task> action)
    {
        return CreateNavButton(text, active, () => _ = action());
    }

    private static void ConfigurePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
        button.FlatAppearance.MouseDownBackColor = Theme.AccentPressed;
        button.BackColor = Theme.Accent;
        button.ForeColor = Theme.Window;
        button.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
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
        button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
    }

    private static Button CreateTitleButton(string text, Action action, bool isClose = false)
    {
        var button = new Button { Text = text };
        ConfigureTitleButton(button, action, isClose);
        return button;
    }

    private static void ConfigureTitleButton(Button button, Action action, bool isClose = false)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = Padding.Empty;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isClose ? Color.FromArgb(196, 43, 58) : Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = isClose ? Color.FromArgb(158, 31, 44) : Theme.Surface;
        button.BackColor = Theme.Window;
        button.ForeColor = Theme.Muted;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        button.TabStop = false;
        button.Click += (_, _) => action();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void OpenBackupsFolder()
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "TiHiY",
                "SystemOptimizer",
                "Backups");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Не вдалося відкрити резервні копії",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            if ((int)message.Result != 0)
            {
                return;
            }

            var raw = message.LParam.ToInt64();
            var screenPoint = new Point(unchecked((short)(raw & 0xFFFF)), unchecked((short)((raw >> 16) & 0xFFFF)));
            var point = PointToClient(screenPoint);
            const int grip = 8;

            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;

            message.Result = (IntPtr)(
                top && left ? HtTopLeft :
                top && right ? HtTopRight :
                bottom && left ? HtBottomLeft :
                bottom && right ? HtBottomRight :
                left ? HtLeft :
                right ? HtRight :
                top ? HtTop :
                bottom ? HtBottom :
                0);
            return;
        }

        base.WndProc(ref message);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
