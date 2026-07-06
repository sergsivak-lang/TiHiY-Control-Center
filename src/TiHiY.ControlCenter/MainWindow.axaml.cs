using System.Globalization;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TiHiY.ControlCenter;

public sealed partial class MainWindow : Window
{
    private XDocument? _doc;
    private string? _currentPath;

    public MainWindow()
    {
        InitializeComponent();
        OpenButton.Click += OpenClicked;
        BackupButton.Click += BackupClicked;
        DefaultsButton.Click += DefaultsClicked;
        SaveButton.Click += SaveClicked;
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Відкрити Star Citizen XML",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Star Citizen XML") { Patterns = ["*.xml"] }]
        });
        if (files.Count == 0) return;
        _currentPath = files[0].Path.LocalPath;
        _doc = XDocument.Load(_currentPath, LoadOptions.PreserveWhitespace);
        FileText.Text = _currentPath;
        LoadValues();
        LoadBindings();
        StatusText.Text = "XML відкрито";
    }

    private void LoadValues()
    {
        if (_doc is null) return;
        XDeadzone.Text = ReadOption("x", "deadzone");
        XSaturation.Text = ReadOption("x", "saturation");
        YDeadzone.Text = ReadOption("y", "deadzone");
        YSaturation.Text = ReadOption("y", "saturation");
        TwistDeadzone.Text = ReadOption("rotz", "deadzone");
        TwistSaturation.Text = ReadOption("rotz", "saturation");
        SliderDeadzone.Text = ReadOption("slider1", "deadzone");
        SliderSaturation.Text = ReadOption("slider1", "saturation");
    }

    private string ReadOption(string input, string attr)
    {
        var value = _doc?.Descendants("deviceoptions")
            .Where(x => ((string?)x.Attribute("name"))?.Contains("T.16000M") == true)
            .Descendants("option")
            .FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attr) != null)
            ?.Attribute(attr)?.Value;
        return value ?? "";
    }

    private void SetOption(string input, string attr, string value)
    {
        if (_doc is null || string.IsNullOrWhiteSpace(value)) return;
        var device = _doc.Descendants("deviceoptions")
            .FirstOrDefault(x => ((string?)x.Attribute("name"))?.Contains("T.16000M") == true);
        if (device is null) return;
        var option = device.Elements("option")
            .FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attr) != null);
        if (option is null)
        {
            option = new XElement("option", new XAttribute("input", input));
            device.Add(option);
        }
        option.SetAttributeValue(attr, NormalizeDecimal(value));
    }

    private static string NormalizeDecimal(string value)
    {
        if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0.########", CultureInfo.InvariantCulture);
        return value;
    }

    private void LoadBindings()
    {
        BindingsList.Items.Clear();
        if (_doc is null) return;
        var rows = _doc.Descendants("action")
            .SelectMany(a => a.Elements("rebind").Select(r => new
            {
                Map = a.Parent?.Attribute("name")?.Value ?? "unknown",
                Action = a.Attribute("name")?.Value ?? "unknown",
                Input = r.Attribute("input")?.Value ?? ""
            }))
            .OrderBy(x => x.Map)
            .ThenBy(x => x.Action)
            .Select(x => $"{x.Map} / {x.Action}  →  {x.Input}")
            .ToList();
        foreach (var row in rows) BindingsList.Items.Add(row);
    }

    private void ApplyTextValuesToXml()
    {
        SetOption("x", "deadzone", XDeadzone.Text ?? "");
        SetOption("x", "saturation", XSaturation.Text ?? "");
        SetOption("y", "deadzone", YDeadzone.Text ?? "");
        SetOption("y", "saturation", YSaturation.Text ?? "");
        SetOption("rotz", "deadzone", TwistDeadzone.Text ?? "");
        SetOption("rotz", "saturation", TwistSaturation.Text ?? "");
        SetOption("slider1", "deadzone", SliderDeadzone.Text ?? "");
        SetOption("slider1", "saturation", SliderSaturation.Text ?? "");
    }

    private void DefaultsClicked(object? sender, RoutedEventArgs e)
    {
        XDeadzone.Text = "0.018";
        XSaturation.Text = "0.95";
        YDeadzone.Text = "0.018";
        YSaturation.Text = "0.95";
        TwistDeadzone.Text = "0.030";
        TwistSaturation.Text = "0.92";
        SliderDeadzone.Text = "0.010";
        SliderSaturation.Text = "1.0";
        StatusText.Text = "TiHiY Defaults застосовано до полів. Натисни Зберегти як...";
    }

    private async void BackupClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentPath)) { StatusText.Text = "Спочатку відкрий XML"; return; }
        var backup = Path.Combine(Path.GetDirectoryName(_currentPath)!, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(_currentPath)}");
        File.Copy(_currentPath, backup, overwrite: false);
        StatusText.Text = $"Backup створено: {backup}";
        await Task.CompletedTask;
    }

    private async void SaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_doc is null) { StatusText.Text = "Спочатку відкрий XML"; return; }
        ApplyTextValuesToXml();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Зберегти XML для Star Citizen",
            SuggestedFileName = "TiHiY_Universal_HOSAM.xml",
            FileTypeChoices = [new FilePickerFileType("XML") { Patterns = ["*.xml"] }]
        });
        if (file is null) return;
        _doc.Save(file.Path.LocalPath, SaveOptions.DisableFormatting);
        StatusText.Text = $"XML збережено: {file.Path.LocalPath}";
    }
}
