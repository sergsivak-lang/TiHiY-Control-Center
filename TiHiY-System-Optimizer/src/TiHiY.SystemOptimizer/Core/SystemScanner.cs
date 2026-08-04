using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Text.Json;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class SystemScanner
{
    private static readonly string[] StarCitizenChannels = ["LIVE", "PTU", "EPTU", "TECH-PREVIEW"];

    public Task<SystemSnapshot> ScanAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal))
        {
            return Task.FromResult(new SystemSnapshot
            {
                Windows = "Windows 11 Pro 24H2",
                Cpu = "AMD Ryzen 7 9800X3D 8-Core Processor",
                Gpu = "NVIDIA GeForce RTX 5060 Ti",
                Ram = "32 ГБ DDR5-6000",
                Bios = "MSI X870E GAMING PLUS WIFI • BIOS 1.A62",
                PowerPlan = "Balanced",
                RsiLauncherPath = @"C:\Program Files\Roberts Space Industries\RSI Launcher\RSI Launcher.exe",
                ObsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
                DiscordPath = @"C:\Users\User\AppData\Local\Discord\Update.exe",
                SteelSeriesPath = @"C:\Program Files\SteelSeries\GG\SteelSeriesGG.exe",
                StarCitizenInstallations =
                [
                    new StarCitizenInstallation
                    {
                        Channel = "LIVE",
                        Path = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE",
                        UserCfgExists = true,
                        UserCfgActiveLines = 7,
                        UserCfgLanguage = "korean_(south_korea)",
                        UserCfgLastWriteTime = DateTime.Now.AddDays(-2)
                    }
                ],
                StarCitizenShaderCaches = [@"C:\Users\User\AppData\Local\Star Citizen\sc-alpha-example"],
                StreamAudit = new StreamAudit
                {
                    ObsConfigFound = true,
                    ObsConfigRoot = @"C:\Users\User\AppData\Roaming\obs-studio",
                    ObsProfileName = "TiHiY Stream",
                    ObsProfilePath = @"C:\Users\User\AppData\Roaming\obs-studio\basic\profiles\TiHiY Stream",
                    ObsCanvasResolution = "2560×1440",
                    ObsOutputResolution = "2560×1440",
                    ObsFps = "60 FPS",
                    ObsOutputMode = "Advanced",
                    ObsStreamEncoder = "NVIDIA NVENC H.264",
                    ObsAudioSampleRate = "48 kHz",
                    ObsRunning = true,
                    DiscordRunning = true,
                    SteelSeriesRunning = true,
                    SonarRunning = true
                }
            });
        }

        return Task.Run(() =>
        {
            var installations = FindStarCitizenInstallations();
            var obsPath = FindObs();
            var discordPath = FindDiscord();
            var steelSeriesPath = FindSteelSeries();

            return new SystemSnapshot
            {
                Windows = ReadWindows(),
                Cpu = ReadWmi("Win32_Processor", "Name"),
                Gpu = ReadWmi("Win32_VideoController", "Name"),
                Ram = ReadRam(),
                Bios = $"{ReadWmi("Win32_BaseBoard", "Manufacturer")} {ReadWmi("Win32_BaseBoard", "Product")} • BIOS {ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion")}",
                PowerPlan = ReadPowerPlan(),
                RsiLauncherPath = FindRsiLauncher(),
                ObsPath = obsPath,
                DiscordPath = discordPath,
                SteelSeriesPath = steelSeriesPath,
                StarCitizenInstallations = installations,
                StarCitizenShaderCaches = FindStarCitizenShaderCaches(installations),
                StreamAudit = ReadStreamAudit(obsPath, discordPath, steelSeriesPath)
            };
        });
    }

    private static string ReadWindows()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = Convert.ToString(key?.GetValue("ProductName")) ?? "Windows";
            var display = Convert.ToString(key?.GetValue("DisplayVersion"));
            var build = Convert.ToString(key?.GetValue("CurrentBuildNumber"));
            return string.Join(" ", new[] { product, display, string.IsNullOrWhiteSpace(build) ? null : $"(build {build})" }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        catch
        {
            return "Невідомо";
        }
    }

    private static string ReadWmi(string cls, string prop)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            var values = searcher.Get()
                .Cast<ManagementObject>()
                .Select(item => item[prop]?.ToString()?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var result = string.Join(" / ", values!);
            return result.Length > 0 ? result : "Невідомо";
        }
        catch
        {
            return "Невідомо";
        }
    }

    private static string ReadRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            var value = Convert.ToUInt64(searcher.Get().Cast<ManagementObject>().First()["TotalPhysicalMemory"]);
            return $"{Math.Round(value / 1024d / 1024d / 1024d)} ГБ";
        }
        catch
        {
            return "Невідомо";
        }
    }

    private static string ReadPowerPlan()
    {
        try
        {
            var result = CommandRunner.RunAsync("powercfg", "/getactivescheme").GetAwaiter().GetResult();
            var line = result.Output.Trim();
            var open = line.IndexOf('(');
            var close = line.LastIndexOf(')');
            return open >= 0 && close > open ? line[(open + 1)..close] : line;
        }
        catch
        {
            return "Невідомо";
        }
    }

    private static IReadOnlyList<StarCitizenInstallation> FindStarCitizenInstallations()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddStarCitizenRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddStarCitizenRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        foreach (var drive in SafeFixedDrives())
        {
            foreach (var relative in new[]
                     {
                         @"Roberts Space Industries\StarCitizen",
                         @"Games\Roberts Space Industries\StarCitizen",
                         @"Games\StarCitizen",
                         @"Program Files\Roberts Space Industries\StarCitizen"
                     })
            {
                roots.Add(Path.Combine(drive.RootDirectory.FullName, relative));
            }
        }

        AddLauncherLibraryRoots(roots);

        var result = new List<StarCitizenInstallation>();
        foreach (var root in roots)
        {
            foreach (var channel in StarCitizenChannels)
            {
                var channelPath = string.Equals(Path.GetFileName(root), channel, StringComparison.OrdinalIgnoreCase)
                    ? root
                    : Path.Combine(root, channel);
                if (!File.Exists(Path.Combine(channelPath, "Bin64", "StarCitizen.exe")))
                {
                    continue;
                }

                var cfg = ReadUserCfg(Path.Combine(channelPath, "USER.cfg"));
                result.Add(new StarCitizenInstallation
                {
                    Channel = channel,
                    Path = channelPath,
                    UserCfgExists = cfg.Exists,
                    UserCfgActiveLines = cfg.ActiveLines,
                    UserCfgLanguage = cfg.Language,
                    UserCfgLastWriteTime = cfg.LastWriteTime
                });
            }
        }

        return result
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => Array.IndexOf(StarCitizenChannels, item.Channel))
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (bool Exists, int ActiveLines, string? Language, DateTime? LastWriteTime) ReadUserCfg(string path)
    {
        if (!File.Exists(path))
        {
            return (false, 0, null, null);
        }

        try
        {
            var active = 0;
            string? language = null;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith('#') || line.StartsWith(';'))
                {
                    continue;
                }

                active++;
                if (line.StartsWith("g_language", StringComparison.OrdinalIgnoreCase))
                {
                    var separator = line.IndexOf('=');
                    language = separator >= 0
                        ? line[(separator + 1)..].Trim().Trim('"')
                        : line["g_language".Length..].Trim().Trim('"');
                }
            }

            return (true, active, language, File.GetLastWriteTime(path));
        }
        catch
        {
            return (true, 0, null, null);
        }
    }

    private static void AddStarCitizenRoot(HashSet<string> roots, string baseFolder)
    {
        if (!string.IsNullOrWhiteSpace(baseFolder))
        {
            roots.Add(Path.Combine(baseFolder, "Roberts Space Industries", "StarCitizen"));
        }
    }

    private static void AddLauncherLibraryRoots(HashSet<string> roots)
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rsilauncher", "library-folders.json");
            if (!File.Exists(file)) return;

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var value in EnumerateJsonStrings(document.RootElement))
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                if (expanded.Contains("StarCitizen", StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = expanded.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fileName = Path.GetFileName(normalized);
                    roots.Add(StarCitizenChannels.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(normalized) ?? normalized
                        : normalized);
                }
                else if (Directory.Exists(expanded))
                {
                    roots.Add(Path.Combine(expanded, "StarCitizen"));
                    roots.Add(Path.Combine(expanded, "Roberts Space Industries", "StarCitizen"));
                }
            }
        }
        catch
        {
            // Launcher settings are optional.
        }
    }

    private static IEnumerable<string> EnumerateJsonStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (element.GetString() is { } value) yield return value;
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                foreach (var nested in EnumerateJsonStrings(child))
                    yield return nested;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                foreach (var nested in EnumerateJsonStrings(property.Value))
                    yield return nested;
                break;
        }
    }

    private static string? FindRsiLauncher()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return FirstExistingFile(
                   Path.Combine(programFiles, "Roberts Space Industries", "RSI Launcher", "RSI Launcher.exe"),
                   Path.Combine(programFilesX86, "Roberts Space Industries", "RSI Launcher", "RSI Launcher.exe"))
               ?? FindInstalledExecutable(["RSI Launcher", "Roberts Space Industries"], ["RSI Launcher.exe"]);
    }

    private static string? FindObs()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return FirstExistingFile(
                   Path.Combine(programFiles, "obs-studio", "bin", "64bit", "obs64.exe"),
                   Path.Combine(programFiles, "OBS Studio", "bin", "64bit", "obs64.exe"))
               ?? FindInstalledExecutable(["OBS Studio"], ["obs64.exe"]);
    }

    private static string? FindDiscord()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var folderName in new[] { "Discord", "DiscordCanary", "DiscordPTB" })
            {
                var root = Path.Combine(local, folderName);
                var update = Path.Combine(root, "Update.exe");
                if (File.Exists(update)) return update;

                if (Directory.Exists(root))
                {
                    var executable = Directory.GetDirectories(root, "app-*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(path => Path.Combine(path, "Discord.exe"))
                        .FirstOrDefault(File.Exists);
                    if (executable is not null) return executable;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? FindSteelSeries()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return FirstExistingFile(
                   Path.Combine(programFiles, "SteelSeries", "GG", "SteelSeriesGG.exe"),
                   Path.Combine(programFiles, "SteelSeries", "SteelSeries Engine 3", "SteelSeriesEngine3.exe"))
               ?? FindInstalledExecutable(["SteelSeries GG", "SteelSeries Engine"], ["SteelSeriesGG.exe", "SteelSeriesEngine3.exe"]);
    }

    private static string? FindInstalledExecutable(string[] displayNameFragments, string[] executableNames)
    {
        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Registry64)
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;

                foreach (var subName in uninstall.GetSubKeyNames())
                {
                    using var app = uninstall.OpenSubKey(subName);
                    var displayName = Convert.ToString(app?.GetValue("DisplayName"));
                    if (string.IsNullOrWhiteSpace(displayName)
                        || !displayNameFragments.Any(fragment => displayName.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var displayIcon = NormalizeExecutablePath(Convert.ToString(app?.GetValue("DisplayIcon")));
                    if (displayIcon is not null && File.Exists(displayIcon)) return displayIcon;

                    var installLocation = Convert.ToString(app?.GetValue("InstallLocation"));
                    if (string.IsNullOrWhiteSpace(installLocation)) continue;
                    foreach (var executableName in executableNames)
                    {
                        var direct = Path.Combine(installLocation, executableName);
                        if (File.Exists(direct)) return direct;
                        try
                        {
                            var recursive = Directory.EnumerateFiles(installLocation, executableName, SearchOption.AllDirectories).FirstOrDefault();
                            if (recursive is not null) return recursive;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? NormalizeExecutablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().Trim('"');
        var comma = trimmed.LastIndexOf(',');
        if (comma > 2 && int.TryParse(trimmed[(comma + 1)..], out _))
        {
            trimmed = trimmed[..comma].Trim().Trim('"');
        }
        return Environment.ExpandEnvironmentVariables(trimmed);
    }

    private static IReadOnlyList<string> FindStarCitizenShaderCaches(IReadOnlyList<StarCitizenInstallation> installations)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Star Citizen");
            if (Directory.Exists(root))
            {
                foreach (var directory in Directory.GetDirectories(root, "sc-alpha-*", SearchOption.TopDirectoryOnly)) result.Add(directory);
            }
        }
        catch
        {
        }

        foreach (var installation in installations)
        {
            var legacy = Path.Combine(installation.Path, "USER", "Client", "0", "shaders");
            if (Directory.Exists(legacy)) result.Add(legacy);
        }
        return result.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static StreamAudit ReadStreamAudit(string? obsPath, string? discordPath, string? steelSeriesPath)
    {
        var configRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio");
        var configFound = Directory.Exists(configRoot);
        string? profileName = null;
        string? profilePath = null;
        string? canvas = null;
        string? output = null;
        string? fps = null;
        string? mode = null;
        string? encoder = null;
        string? audio = null;

        try
        {
            if (configFound)
            {
                var global = ReadIni(Path.Combine(configRoot, "global.ini"));
                profileName = GetIni(global, "Basic", "Profile");
                var profileDir = GetIni(global, "Basic", "ProfileDir") ?? profileName;
                var profilesRoot = Path.Combine(configRoot, "basic", "profiles");

                if (!string.IsNullOrWhiteSpace(profileDir))
                {
                    var direct = Path.Combine(profilesRoot, profileDir);
                    if (Directory.Exists(direct)) profilePath = direct;
                }

                if (profilePath is null && Directory.Exists(profilesRoot))
                {
                    foreach (var directory in Directory.GetDirectories(profilesRoot))
                    {
                        var ini = ReadIni(Path.Combine(directory, "basic.ini"));
                        var name = GetIni(ini, "General", "Name");
                        if (!string.IsNullOrWhiteSpace(profileName)
                            && string.Equals(name, profileName, StringComparison.OrdinalIgnoreCase))
                        {
                            profilePath = directory;
                            break;
                        }
                    }

                    profilePath ??= Directory.GetDirectories(profilesRoot).Length == 1
                        ? Directory.GetDirectories(profilesRoot)[0]
                        : null;
                }

                if (profilePath is not null)
                {
                    var basic = ReadIni(Path.Combine(profilePath, "basic.ini"));
                    profileName ??= GetIni(basic, "General", "Name") ?? Path.GetFileName(profilePath);
                    canvas = FormatResolution(GetIni(basic, "Video", "BaseCX"), GetIni(basic, "Video", "BaseCY"));
                    output = FormatResolution(GetIni(basic, "Video", "OutputCX"), GetIni(basic, "Video", "OutputCY"));
                    fps = FormatFps(basic);
                    mode = GetIni(basic, "Output", "Mode");
                    encoder = string.Equals(mode, "Advanced", StringComparison.OrdinalIgnoreCase)
                        ? GetIni(basic, "AdvOut", "Encoder")
                        : GetIni(basic, "SimpleOutput", "StreamEncoder");
                    audio = FormatSampleRate(GetIni(basic, "Audio", "SampleRate"));
                }
            }
        }
        catch
        {
            // OBS audit is read-only. A malformed profile must not fail the full system scan.
        }

        return new StreamAudit
        {
            ObsConfigFound = configFound,
            ObsConfigRoot = configFound ? configRoot : null,
            ObsProfileName = profileName,
            ObsProfilePath = profilePath,
            ObsCanvasResolution = canvas,
            ObsOutputResolution = output,
            ObsFps = fps,
            ObsOutputMode = mode,
            ObsStreamEncoder = HumanizeEncoder(encoder),
            ObsAudioSampleRate = audio,
            ObsRunning = IsProcessRunning("obs64"),
            DiscordRunning = IsProcessRunning("Discord"),
            SteelSeriesRunning = IsProcessRunning("SteelSeriesGG") || IsProcessRunning("SteelSeriesEngine3"),
            SonarRunning = IsProcessRunning("SteelSeriesSonar") || IsProcessRunning("SteelSeriesSonarHelper")
        };
    }

    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        var section = string.Empty;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (!result.ContainsKey(section)) result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            if (!result.TryGetValue(section, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[section] = values;
            }
            values[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        return result;
    }

    private static string? GetIni(Dictionary<string, Dictionary<string, string>> ini, string section, string key)
    {
        return ini.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string? FormatResolution(string? width, string? height)
        => int.TryParse(width, out var w) && int.TryParse(height, out var h) ? $"{w}×{h}" : null;

    private static string? FormatFps(Dictionary<string, Dictionary<string, string>> ini)
    {
        var common = GetIni(ini, "Video", "FPSCommon");
        if (!string.IsNullOrWhiteSpace(common)) return $"{common} FPS";
        var integer = GetIni(ini, "Video", "FPSInt");
        if (!string.IsNullOrWhiteSpace(integer)) return $"{integer} FPS";
        if (double.TryParse(GetIni(ini, "Video", "FPSNum"), out var num)
            && double.TryParse(GetIni(ini, "Video", "FPSDen"), out var den)
            && den > 0)
        {
            return $"{num / den:0.##} FPS";
        }
        return null;
    }

    private static string? FormatSampleRate(string? value)
    {
        if (!int.TryParse(value, out var rate)) return value;
        return rate >= 1000 ? $"{rate / 1000d:0.#} kHz" : $"{rate} Hz";
    }

    private static string? HumanizeEncoder(string? encoder)
    {
        if (string.IsNullOrWhiteSpace(encoder)) return null;
        if (encoder.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
        {
            if (encoder.Contains("av1", StringComparison.OrdinalIgnoreCase)) return "NVIDIA NVENC AV1";
            if (encoder.Contains("hevc", StringComparison.OrdinalIgnoreCase) || encoder.Contains("h265", StringComparison.OrdinalIgnoreCase)) return "NVIDIA NVENC HEVC";
            return "NVIDIA NVENC H.264";
        }
        if (encoder.Contains("qsv", StringComparison.OrdinalIgnoreCase)) return "Intel Quick Sync";
        if (encoder.Contains("amf", StringComparison.OrdinalIgnoreCase)) return "AMD HW Encoder";
        if (encoder.Contains("x264", StringComparison.OrdinalIgnoreCase)) return "x264 (CPU)";
        return encoder;
    }

    private static bool IsProcessRunning(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<DriveInfo> SafeFixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive =>
                {
                    try { return drive.IsReady && drive.DriveType == DriveType.Fixed; }
                    catch { return false; }
                })
                .ToArray();
        }
        catch
        {
            return Array.Empty<DriveInfo>();
        }
    }

    private static string? FirstExistingFile(params string[] candidates)
        => candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
}
