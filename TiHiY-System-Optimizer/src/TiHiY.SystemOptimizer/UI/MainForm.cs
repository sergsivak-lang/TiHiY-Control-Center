using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    private const int RecommendationRowHeight = 156;

    private readonly SystemScanner _scanner = new();
    private readonly RecommendationEngine _engine = new();
    private readonly BackupService _backup = new();
    private readonly OptimizationService _optimizer = new();
    private readonly RestoreService _restore = new();

    private readonly Panel _pageHost = new();
    private readonly Dictionary<PageKind, Control> _pages = new();
    private readonly Dictionary<PageKind, Button> _navButtons = new();

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

    private readonly SoftwareCardView _rsiLauncherCard = new();
    private readonly SoftwareCardView _starCitizenCard = new();
    private readonly SoftwareCardView _obsCard = new();
    private readonly SoftwareCardView _discordCard = new();
    private readonly SoftwareCardView _steelSeriesCard = new();
    private readonly SoftwareCardView _safetyCard = new();

    private readonly Label _scSummaryLabel = new();
    private readonly Label _scCacheLabel = new();
    private readonly TableLayoutPanel _scChannelsPanel = new();
    private readonly Button _scOpenFolderButton = new();
    private readonly Button _scClearCacheButton = new();

    private readonly Label _restoreStatusLabel = new();
    private readonly Label _restoreItemsLabel = new();
    private readonly Button _restoreButton = new();
    private readonly Button _openBackupsButton = new();

    private IReadOnlyList<OptimizationItem> _items = Array.Empty<OptimizationItem>();
    private SystemSnapshot? _snapshot;
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
            if (e.Button != MouseButtons.Left) return;
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
        _pageHost.Dock = DockStyle.Fill;
        _pageHost.Margin = Padding.Empty;
        _pageHost.Padding = Padding.Empty;
        _pageHost.BackColor = Theme.Window;
        RegisterPage(PageKind.Overview, CreateOverviewPage());
        RegisterPage(PageKind.Applications, CreateApplicationsPage());
        RegisterPage(PageKind.StarCitizen, CreateStarCitizenPage());
        RegisterPage(PageKind.Restore, CreateRestorePage());
        body.Controls.Add(_pageHost, 1, 0);
        ShowPage(PageKind.Overview);
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
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
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
        AddNavButton(nav, PageKind.Overview, "Огляд системи");
        AddNavButton(nav, PageKind.Applications, "Програми та ігри");
        AddNavButton(nav, PageKind.StarCitizen, "Star Citizen");
        AddNavButton(nav, PageKind.Restore, "Скасувати зміни");
        nav.Resize += (_, _) =>
        {
            var width = Math.Max(120, nav.ClientSize.Width);
            foreach (Control control in nav.Controls) control.Width = width;
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
            Text = "ЯК ЦЕ ПРАЦЮЄ\n1. Аналізуємо ПК\n2. Пояснюємо кожну зміну\n3. Застосовуємо лише після підтвердження",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 7.7F),
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

    private Control CreateOverviewPage()
    {
        var host = CreatePageHost();
        var dashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 184F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        dashboard.Controls.Add(CreateOverviewHeader(), 0, 0);
        dashboard.Controls.Add(CreateSummaryCard(), 0, 1);
        dashboard.Controls.Add(CreateRecommendationsHeader(), 0, 2);
        dashboard.Controls.Add(CreateRecommendationsViewport(), 0, 3);
        host.Controls.Add(dashboard);
        return host;
    }

    private Control CreateOverviewHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(CreatePageTitle("Стан вашого ПК"), 0, 0);
        header.Controls.Add(CreatePageDescription("TiHiY перевіряє Windows і пояснює простими словами, що саме можна змінити та навіщо."), 0, 1);
        _statusLabel.Text = "Підготовка до аналізу…";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.TopLeft;
        _statusLabel.ForeColor = Theme.Subtle;
        _statusLabel.Font = new Font("Segoe UI", 8.6F);
        _statusLabel.AutoEllipsis = true;
        _statusLabel.BackColor = Theme.Window;
        header.Controls.Add(_statusLabel, 0, 2);
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
        block.Controls.Add(CreateCaption("СТАН СИСТЕМИ", Theme.Card), 0, 0);
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
        cell.Controls.Add(CreateCaption(caption, Theme.Card), 0, 0);
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
        _applyButton.Text = "Застосувати вибране";
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
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 5, 0, 5),
            BackColor = Theme.Window
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(new Label
        {
            Text = "Що можна покращити",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            BackColor = Theme.Window
        }, 0, 0);
        _recommendationCountLabel.Text = "Очікуємо аналіз";
        _recommendationCountLabel.Dock = DockStyle.Fill;
        _recommendationCountLabel.TextAlign = ContentAlignment.BottomRight;
        _recommendationCountLabel.ForeColor = Theme.Muted;
        _recommendationCountLabel.Font = new Font("Segoe UI", 8.8F);
        _recommendationCountLabel.BackColor = Theme.Window;
        header.Controls.Add(_recommendationCountLabel, 1, 0);
        var hint = new Label
        {
            Text = "Галочка означає, що ця зміна буде застосована після вашого підтвердження.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Subtle,
            Font = new Font("Segoe UI", 8F),
            AutoEllipsis = true,
            BackColor = Theme.Window
        };
        header.Controls.Add(hint, 0, 1);
        header.SetColumnSpan(hint, 2);
        return header;
    }

    private Control CreateRecommendationsViewport()
    {
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
        return _recommendationsViewport;
    }

    private Control CreateApplicationsPage()
    {
        var host = CreatePageHost();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateSimpleHeader("Програми та ігри", "Програма лише показує, що знайдено. OBS, Discord і SteelSeries тут не змінюються автоматично."), 0, 0);
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
        grid.Controls.Add(CreateSoftwareCard(_rsiLauncherCard, "RSI Launcher", "Запуск і оновлення Star Citizen."), 0, 0);
        grid.Controls.Add(CreateSoftwareCard(_starCitizenCard, "Star Citizen", "Пошук LIVE, PTU, EPTU та TECH-PREVIEW."), 1, 0);
        grid.Controls.Add(CreateSoftwareCard(_obsCard, "OBS Studio", "Перевіряємо наявність. Налаштування стріму не переписуємо."), 0, 1);
        grid.Controls.Add(CreateSoftwareCard(_discordCard, "Discord", "Перевіряємо наявність. Голос і канали не змінюємо."), 1, 1);
        grid.Controls.Add(CreateSoftwareCard(_steelSeriesCard, "SteelSeries GG / Sonar", "Перевіряємо наявність. Поточну маршрутизацію звуку не чіпаємо."), 0, 2);
        grid.Controls.Add(CreateSoftwareCard(_safetyCard, "Безпечний режим", "Кожна зміна показується до застосування і має зрозуміле пояснення."), 1, 2);
        layout.Controls.Add(grid, 0, 1);
        host.Controls.Add(layout);
        return host;
    }

    private Control CreateSoftwareCard(SoftwareCardView view, string title, string description)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 12),
            Padding = new Padding(16, 13, 16, 13),
            Radius = 16,
            BackColor = Theme.Card,
            BorderColor = Theme.Border
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 37F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        grid.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        grid.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.4F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        }, 0, 1);
        view.StatusLabel.Dock = DockStyle.Fill;
        view.StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        view.StatusLabel.ForeColor = Theme.Subtle;
        view.StatusLabel.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        view.StatusLabel.BackColor = Theme.Card;
        grid.Controls.Add(view.StatusLabel, 0, 2);
        view.PathLabel.Dock = DockStyle.Fill;
        view.PathLabel.TextAlign = ContentAlignment.TopLeft;
        view.PathLabel.ForeColor = Theme.Subtle;
        view.PathLabel.Font = new Font("Segoe UI", 7.8F);
        view.PathLabel.AutoEllipsis = true;
        view.PathLabel.BackColor = Theme.Card;
        grid.Controls.Add(view.PathLabel, 0, 3);
        view.OpenButton.Text = "Відкрити папку";
        view.OpenButton.Dock = DockStyle.Left;
        view.OpenButton.Width = 148;
        view.OpenButton.Margin = Padding.Empty;
        ConfigureSecondaryButton(view.OpenButton);
        view.OpenButton.Click += (_, _) => OpenDetectedPath(view.CurrentPath);
        grid.Controls.Add(view.OpenButton, 0, 4);
        card.Controls.Add(grid);
        return card;
    }

    private Control CreateStarCitizenPage()
    {
        var host = CreatePageHost();
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 146F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateSimpleHeader("Star Citizen", "Перевіряємо встановлені канали, USER.cfg і кеш шейдерів. Файли гри та керування не змінюємо мовчки."), 0, 0);
        layout.Controls.Add(CreateStarCitizenSummaryCard(), 0, 1);
        _scChannelsPanel.Dock = DockStyle.Fill;
        _scChannelsPanel.ColumnCount = 1;
        _scChannelsPanel.RowCount = 4;
        _scChannelsPanel.Margin = new Padding(0, 12, 0, 0);
        _scChannelsPanel.Padding = Padding.Empty;
        _scChannelsPanel.BackColor = Theme.Window;
        for (var index = 0; index < 4; index++) _scChannelsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        layout.Controls.Add(_scChannelsPanel, 0, 2);
        host.Controls.Add(layout);
        return host;
    }

    private Control CreateStarCitizenSummaryCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(18, 14, 18, 14),
            Radius = 18,
            BackColor = Theme.Card,
            BorderColor = Theme.Border
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216F));
        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        info.Controls.Add(CreateCaption("СТАН ГРИ", Theme.Card), 0, 0);
        _scSummaryLabel.Dock = DockStyle.Fill;
        _scSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scSummaryLabel.ForeColor = Theme.Text;
        _scSummaryLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _scSummaryLabel.AutoEllipsis = true;
        _scSummaryLabel.BackColor = Theme.Card;
        info.Controls.Add(_scSummaryLabel, 0, 1);
        _scCacheLabel.Dock = DockStyle.Fill;
        _scCacheLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scCacheLabel.ForeColor = Theme.Muted;
        _scCacheLabel.Font = new Font("Segoe UI", 8.4F);
        _scCacheLabel.AutoEllipsis = true;
        _scCacheLabel.BackColor = Theme.Card;
        info.Controls.Add(_scCacheLabel, 0, 2);
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(14, 0, 0, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _scClearCacheButton.Text = "Очистити кеш шейдерів";
        _scClearCacheButton.Dock = DockStyle.Fill;
        ConfigurePrimaryButton(_scClearCacheButton);
        _scClearCacheButton.Click += async (_, _) => await ClearStarCitizenCacheAsync();
        _scOpenFolderButton.Text = "Відкрити папку гри";
        _scOpenFolderButton.Dock = DockStyle.Fill;
        ConfigureSecondaryButton(_scOpenFolderButton);
        _scOpenFolderButton.Click += (_, _) => OpenDetectedPath(_snapshot?.StarCitizenPath);
        actions.Controls.Add(_scClearCacheButton, 0, 0);
        actions.Controls.Add(_scOpenFolderButton, 0, 2);
        grid.Controls.Add(info, 0, 0);
        grid.Controls.Add(actions, 1, 0);
        card.Controls.Add(grid);
        return card;
    }

    private Control CreateRestorePage()
    {
        var host = CreatePageHost();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateSimpleHeader("Скасувати останні зміни", "Повертає точні значення, які були до останнього застосування. Налаштування, створені програмою, будуть видалені."), 0, 0);
        var card = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 330,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20, 18, 20, 18),
            Radius = 18,
            BackColor = Theme.Card,
            BorderColor = Theme.Border
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        grid.Controls.Add(CreateCaption("ОСТАННЯ РЕЗЕРВНА КОПІЯ", Theme.Card), 0, 0);
        _restoreStatusLabel.Dock = DockStyle.Fill;
        _restoreStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _restoreStatusLabel.ForeColor = Theme.Text;
        _restoreStatusLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _restoreStatusLabel.BackColor = Theme.Card;
        grid.Controls.Add(_restoreStatusLabel, 0, 1);
        grid.Controls.Add(CreateCaption("ЩО БУДЕ ПОВЕРНЕНО", Theme.Card), 0, 2);
        _restoreItemsLabel.Dock = DockStyle.Fill;
        _restoreItemsLabel.TextAlign = ContentAlignment.TopLeft;
        _restoreItemsLabel.ForeColor = Theme.Muted;
        _restoreItemsLabel.Font = new Font("Segoe UI", 9F);
        _restoreItemsLabel.BackColor = Theme.Card;
        grid.Controls.Add(_restoreItemsLabel, 0, 3);
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        _restoreButton.Text = "Скасувати останні зміни";
        _restoreButton.Dock = DockStyle.Fill;
        ConfigurePrimaryButton(_restoreButton);
        _restoreButton.Click += async (_, _) => await RestoreLatestAsync();
        _openBackupsButton.Text = "Відкрити резервні копії";
        _openBackupsButton.Dock = DockStyle.Fill;
        ConfigureSecondaryButton(_openBackupsButton);
        _openBackupsButton.Click += (_, _) => OpenBackupsFolder();
        actions.Controls.Add(_restoreButton, 0, 0);
        actions.Controls.Add(_openBackupsButton, 2, 0);
        grid.Controls.Add(actions, 0, 4);
        card.Controls.Add(grid);
        layout.Controls.Add(card, 0, 1);
        host.Controls.Add(layout);
        return host;
    }

    private async Task ScanAsync()
    {
        if (_isBusy) return;
        SetBusy(true, "Аналізуємо Windows, програми та Star Citizen…");
        _recommendationScrollOffset = 0;
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        _cards.RowStyles.Clear();
        _cards.RowCount = 0;
        try
        {
            _snapshot = await _scanner.ScanAsync();
            _items = _engine.Analyze(_snapshot);
            var goodCount = _items.Count(item => item.Level == RecommendationLevel.Good);
            var attentionCount = _items.Count - goodCount;
            var score = 78 + (int)Math.Round(22D * goodCount / Math.Max(1, _items.Count));
            _scoreLabel.Text = $"{score}/100";
            _scoreCaption.Text = score >= 96 ? "Відмінний стан" : score >= 90 ? "Добрий стан" : "Є що покращити";
            _cpuValue.Text = NormalizeMetric(_snapshot.Cpu);
            _gpuValue.Text = NormalizeMetric(_snapshot.Gpu);
            _ramValue.Text = NormalizeMetric(_snapshot.Ram);
            _windowsValue.Text = NormalizeMetric(_snapshot.Windows);
            _statusLabel.Text = attentionCount == 0
                ? "Аналіз завершено: додаткові зміни не потрібні."
                : $"Аналіз завершено: знайдено {attentionCount} рекомендацій. Нижче видно, що саме зробить кожна з них.";
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
            UpdateApplicationsPage(_snapshot);
            UpdateStarCitizenPage(_snapshot);
            UpdateRestorePage();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Не вдалося завершити аналіз.";
            if (!IsSnapshotMode()) MessageBox.Show(exception.Message, "Помилка аналізу", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Margin = new Padding(0, 0, 0, 12),
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
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172F));
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
            RowCount = 3,
            Margin = new Padding(0, 0, 14, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Card
        };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
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
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.8F),
            BackColor = Theme.Card
        }, 0, 1);
        text.Controls.Add(new Label
        {
            Text = good ? "Змін не потрібно" : $"Зараз: {FormatCurrentState(item)}  →  Буде: {FormatRecommendedState(item)}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = good ? Theme.Good : Theme.Warning,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 2);
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(8, 5, 0, 5),
            BackColor = Theme.Card
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        var pill = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
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
            Text = "Що змінить",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            MinimumSize = new Size(0, 38)
        };
        ConfigureSecondaryButton(details);
        details.Click += (_, _) => ShowRecommendationDetails(item);
        right.Controls.Add(pill, 0, 0);
        right.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card }, 0, 1);
        right.Controls.Add(details, 0, 2);
        grid.Controls.Add(check, 0, 0);
        grid.Controls.Add(text, 1, 0);
        grid.Controls.Add(right, 2, 0);
        card.Controls.Add(grid);
        return card;
    }

    private void ShowRecommendationDetails(OptimizationItem item)
    {
        var restart = item.RequiresRestart ? "Так" : "Ні";
        var message = $"{item.Details}\n\nЗараз: {FormatCurrentState(item)}\nПісля застосування: {FormatRecommendedState(item)}\nПотрібне перезавантаження: {restart}\n\nПеред зміною TiHiY створить резервну копію.";
        MessageBox.Show(message, item.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateApplicationsPage(SystemSnapshot snapshot)
    {
        UpdateSoftwareCard(_rsiLauncherCard, snapshot.RsiLauncherFound, snapshot.RsiLauncherPath, "RSI Launcher знайдено");
        var starCitizenStatus = snapshot.StarCitizenInstallations.Count == 1
            ? $"Знайдено канал {snapshot.StarCitizenInstallations[0].Channel}"
            : $"Знайдено каналів: {snapshot.StarCitizenInstallations.Count} ({string.Join(", ", snapshot.StarCitizenInstallations.Select(item => item.Channel))})";
        UpdateSoftwareCard(_starCitizenCard, snapshot.StarCitizenFound, snapshot.StarCitizenPath, starCitizenStatus);
        UpdateSoftwareCard(_obsCard, snapshot.ObsFound, snapshot.ObsPath, "OBS Studio знайдено");
        UpdateSoftwareCard(_discordCard, snapshot.DiscordFound, snapshot.DiscordPath, "Discord знайдено");
        UpdateSoftwareCard(_steelSeriesCard, snapshot.SteelSeriesFound, snapshot.SteelSeriesPath, "SteelSeries GG / Sonar знайдено");
        UpdateSoftwareCard(_safetyCard, true, "Зміни застосовуються лише після підтвердження", "Захист увімкнено", false);
    }

    private static void UpdateSoftwareCard(SoftwareCardView view, bool found, string? path, string foundText, bool allowOpen = true)
    {
        view.CurrentPath = allowOpen ? path : null;
        view.StatusLabel.Text = found ? foundText : "Не знайдено";
        view.StatusLabel.ForeColor = found ? Theme.Good : Theme.Subtle;
        view.PathLabel.Text = found
            ? string.IsNullOrWhiteSpace(path) ? "Шлях не визначено" : path
            : "Програма не знайдена у стандартних місцях.";
        view.OpenButton.Enabled = found && allowOpen && !string.IsNullOrWhiteSpace(path);
        view.OpenButton.Visible = allowOpen;
    }

    private void UpdateStarCitizenPage(SystemSnapshot snapshot)
    {
        var count = snapshot.StarCitizenInstallations.Count;
        _scSummaryLabel.Text = count == 0
            ? "Star Citizen не знайдено у стандартних папках."
            : count == 1 ? $"Знайдено канал {snapshot.StarCitizenInstallations[0].Channel}." : $"Знайдено каналів гри: {count}.";
        _scCacheLabel.Text = snapshot.StarCitizenShaderCaches.Count == 0
            ? "Кеш шейдерів не знайдено — очищення не потрібне."
            : $"Знайдено папок кешу шейдерів: {snapshot.StarCitizenShaderCaches.Count}.";
        _scClearCacheButton.Enabled = !_isBusy && snapshot.StarCitizenShaderCaches.Count > 0;
        _scOpenFolderButton.Enabled = !_isBusy && snapshot.StarCitizenFound;
        _scChannelsPanel.SuspendLayout();
        _scChannelsPanel.Controls.Clear();
        var channels = new[] { "LIVE", "PTU", "EPTU", "TECH-PREVIEW" };
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            var installation = snapshot.StarCitizenInstallations.FirstOrDefault(item => string.Equals(item.Channel, channel, StringComparison.OrdinalIgnoreCase));
            _scChannelsPanel.Controls.Add(CreateStarCitizenChannelCard(channel, installation), 0, index);
        }
        _scChannelsPanel.ResumeLayout(true);
    }

    private static Control CreateStarCitizenChannelCard(string channel, StarCitizenInstallation? installation)
    {
        var found = installation is not null;
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 10, 16, 10),
            Radius = 14,
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
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        grid.Controls.Add(new Label
        {
            Text = channel,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 0, 0);
        grid.Controls.Add(new Label
        {
            Text = found ? installation!.Path : "Не встановлено",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = found ? Theme.Muted : Theme.Subtle,
            Font = new Font("Segoe UI", 8.2F),
            AutoEllipsis = true,
            BackColor = Theme.Card
        }, 1, 0);
        grid.Controls.Add(new Label
        {
            Text = found ? installation!.UserCfgExists ? "USER.cfg знайдено" : "USER.cfg відсутній" : "Не знайдено",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = found && installation!.UserCfgExists ? Theme.Good : Theme.Subtle,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            BackColor = Theme.Card
        }, 2, 0);
        card.Controls.Add(grid);
        return card;
    }

    private void UpdateRestorePage()
    {
        var folder = _backup.GetLatestBackupFolder();
        if (folder is null)
        {
            _restoreStatusLabel.Text = "Резервних копій ще немає.";
            _restoreStatusLabel.ForeColor = Theme.Muted;
            _restoreItemsLabel.Text = "Після першого застосування змін тут з'явиться можливість повернути попередній стан.";
            _restoreButton.Enabled = false;
            return;
        }
        try
        {
            var statePath = Path.Combine(folder, "state.json");
            var state = JsonSerializer.Deserialize<BackupState>(File.ReadAllText(statePath));
            if (state is null) throw new InvalidOperationException("Порожня резервна копія");
            _restoreStatusLabel.Text = $"Створено {state.CreatedAt:dd.MM.yyyy о HH:mm}";
            _restoreStatusLabel.ForeColor = Theme.Good;
            _restoreItemsLabel.Text = state.Items.Length == 0
                ? "У резервній копії немає змін для відновлення."
                : string.Join(Environment.NewLine, state.Items.Select(id => $"• {HumanizeItem(id)}"));
            _restoreButton.Enabled = !_isBusy && (state.RegistryValues.Count > 0 || !string.IsNullOrWhiteSpace(state.PowerSchemeGuid));
        }
        catch
        {
            _restoreStatusLabel.Text = "Останню резервну копію не вдалося прочитати.";
            _restoreStatusLabel.ForeColor = Theme.Warning;
            _restoreItemsLabel.Text = folder;
            _restoreButton.Enabled = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (_isBusy) return;
        var selected = _items.Where(item => item.Selected).ToArray();
        if (selected.Length == 0) return;
        var summary = string.Join("\n", selected.Select(item => $"• {item.Title}: {FormatRecommendedState(item)}"));
        var confirmation = MessageBox.Show($"Буде застосовано змін: {selected.Length}.\n\n{summary}\n\nПеред початком програма створить резервну копію. Продовжити?", "Підтвердження змін", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;
        SetBusy(true, "Створюємо резервну копію…");
        try
        {
            var folder = _backup.CreateBackup(selected.Select(item => item.Id));
            var log = await _optimizer.ApplyAsync(selected, new Progress<string>(message => _statusLabel.Text = $"Застосовуємо: {message}…"));
            await File.WriteAllLinesAsync(Path.Combine(folder, "apply.log"), log);
            var failed = log.Count(line => line.StartsWith("ПОМИЛКА", StringComparison.OrdinalIgnoreCase));
            var restartText = selected.Any(item => item.RequiresRestart) ? "\n\nДля завершення змін перезавантажте ПК." : string.Empty;
            MessageBox.Show(
                failed == 0
                    ? $"Зміни застосовано успішно.\n\nРезервна копія:\n{folder}{restartText}"
                    : $"Зміни застосовано з попередженнями.\n\nПеревірте журнал:\n{Path.Combine(folder, "apply.log")}{restartText}",
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

    private async Task ClearStarCitizenCacheAsync()
    {
        if (_snapshot is null || _snapshot.StarCitizenShaderCaches.Count == 0 || _isBusy) return;
        var confirmation = MessageBox.Show(
            $"Буде видалено папок кешу шейдерів: {_snapshot.StarCitizenShaderCaches.Count}.\n\nФайли гри, USER.cfg і налаштування керування не видаляються. Після очищення перший запуск може короткочасно підфризувати, поки кеш створюється заново.\n\nПродовжити?",
            "Очищення кешу Star Citizen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;
        var item = _items.FirstOrDefault(value => value.Id == "sc-shaders")
                   ?? new OptimizationItem
                   {
                       Id = "sc-shaders",
                       Title = "Star Citizen — кеш шейдерів",
                       Summary = "Очищення кешу",
                       Details = "Видаляється лише кеш шейдерів.",
                       CurrentValue = "Знайдено",
                       RecommendedValue = "Очистити",
                       Level = RecommendationLevel.Recommended,
                       Selected = true,
                       RequiresRestart = false
                   };
        SetBusy(true, "Очищаємо кеш шейдерів Star Citizen…");
        try
        {
            var folder = _backup.CreateBackup([item.Id]);
            var log = await _optimizer.ApplyAsync([item]);
            await File.WriteAllLinesAsync(Path.Combine(folder, "apply.log"), log);
            var failed = log.Any(line => line.StartsWith("ПОМИЛКА", StringComparison.OrdinalIgnoreCase));
            MessageBox.Show(
                failed ? string.Join(Environment.NewLine, log) : "Кеш шейдерів очищено. Гра створить його заново під час наступного запуску.",
                "Star Citizen",
                MessageBoxButtons.OK,
                failed ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            await ScanAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RestoreLatestAsync()
    {
        if (_isBusy || !_restoreButton.Enabled) return;
        var confirmation = MessageBox.Show("Програма поверне точні значення, які були до останнього застосування. Продовжити?", "Скасування останніх змін", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes) return;
        SetBusy(true, "Відновлюємо попередні налаштування…");
        try
        {
            var result = await _restore.RestoreLatestAsync(new Progress<string>(message => _restoreStatusLabel.Text = message));
            var failed = result.Log.Count(line => line.StartsWith("ПОМИЛКА", StringComparison.OrdinalIgnoreCase));
            MessageBox.Show(
                failed == 0 ? "Попередні налаштування відновлено." : $"Відновлення завершено з попередженнями: {failed}.\n\nЖурнал: {Path.Combine(result.Folder, "restore.log")}",
                "TiHiY System Optimizer",
                MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            await ScanAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося скасувати зміни", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            UpdateRestorePage();
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _isBusy = busy;
        _scanButton.Enabled = !busy;
        if (busy) _restoreButton.Enabled = false;
        else UpdateRestorePage();
        _scOpenFolderButton.Enabled = !busy && (_snapshot?.StarCitizenFound ?? false);
        _scClearCacheButton.Enabled = !busy && (_snapshot?.StarCitizenShaderCaches.Count ?? 0) > 0;
        if (!string.IsNullOrWhiteSpace(message)) _statusLabel.Text = message;
        UpdateApplyButtonState();
    }

    private void UpdateApplyButtonState()
    {
        var enabled = !_isBusy && _items.Any(item => item.Selected);
        _applyButton.Enabled = enabled;
        _applyButton.BackColor = enabled ? Theme.Accent : Theme.CardHover;
        _applyButton.ForeColor = enabled ? Theme.Window : Theme.Subtle;
    }

    private void RegisterPage(PageKind kind, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages[kind] = page;
        _pageHost.Controls.Add(page);
    }

    private void AddNavButton(Control parent, PageKind kind, string text)
    {
        var button = CreateNavButton(text, () => ShowPage(kind));
        _navButtons[kind] = button;
        parent.Controls.Add(button);
    }

    private void ShowPage(PageKind kind)
    {
        foreach (var pair in _pages) pair.Value.Visible = pair.Key == kind;
        if (_pages.TryGetValue(kind, out var activePage)) activePage.BringToFront();
        foreach (var pair in _navButtons)
        {
            var active = pair.Key == kind;
            pair.Value.BackColor = active ? Theme.AccentSoft : Theme.Sidebar;
            pair.Value.ForeColor = active ? Theme.Accent : Theme.Muted;
        }
        if (kind == PageKind.Restore) UpdateRestorePage();
    }

    private void AttachRecommendationWheel(Control control)
    {
        control.MouseWheel += RecommendationMouseWheel;
        foreach (Control child in control.Controls) AttachRecommendationWheel(child);
    }

    private void RecommendationMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_scrollTrack.Visible) return;
        var direction = e.Delta > 0 ? -1 : 1;
        SetRecommendationScroll(_recommendationScrollOffset + direction * 82);
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
        if (_recommendationsViewport.IsDisposed || _cards.IsDisposed) return;
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
        if (!_scrollTrack.Visible || _scrollTrack.Height <= 0 || _cards.Height <= 0) return;
        var viewportHeight = Math.Max(1, _recommendationsViewport.ClientSize.Height);
        var thumbHeight = Math.Max(34, (int)Math.Round(_scrollTrack.Height * Math.Min(1D, viewportHeight / (double)_cards.Height)));
        thumbHeight = Math.Min(_scrollTrack.Height, thumbHeight);
        _scrollThumb.Height = thumbHeight;
        var travel = Math.Max(0, _scrollTrack.Height - thumbHeight);
        _scrollThumb.Top = max == 0 ? 0 : (int)Math.Round(travel * (_recommendationScrollOffset / (double)max));
    }

    private static string FormatCurrentState(OptimizationItem item)
    {
        return item.Id switch
        {
            "game-mode" => item.CurrentValue == "1" ? "Увімкнено" : "Вимкнено / не задано",
            "game-dvr" => item.CurrentValue == "0" ? "Вимкнено" : "Увімкнено / не задано",
            "hags" => item.CurrentValue == "2" ? "Увімкнено" : "Вимкнено / не задано",
            "mouse-accel" => item.CurrentValue == "0" ? "Вимкнено" : "Увімкнено / не задано",
            "power-plan" => string.IsNullOrWhiteSpace(item.CurrentValue) ? "Невідомо" : item.CurrentValue,
            "sc-shaders" => item.CurrentValue,
            _ => item.CurrentValue
        };
    }

    private static string FormatRecommendedState(OptimizationItem item)
    {
        return item.Id switch
        {
            "game-mode" => "Увімкнено",
            "game-dvr" => "Вимкнено",
            "hags" => "Увімкнено",
            "mouse-accel" => "Вимкнено",
            "power-plan" => "Збалансований",
            "sc-shaders" => "Очистити кеш",
            _ => item.RecommendedValue
        };
    }

    private static string HumanizeItem(string id)
    {
        return id switch
        {
            "game-mode" => "Ігровий режим Windows",
            "game-dvr" => "Фоновий запис Xbox",
            "hags" => "Апаратне планування GPU",
            "mouse-accel" => "Прискорення миші",
            "power-plan" => "План живлення Windows",
            "sc-shaders" => "Кеш шейдерів Star Citizen не відновлюється — гра створює його заново",
            _ => id
        };
    }

    private static string NormalizeMetric(string value) => string.IsNullOrWhiteSpace(value) ? "Невідомо" : value.Trim();

    private static Panel CreatePageHost() => new()
    {
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = new Padding(26, 18, 26, 22),
        BackColor = Theme.Window
    };

    private static Control CreateSimpleHeader(string title, string description)
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
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(CreatePageTitle(title), 0, 0);
        header.Controls.Add(CreatePageDescription(description), 0, 1);
        return header;
    }

    private static Label CreatePageTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
        BackColor = Theme.Window
    };

    private static Label CreatePageDescription(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.TopLeft,
        ForeColor = Theme.Muted,
        Font = new Font("Segoe UI", 9F),
        AutoEllipsis = true,
        BackColor = Theme.Window
    };

    private static Label CreateCaption(string text, Color background) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = Theme.Subtle,
        Font = new Font("Segoe UI Semibold", 7.4F, FontStyle.Bold),
        BackColor = background
    };

    private static Button CreateNavButton(string text, Action action)
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
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }

    private static void OpenDetectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target)) throw new DirectoryNotFoundException(path);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося відкрити папку", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenBackupsFolder()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TiHiY", "SystemOptimizer", "Backups");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Не вдалося відкрити резервні копії", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsSnapshotMode() => string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal);

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            var raw = message.LParam.ToInt64();
            var screenPoint = new Point(unchecked((short)(raw & 0xFFFF)), unchecked((short)((raw >> 16) & 0xFFFF)));
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

    private enum PageKind
    {
        Overview,
        Applications,
        StarCitizen,
        Restore
    }

    private sealed class SoftwareCardView
    {
        public Label StatusLabel { get; } = new();
        public Label PathLabel { get; } = new();
        public Button OpenButton { get; } = new();
        public string? CurrentPath { get; set; }
    }
}
