using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiHiY.ControlCenter.Models;
using TiHiY.ControlCenter.Services;

namespace TiHiY.ControlCenter.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly StarCitizenXmlService _xml = new();

    [ObservableProperty] private string status = "Готово. Відкрий layout_300126_exported.xml.";
    [ObservableProperty] private string currentFile = "Файл не відкрито";
    [ObservableProperty] private ObservableCollection<AxisOption> axisOptions = new();
    [ObservableProperty] private ObservableCollection<BindingInfo> bindings = new();

    [RelayCommand]
    private void OpenSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "profiles", "layout_300126_exported.xml");
        if (!File.Exists(path))
        {
            path = Path.Combine(Environment.CurrentDirectory, "assets", "profiles", "layout_300126_exported.xml");
        }
        if (!File.Exists(path))
        {
            Status = "Не знайдено assets/profiles/layout_300126_exported.xml. Скопіюй XML у цю папку.";
            return;
        }
        Load(path);
    }

    [RelayCommand]
    private void ApplyDefaults()
    {
        var count = _xml.ApplyTiHiYDefaults();
        AxisOptions = _xml.ReadAxisOptions();
        Status = count > 0 ? $"Застосовано TiHiY PRO Defaults: {count} змін." : "Спочатку відкрий XML.";
    }

    [RelayCommand]
    private void Backup()
    {
        try { Status = "Backup створено: " + _xml.CreateBackup(); }
        catch (Exception ex) { Status = "Backup помилка: " + ex.Message; }
    }

    [RelayCommand]
    private void SaveExport()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var outDir = Path.Combine(desktop, "TiHiY-Control-Center-Export");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, "layout_TiHiY_HOSAM_PRO_v0_1.xml");
            _xml.SaveAs(outPath);
            Status = "XML збережено: " + outPath;
        }
        catch (Exception ex) { Status = "Save помилка: " + ex.Message; }
    }

    private void Load(string path)
    {
        try
        {
            _xml.Load(path);
            CurrentFile = path;
            AxisOptions = _xml.ReadAxisOptions();
            Bindings = _xml.ReadBindings();
            Status = $"XML відкрито. Осі: {AxisOptions.Count}, біндів: {Bindings.Count}.";
        }
        catch (Exception ex) { Status = "Open помилка: " + ex.Message; }
    }
}
