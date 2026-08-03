using Microsoft.Win32;
using System.Management;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class SystemScanner
{
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
                StarCitizenPath = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE",
                ObsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
                DiscordPath = @"C:\Users\User\AppData\Local\Discord\Update.exe",
                SteelSeriesPath = @"C:\Program Files\SteelSeries\GG\SteelSeriesGG.exe"
            });
        }

        return Task.Run(() =>
        {
            var starCitizen = FindStarCitizen();
            return new SystemSnapshot
            {
                Windows = ReadWindows(),
                Cpu = ReadWmi("Win32_Processor", "Name"),
                Gpu = ReadWmi("Win32_VideoController", "Name"),
                Ram = ReadRam(),
                Bios = $"{ReadWmi("Win32_BaseBoard", "Manufacturer")} {ReadWmi("Win32_BaseBoard", "Product")} • BIOS {ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion")}",
                PowerPlan = ReadPowerPlan(),
                StarCitizenPath = starCitizen,
                ObsPath = FindObs(),
                DiscordPath = FindDiscord(),
                SteelSeriesPath = FindSteelSeries(),
                StarCitizenShaderCaches = FindStarCitizenShaderCaches(starCitizen)
            };
        });
    }

    private static string ReadWindows()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return $"{key?.GetValue("ProductName")} {key?.GetValue("DisplayVersion")} (build {key?.GetValue("CurrentBuildNumber")})";
    }

    private static string ReadWmi(string cls, string prop)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            var values = searcher.Get()
                .Cast<ManagementObject>()
                .Select(item => item[prop]?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var result = string.Join(" / ", values);
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
            var line = CommandRunner.RunAsync("powercfg", "/getactivescheme").GetAwaiter().GetResult().Output.Trim();
            var open = line.IndexOf('(');
            var close = line.LastIndexOf(')');
            return open >= 0 && close > open ? line[(open + 1)..close] : line;
        }
        catch
        {
            return "Невідомо";
        }
    }

    private static string? FindStarCitizen()
    {
        var candidates = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "Roberts Space Industries", "StarCitizen", "LIVE"));
            candidates.Add(Path.Combine(programFiles, "Roberts Space Industries", "StarCitizen", "PTU"));
            candidates.Add(Path.Combine(programFiles, "Roberts Space Industries", "StarCitizen", "EPTU"));
            candidates.Add(Path.Combine(programFiles, "Roberts Space Industries", "StarCitizen", "TECH-PREVIEW"));
        }

        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            foreach (var relative in new[]
                     {
                         @"Roberts Space Industries\StarCitizen\LIVE",
                         @"Games\Roberts Space Industries\StarCitizen\LIVE",
                         @"Games\StarCitizen\LIVE",
                         @"Program Files\Roberts Space Industries\StarCitizen\LIVE"
                     })
            {
                candidates.Add(Path.Combine(drive.RootDirectory.FullName, relative));
            }
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "Bin64", "StarCitizen.exe")));
    }

    private static string? FindObs()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return FirstExistingFile(
            Path.Combine(programFiles, "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(programFiles, "OBS Studio", "bin", "64bit", "obs64.exe"));
    }

    private static string? FindDiscord()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.Combine(local, "Discord");
            var update = Path.Combine(root, "Update.exe");
            if (File.Exists(update))
            {
                return update;
            }

            if (Directory.Exists(root))
            {
                return Directory.GetDirectories(root, "app-*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, "Discord.exe"))
                    .FirstOrDefault(File.Exists);
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
            Path.Combine(programFiles, "SteelSeries", "SteelSeries Engine 3", "SteelSeriesEngine3.exe"));
    }

    private static IReadOnlyList<string> FindStarCitizenShaderCaches(string? starCitizenPath)
    {
        var result = new List<string>();
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.Combine(local, "Star Citizen");
            if (Directory.Exists(root))
            {
                result.AddRange(Directory.GetDirectories(root, "sc-alpha-*", SearchOption.TopDirectoryOnly));
            }
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(starCitizenPath))
        {
            var legacy = Path.Combine(starCitizenPath, "USER", "Client", "0", "shaders");
            if (Directory.Exists(legacy))
            {
                result.Add(legacy);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? FirstExistingFile(params string[] candidates)
    {
        return candidates.FirstOrDefault(File.Exists);
    }
}
