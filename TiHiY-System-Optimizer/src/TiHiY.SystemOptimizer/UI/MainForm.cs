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
    private const int RecommendationRowHeight = 122;

    private readonly SystemScanner _scanner = new();
    private readonly RecommendationEngine _engine = new();
    private readonly BackupService _backup = new();
    private readonly OptimizationService _optimizer = new();

    private readonly Panel _recommendationsViewport = new();
    private readonly TableLayoutPanel _cards = new();
    private readonly Panel _scrollTrack = new();
    private readonly Panel _scrollThumb = new();
    private readonly Label _statusLabel = new();
    private readonly Label _scoreLabel = new();
    private readonly Label _scoreCaption = new();
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
    private int _recommendationScrollOffset;

    public MainForm()
    {
        Text = "TiHiY System Optimizer";
        Name = nameof(MainForm);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 700);
        ClientSize = new Size(1280, 800);
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
            UpdateRecommendationViewport();
        };
    }

    private void BuildUi()
    {
        SuspendLayout();
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Controls.Add(CreateTitleBar(), 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
        Controls.Add(root);
        ResumeLayout(true);
    }

    private Control CreateTitleBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };

        var windowButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 138,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        windowButtons.Controls.Add(CreateWindowButton("—", () => WindowState = FormWindowState.Minimized), 0, 0);
        ConfigureWindowButton(_maximizeButton, "□", ToggleMaximize);
        windowButtons.Controls.Add(_maximizeButton, 1, 0);
        windowButtons.Controls.Add(CreateWindowButton("×", Close, true), 2, 0);
        bar.Controls.Add(windowButtons);

        bar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var accentBrush = new SolidBrush(Theme.Accent);
            using var textBrush = new SolidBrush(Theme.Muted);
            using var titleFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            e.Graphics.FillEllipse(accentBrush, 17, 16, 12, 12);
            e.Graphics.DrawString("TiHiY System Optimizer", titleFont, textBrush, 42, 12);
        };

        bar.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
        };
        bar.DoubleClick += (_, _) => ToggleMaximize();
        return bar;
    }

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216F));
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
            Margin = Padding.Empty,
            Padding = new Padding(18),
            BackColor = Theme.Sidebar
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Sidebar
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        grid.Controls.Add(CreateBrand(), 0, 0);
        grid.Controls.Add(new Label
        {
            Text = "НАВІГАЦІЯ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            BackColor = Theme.Sidebar
        }, 0, 1);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Sidebar
        };
        nav.Controls.Add(CreateNavButton("Огляд системи", true, () => { _ = ScanAsync(); }));
        nav.Controls.Add(CreateNavButton("Повторити аналіз", false, () => { _ = ScanAsync(); }));
        nav.Controls.Add(CreateNavButton("Резервні копії", false, OpenBackupsFolder));
        nav.Resize += (_, _) =>
        {
            var width = Math.Max(120, nav.ClientSize.Width);
            foreach (Control control in nav.Controls)
            {
                control.Width = width;
            }
        };
        grid.Controls.Add(nav, 0, 2);

        var safe = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0),
            Padding = new Padding(14, 10, 14, 10),
            Radius = 14,
            BackColor = Theme.Surface,
            BorderColor = Theme.Border
        };
        safe.Controls.Add(new Label
        {
            Text = "БЕЗПЕЧНИЙ РЕЖИМ\nЗміни — лише після вашого підтвердження",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8F),
            BackColor = Theme.Surface
        });
        grid.Controls.Add(safe, 0, 3);
        sidebar.Controls.Add(grid);
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
            Padding = Padding.Empty,
            BackColor = Theme.Sidebar
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var logo = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 10, 17),
            Radius = 14,
            BackColor = Theme.AccentSoft,
            BorderColor = Color.FromArgb(70, Theme.Accent)
        };
        logo.Controls.Add(new Label
        {
            Text = "T",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            BackColor = Theme.AccentSoft
        });

        var text = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 5, 0, 10),
            BackColor = Theme.Sidebar
        };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 39F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        text.Controls.Add(new Label
        {
            Text = "TiHiY",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            BackColor = Theme.Sidebar
        }, 0, 0);
        text.Controls.Add(new Label
        {
            Text = "SYSTEM OPTIMIZER",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Accent,
            Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold),
            BackColor = Theme.Sidebar
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
            Margin = Padding.Empty,
            Padding = new Padding(26, 20, 26, 22),
            BackColor = Theme.Window
        };
        var dashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 184F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        dashboard.Controls.Add(CreateHeader(), 0, 0);
        dashboard.Controls.Add(CreateSummaryCard(), 0, 1);
        dashboard.Controls.Add(CreateRecommendationsHeader(), 0, 2);

        _recommendationsViewport.Dock = DockStyle.Fill;
        _recommendationsViewport.Margin = Padding.Empty;
        _recommendationsViewport.Padding = Padding.Empty;
        _recommendationsViewport.AutoScroll = false;
        _recommendationsViewport.BackColor = Theme.Window;
        _recommendationsViewport.Resize += (_, _) => UpdateRecommendationViewport();
        _recommendationsViewport.MouseWheel += RecommendationMouseWheel;

        _cards.AutoSize = false;
        _cards.ColumnCount = 1;
        _cards.RowCount = 0;
        _cards.Margin = Padding.Empty;
        _cards.Padding = Padding.Empty;
        _cards.BackColor = Theme.Window;
        _cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _cards.Location = Point.Empty;
        _cards.MouseWheel += RecommendationMouseWheel;

        _scrollTrack.Dock = DockStyle.Right;
        _scrollTrack.Width = 7;
        _scrollTrack.Margin = Padding.Empty;
        _scrollTrack.Padding = Padding.Empty;
        _scrollTrack.BackColor = Theme.Surface;
        _scrollTrack.Visible = false;
        _scrollThumb.Width = 7;
        _scrollThumb.Height = 42;
        _scrollThumb.Left = 0;
        _scrollThumb.Top = 0;
        _scrollThumb.BackColor = Theme.Subtle;
        _scrollTrack.Controls.Add(_scrollThumb);

        _recommendationsViewport.Controls.Add(_cards);
        _recommendationsViewport.Controls.Add(_scrollTrack);
        dashboard.Controls.Add(_recommendationsViewport, 0, 3);
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
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        header.Controls.Add(new Label
        {
            Text = "Стан вашого ПК",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            BackColor = Theme.Window
        }, 0, 0);
        _statusLabel.Text = "Підготовка до аналізу…";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.TopLeft;
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Font = new Font("Segoe UI", 9.5F);
        _statusLabel.AutoEllipsis = true;
        _statusLabel.BackColor = Theme.Window;
        header.Controls.Add(_statusLabel, 0, 1);
        return header;
    }

    private Control CreateSummaryCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(20, 18, 20, 18),
            Radius = 20,
            BackColor = Theme.Card,
            BorderColor = Theme.Border
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218F));
        grid.Controls.Add(CreateScoreBlock(), 0, 0);
        grid.Controls.Add(CreateMetricsBlock(), 1, 0);
        grid.Controls.Add(CreateActionsBlock(), 2, 0);
        card.Controls.Add(grid);
        return card;
    }

    private Control CreateScoreBlock()
    {
        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(4, 6, 14, 6),
            BackColor = Theme.Card
        };
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        block.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        block.Controls.Add(new Label
        {
            Text = "СТАН СИСТЕМИ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        _scoreLabel.Text = "—";
        _scoreLabel.Dock = DockStyle.Fill;
        _scoreLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scoreLabel.ForeColor = Theme.Accent;
        _scoreLabel.Font = new Font("Segoe UI Semibold", 27F, FontStyle.Bold);
        _scoreLabel.BackColor = Theme.Card;
        block.Controls.Add(_scoreLabel, 0, 1);
        _scoreCaption.Text = "Очікуємо перевірку";
        _scoreCaption.Dock = DockStyle.Fill;
        _scoreCaption.TextAlign = ContentAlignment.TopLeft;
        _scoreCaption.ForeColor = Theme.Muted;
        _scoreCaption.Font = new Font("Segoe UI", 8.5F);
        _scoreCaption.BackColor = Theme.Card;
        block.Controls.Add(_scoreCaption, 0, 2);
        return block;
    }

    private Control CreateMetricsBlock()
    {
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(8, 0, 14, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
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

    private static Control CreateMetric(string caption, Label value)
    {
        var cell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8, 5, 8, 5),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
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
        value.Text = "—";
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.TopLeft;
        value.AutoEllipsis = true;
        value.ForeColor = Theme.Text;
        value.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        value.BackColor = Theme.Card;
        cell.Controls.Add(value, 0, 1);
        return cell;
    }

    private Control CreateActionsBlock()
    {
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(12, 3, 0, 3),
            Padding = Padding.Empty,
            BackColor = Theme.Card
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
            Padding = new Padding(0, 6, 0, 5),
            BackColor = Theme.Window
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        header.Controls.Add(new Label
        {
            Text = "Рекомендації",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            BackColor = Theme.Window
        }, 0, 0);
        _recommendationCountLabel.Text = "Очікуємо аналіз";
        _recommendationCountLabel.Dock = DockStyle.Fill;
        _recommendationCountLabel.TextAlign = ContentAlignment.MiddleRight;
        _recommendationCountLabel.ForeColor = Theme.Muted;
        _recommendationCountLabel.Font = new Font("Segoe UI", 8.8F);
        _recommendationCountLabel.BackColor = Theme.Window;
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
        _recommendationScrollOffset = 0;
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _cards.RowStyles.Clear();
        _cards.RowCount = 0;

        try
        {
            var snapshot = await _scanner.ScanAsync();
            _items = _engine.Analyze(snapshot);
            var goodCount = _items.Count(item => item.Level == RecommendationLevel.Good);
            var attentionCount = _items.Count - goodCount;
            var score = 78 + (int)Math.Round(22D * goodCount / Math.Max(1, _items.Count));
            _scoreLabel.Text = $"{score}/100";
            _scoreCaption.Text = score >= 96 ? "Відмінний стан" : score >= 90 ? "Добрий стан" : "Є що покращити";
            _cpuValue.Text = NormalizeMetric(snapshot.Cpu);
            _gpuValue.Text = NormalizeMetric(snapshot.Gpu);
            _ramValue.Text = NormalizeMetric(snapshot.Ram);
            _windowsValue.Text = NormalizeMetric(snapshot.Windows);
            _statusLabel.Text = attentionCount == 0
                ? "Перевірку завершено. Додаткові дії не потрібні."
                : $"Перевірку завершено. Знайдено {attentionCount} безпечних рекомендацій.";
            _recommendationCountLabel.Text = attentionCount == 0 ? "Усе налаштовано" : $"До застосування: {attentionCount}";

            var row = 0;
            foreach (var item in _items)
            {
                _cards.RowCount++;
                _cards.RowStyles.Add(new RowStyle(SizeType.Absolute, RecommendationRowHeight));
                var card = CreateRecommendationCard(item);
                AttachRecommendationWheel(card);
                _cards.Controls.Add(card, 0, row++);
            }
            _cards.Height = _cards.RowCount * RecommendationRowHeight;
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Не вдалося завершити аналіз.";
            if (!IsSnapshotMode())
            {
                MessageBox.Show(exception.Message, "Помилка аналізу", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _cards.ResumeLayout(true);
            UpdateRecommendationViewport();
            SetBusy(false);
        }
    }

    private Control CreateRecommendationCard(OptimizationItem item)
    {
        var good = item.Level == RecommendationLevel.Good;
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 12, 14, 12),
            Radius = 16,
            BackColor = Theme.Card,
            BorderColor = Theme.Border
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158F));

        var check = new CheckBox
        {
            Checked = item.Selected,
            Enabled = !good,
            AutoSize = false,
            Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleCenter,
            Cursor = good ? Cursors.Default : Cursors.Hand,
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
            RowCount = 2,
            Margin = new Padding(0, 0, 12, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        text.Controls.Add(new Label
        {
            Text = item.Title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 10.8F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        text.Controls.Add(new Label
        {
            Text = item.Summary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.8F),
            BackColor = Theme.Card
        }, 0, 1);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        var pill = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 2),
            Padding = Padding.Empty,
            Radius = 10,
            BackColor = good ? Color.FromArgb(25, 68, 53) : Color.FromArgb(74, 58, 30),
            BorderColor = Color.Transparent
        };
        pill.Controls.Add(new Label
        {
            Text = good ? "Все добре" : "Рекомендовано",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = good ? Theme.Good : Theme.Warning,
            Font = new Font("Segoe UI Semibold", 7.6F, FontStyle.Bold),
            BackColor = pill.BackColor
        });
        var details = new Button
        {
            Text = "Докладніше",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0)
        };
        ConfigureSecondaryButton(details);
        details.Click += (_, _) => MessageBox.Show(
            $"{item.Details}\n\nПоточний стан: {item.CurrentValue}\nРекомендовано: {item.RecommendedValue}",
            item.Title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        right.Controls.Add(pill, 0, 0);
        right.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card }, 0, 1);
        right.Controls.Add(details, 0, 2);
        grid.Controls.Add(check, 0, 0);
        grid.Controls.Add(text, 1, 0);
        grid.Controls.Add(right, 2, 0);
        card.Controls.Add(grid);
        return card;
    }

    private void AttachRecommendationWheel(Control control)
    {
        control.MouseWheel += RecommendationMouseWheel;
        foreach (Control child in control.Controls)
        {
            AttachRecommendationWheel(child);
        }
    }

    private void RecommendationMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_scrollTrack.Visible)
        {
            return;
        }
        var direction = e.Delta > 0 ? -1 : 1;
        SetRecommendationScroll(_recommendationScrollOffset + direction * 76);
    }

    private void SetRecommendationScroll(int offset)
    {
        var max = Math.Max(0, _cards.Height - _recommendationsViewport.ClientSize.Height);
        _recommendationScrollOffset = Math.Clamp(offset, 0, max);
        _cards.Top = -_recommendationScrollOffset;
        UpdateScrollThumb(max);
    }

    private void UpdateRecommendationViewport()
    {
        if (_recommendationsViewport.IsDisposed || _cards.IsDisposed)
        {
            return;
        }
        var viewWidth = Math.Max(300, _recommendationsViewport.ClientSize.Width);
        _cards.Width = Math.Max(280, viewWidth - 15);
        var max = Math.Max(0, _cards.Height - _recommendationsViewport.ClientSize.Height);
        _recommendationScrollOffset = Math.Clamp(_recommendationScrollOffset, 0, max);
        _cards.Left = 0;
        _cards.Top = -_recommendationScrollOffset;
        _scrollTrack.Visible = max > 0;
        _scrollTrack.BringToFront();
        UpdateScrollThumb(max);
    }

    private void UpdateScrollThumb(int max)
    {
        if (!_scrollTrack.Visible || _scrollTrack.Height <= 0 || _cards.Height <= 0)
        {
            return;
        }
        var viewportHeight = Math.Max(1, _recommendationsViewport.ClientSize.Height);
        var thumbHeight = Math.Max(34, (int)Math.Round(_scrollTrack.Height * Math.Min(1D, viewportHeight / (double)_cards.Height)));
        thumbHeight = Math.Min(_scrollTrack.Height, thumbHeight);
        _scrollThumb.Height = thumbHeight;
        var travel = Math.Max(0, _scrollTrack.Height - thumbHeight);
        _scrollThumb.Top = max == 0 ? 0 : (int)Math.Round(travel * (_recommendationScrollOffset / (double)max));
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
            await ScanAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Помилка оптимізації", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        var enabled = !_isBusy && _items.Any(item => item.Selected);
        _applyButton.Enabled = enabled;
        _applyButton.BackColor = enabled ? Theme.Accent : Theme.CardHover;
        _applyButton.ForeColor = enabled ? Theme.Window : Theme.Subtle;
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
            Width = 180,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = active ? Theme.AccentSoft : Theme.Sidebar,
            ForeColor = active ? Theme.Accent : Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = Theme.Surface;
        button.Click += (_, _) => action();
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
        button.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
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
        button.Font = new Font("Segoe UI Semibold", 8.7F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
    }

    private static Button CreateWindowButton(string text, Action action, bool close = false)
    {
        var button = new Button();
        ConfigureWindowButton(button, text, action, close);
        return button;
    }

    private static void ConfigureWindowButton(Button button, string text, Action action, bool close = false)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = Padding.Empty;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = close ? Color.FromArgb(196, 43, 58) : Theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = close ? Color.FromArgb(158, 31, 44) : Theme.Surface;
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
            MessageBox.Show(exception.Message, "Не вдалося відкрити резервні копії", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsSnapshotMode()
    {
        return string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            var raw = message.LParam.ToInt64();
            var screenPoint = new Point(
                unchecked((short)(raw & 0xFFFF)),
                unchecked((short)((raw >> 16) & 0xFFFF)));
            var point = PointToClient(screenPoint);
            const int grip = 8;
            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;
            if (top && left) message.Result = (IntPtr)HtTopLeft;
            else if (top && right) message.Result = (IntPtr)HtTopRight;
            else if (bottom && left) message.Result = (IntPtr)HtBottomLeft;
            else if (bottom && right) message.Result = (IntPtr)HtBottomRight;
            else if (left) message.Result = (IntPtr)HtLeft;
            else if (right) message.Result = (IntPtr)HtRight;
            else if (top) message.Result = (IntPtr)HtTop;
            else if (bottom) message.Result = (IntPtr)HtBottom;
            return;
        }
        base.WndProc(ref message);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
