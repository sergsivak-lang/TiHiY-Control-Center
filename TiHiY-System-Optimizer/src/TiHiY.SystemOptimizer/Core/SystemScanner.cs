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
                PowerPlan = "Balanced"
            });
        }

        return Task.Run(() => new SystemSnapshot
        {
            Windows = ReadWindows(),
            Cpu = ReadWmi("Win32_Processor", "Name"),
            Gpu = ReadWmi("Win32_VideoController", "Name"),
            Ram = ReadRam(),
            Bios = $"{ReadWmi("Win32_BaseBoard", "Manufacturer")} {ReadWmi("Win32_BaseBoard", "Product")} • BIOS {ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion")}",
            PowerPlan = ReadPowerPlan()
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
}
