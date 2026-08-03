using Microsoft.Win32;
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
                        UserCfgExists = true
                    }
                ],
                StarCitizenShaderCaches = [@"C:\Users\User\AppData\Local\Star Citizen\sc-alpha-example"]
            });
        }

        return Task.Run(() =>
        {
            var installations = FindStarCitizenInstallations();
            return new SystemSnapshot
            {
                Windows = ReadWindows(),
                Cpu = ReadWmi("Win32_Processor", "Name"),
                Gpu = ReadWmi("Win32_VideoController", "Name"),
                Ram = ReadRam(),
                Bios = $"{ReadWmi("Win32_BaseBoard", "Manufacturer")} {ReadWmi("Win32_BaseBoard", "Product")} • BIOS {ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion")}",
                PowerPlan = ReadPowerPlan(),
                RsiLauncherPath = FindRsiLauncher(),
                ObsPath = FindObs(),
                DiscordPath = FindDiscord(),
                SteelSeriesPath = FindSteelSeries(),
                StarCitizenInstallations = installations,
                StarCitizenShaderCaches = FindStarCitizenShaderCaches(installations)
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
                var executable = Path.Combine(channelPath, "Bin64", "StarCitizen.exe");
                if (!File.Exists(executable))
                {
                    continue;
                }

                result.Add(new StarCitizenInstallation
                {
                    Channel = channel,
                    Path = channelPath,
                    UserCfgExists = File.Exists(Path.Combine(channelPath, "USER.cfg"))
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
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var file = Path.Combine(roaming, "rsilauncher", "library-folders.json");
            if (!File.Exists(file))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var value in EnumerateJsonStrings(document.RootElement))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                if (expanded.Contains("StarCitizen", StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = expanded.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fileName = Path.GetFileName(normalized);
                    roots.Add(StarCitizenChannels.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                        ? normalized
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
            // Launcher settings are optional. Known folders are still scanned.
        }
    }

    private static IEnumerable<string> EnumerateJsonStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (value is not null)
                {
                    yield return value;
                }
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    foreach (var nested in EnumerateJsonStrings(child))
                    {
                        yield return nested;
                    }
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in EnumerateJsonStrings(property.Value))
                    {
                        yield return nested;
                    }
                }
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
                if (File.Exists(update))
                {
                    return update;
                }

                if (Directory.Exists(root))
                {
                    var executable = Directory.GetDirectories(root, "app-*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(path => Path.Combine(path, "Discord.exe"))
                        .FirstOrDefault(File.Exists);
                    if (executable is not null)
                    {
                        return executable;
                    }
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
                if (uninstall is null)
                {
                    continue;
                }

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
                    if (displayIcon is not null && File.Exists(displayIcon))
                    {
                        return displayIcon;
                    }

                    var installLocation = Convert.ToString(app?.GetValue("InstallLocation"));
                    if (!string.IsNullOrWhiteSpace(installLocation))
                    {
                        foreach (var executableName in executableNames)
                        {
                            var direct = Path.Combine(installLocation, executableName);
                            if (File.Exists(direct))
                            {
                                return direct;
                            }

                            try
                            {
                                var recursive = Directory.EnumerateFiles(installLocation, executableName, SearchOption.AllDirectories)
                                    .FirstOrDefault();
                                if (recursive is not null)
                                {
                                    return recursive;
                                }
                            }
                            catch
                            {
                            }
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

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
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.Combine(local, "Star Citizen");
            if (Directory.Exists(root))
            {
                foreach (var directory in Directory.GetDirectories(root, "sc-alpha-*", SearchOption.TopDirectoryOnly))
                {
                    result.Add(directory);
                }
            }
        }
        catch
        {
        }

        foreach (var installation in installations)
        {
            var legacy = Path.Combine(installation.Path, "USER", "Client", "0", "shaders");
            if (Directory.Exists(legacy))
            {
                result.Add(legacy);
            }
        }

        return result.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<DriveInfo> SafeFixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive =>
                {
                    try
                    {
                        return drive.IsReady && drive.DriveType == DriveType.Fixed;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToArray();
        }
        catch
        {
            return Array.Empty<DriveInfo>();
        }
    }

    private static string? FirstExistingFile(params string[] candidates)
    {
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }
}
