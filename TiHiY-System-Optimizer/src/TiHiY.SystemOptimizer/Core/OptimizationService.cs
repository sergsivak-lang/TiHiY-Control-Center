using Microsoft.Win32;
using System.Diagnostics;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class OptimizationService
{
    public async Task<IReadOnlyList<string>> ApplyAsync(
        IEnumerable<OptimizationItem> selected,
        IProgress<string>? progress = null)
    {
        var log = new List<string>();

        foreach (var item in selected)
        {
            progress?.Report(item.Title);
            try
            {
                switch (item.Id)
                {
                    case "game-mode":
                        SetDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                        break;

                    case "game-dvr":
                        SetDword(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
                        break;

                    case "hags":
                        SetDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
                        break;

                    case "mouse-accel":
                        SetString(@"Control Panel\Mouse", "MouseSpeed", "0");
                        SetString(@"Control Panel\Mouse", "MouseThreshold1", "0");
                        SetString(@"Control Panel\Mouse", "MouseThreshold2", "0");
                        break;

                    case "power-plan":
                        var powerResult = await CommandRunner.RunAsync("powercfg", "/setactive SCHEME_BALANCED");
                        if (powerResult.ExitCode != 0)
                        {
                            throw new InvalidOperationException(powerResult.Output);
                        }
                        break;

                    case "sc-shaders":
                        ClearStarCitizenShaderCaches();
                        break;
                }

                log.Add($"OK • {item.Title}");
            }
            catch (Exception exception)
            {
                log.Add($"ПОМИЛКА • {item.Title}: {exception.Message}");
            }
        }

        return log;
    }

    private static void ClearStarCitizenShaderCaches()
    {
        if (Process.GetProcessesByName("StarCitizen").Length > 0)
        {
            throw new InvalidOperationException("Star Citizen зараз запущений. Закрийте гру та повторіть очищення кешу.");
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(local, "Star Citizen");
        var deleted = 0;

        if (Directory.Exists(root))
        {
            foreach (var directory in Directory.GetDirectories(root, "sc-alpha-*", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(directory, true);
                deleted++;
            }
        }

        foreach (var legacy in FindLegacyShaderDirectories())
        {
            Directory.Delete(legacy, true);
            deleted++;
        }

        if (deleted == 0)
        {
            throw new InvalidOperationException("Кеш шейдерів Star Citizen уже відсутній.");
        }
    }

    private static IEnumerable<string> FindLegacyShaderDirectories()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            foreach (var relative in new[]
                     {
                         @"Program Files\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\shaders",
                         @"Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\shaders",
                         @"Games\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\shaders",
                         @"Games\StarCitizen\LIVE\USER\Client\0\shaders"
                     })
            {
                var path = Path.Combine(drive.RootDirectory.FullName, relative);
                if (Directory.Exists(path))
                {
                    result.Add(path);
                }
            }
        }

        return result;
    }

    private static void SetDword(RegistryHive hive, string path, string name, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(path, true) ?? throw new InvalidOperationException(path);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetString(string path, string name, string value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(path, true) ?? throw new InvalidOperationException(path);
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
