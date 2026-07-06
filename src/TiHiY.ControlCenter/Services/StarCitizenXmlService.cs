using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Linq;
using TiHiY.ControlCenter.Models;

namespace TiHiY.ControlCenter.Services;

public sealed class StarCitizenXmlService
{
    private XDocument? _document;
    private string? _path;

    public string? CurrentPath => _path;
    public XDocument? Document => _document;

    public void Load(string path)
    {
        _document = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        _path = path;
    }

    public ObservableCollection<AxisOption> ReadAxisOptions()
    {
        var result = new ObservableCollection<AxisOption>();
        if (_document?.Root == null) return result;

        foreach (var dev in _document.Root.Elements("deviceoptions"))
        {
            var deviceName = (string?)dev.Attribute("name") ?? "Unknown";
            foreach (var opt in dev.Elements("option"))
            {
                var input = (string?)opt.Attribute("input") ?? "";
                foreach (var attrName in new[] { "deadzone", "saturation", "acceleration" })
                {
                    var attr = opt.Attribute(attrName);
                    if (attr == null) continue;
                    if (double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        result.Add(new AxisOption { DeviceName = deviceName, Input = input, Setting = attrName, Value = value });
                    }
                }
            }
        }
        return result;
    }

    public ObservableCollection<BindingInfo> ReadBindings()
    {
        var result = new ObservableCollection<BindingInfo>();
        if (_document?.Root == null) return result;

        foreach (var map in _document.Root.Elements("actionmap"))
        {
            var mapName = (string?)map.Attribute("name") ?? "";
            foreach (var action in map.Elements("action"))
            {
                var actionName = (string?)action.Attribute("name") ?? "";
                foreach (var rebind in action.Elements("rebind"))
                {
                    result.Add(new BindingInfo
                    {
                        ActionMap = mapName,
                        Action = actionName,
                        Input = (string?)rebind.Attribute("input") ?? "",
                        ActivationMode = (string?)rebind.Attribute("activationMode") ?? ""
                    });
                }
            }
        }
        return result;
    }

    public int ApplyTiHiYDefaults()
    {
        if (_document?.Root == null) return 0;
        var changes = 0;
        foreach (var dev in _document.Root.Elements("deviceoptions"))
        {
            var name = ((string?)dev.Attribute("name") ?? "").ToLowerInvariant();
            if (!name.Contains("t.16000m")) continue;
            changes += SetOption(dev, "x", "deadzone", 0.018);
            changes += SetOption(dev, "y", "deadzone", 0.018);
            changes += SetOption(dev, "rotz", "deadzone", 0.030);
            changes += SetOption(dev, "slider1", "deadzone", 0.010);
            changes += SetOption(dev, "x", "saturation", 0.950);
            changes += SetOption(dev, "y", "saturation", 0.950);
            changes += SetOption(dev, "rotz", "saturation", 0.950);
            changes += SetOption(dev, "slider1", "saturation", 1.000);
        }
        return changes;
    }

    private static int SetOption(XElement device, string input, string setting, double value)
    {
        var option = device.Elements("option").FirstOrDefault(e => (string?)e.Attribute("input") == input && e.Attribute(setting) != null);
        if (option == null)
        {
            option = new XElement("option", new XAttribute("input", input));
            device.Add(option);
        }
        option.SetAttributeValue(setting, value.ToString("0.###", CultureInfo.InvariantCulture));
        return 1;
    }

    public string CreateBackup()
    {
        if (_path == null) throw new InvalidOperationException("XML не відкрито.");
        var backupDir = Path.Combine(Path.GetDirectoryName(_path) ?? Environment.CurrentDirectory, "Backup");
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(_path) + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");
        File.Copy(_path, backupPath, overwrite: false);
        return backupPath;
    }

    public void SaveAs(string path)
    {
        if (_document == null) throw new InvalidOperationException("XML не відкрито.");
        _document.Save(path);
    }
}
