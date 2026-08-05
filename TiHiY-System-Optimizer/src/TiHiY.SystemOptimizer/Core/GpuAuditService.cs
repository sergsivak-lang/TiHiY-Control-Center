using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class GpuAuditService
{
    public Task<GpuAuditSnapshot> ScanAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal))
        {
            return Task.FromResult(new GpuAuditSnapshot
            {
                NvidiaFound = true,
                GpuName = "NVIDIA GeForce RTX 5060 Ti",
                DriverVersion = "590.12",
                DriverDate = "02.08.2026",
                Vram = "16 ГБ",
                Utilization = "27%",
                Temperature = "54 °C",
                MemoryUsage = "5.2 / 16 ГБ",
                HagsState = "Увімкнено",
                NvidiaSmiAvailable = true
            });
        }

        return Task.Run(ScanInternal);
    }

    private static GpuAuditSnapshot ScanInternal()
    {
        var wmi = ReadNvidiaFromWmi();
        var smi = ReadNvidiaSmi();
        return new GpuAuditSnapshot
        {
            NvidiaFound = wmi.Found || smi.Found,
            GpuName = FirstValue(smi.Name, wmi.Name, "NVIDIA не знайдено"),
            DriverVersion = FirstValue(smi.DriverVersion, wmi.DriverVersion, "Не визначено"),
            DriverDate = FirstValue(wmi.DriverDate, null, "Не визначено"),
            Vram = FirstValue(smi.Vram, null, "Не визначено"),
            Utilization = FirstValue(smi.Utilization, null, "Немає даних"),
            Temperature = FirstValue(smi.Temperature, null, "Немає даних"),
            MemoryUsage = FirstValue(smi.MemoryUsage, null, "Немає даних"),
            HagsState = ReadHagsState(),
            NvidiaSmiAvailable = smi.Found
        };
    }

    private static (bool Found, string? Name, string? DriverVersion, string? DriverDate) ReadNvidiaFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController");
            foreach (ManagementObject item in searcher.Get())
            {
                var name = Convert.ToString(item["Name"]);
                if (string.IsNullOrWhiteSpace(name) || !name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) continue;
                var date = Convert.ToString(item["DriverDate"]);
                return (true, name.Trim(), Convert.ToString(item["DriverVersion"])?.Trim(), FormatWmiDate(date));
            }
        }
        catch
        {
        }
        return (false, null, null, null);
    }

    private static (bool Found, string? Name, string? DriverVersion, string? Vram, string? Utilization, string? Temperature, string? MemoryUsage) ReadNvidiaSmi()
    {
        try
        {
            var executable = FindNvidiaSmi();
            if (executable is null) return (false, null, null, null, null, null, null);
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--query-gpu=name,driver_version,memory.total,memory.used,temperature.gpu,utilization.gpu --format=csv,noheader,nounits",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is null) return (false, null, null, null, null, null, null);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);
            if (!process.HasExited || process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return (false, null, null, null, null, null, null);
            var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (line is null) return (false, null, null, null, null, null, null);
            var parts = line.Split(',').Select(value => value.Trim()).ToArray();
            if (parts.Length < 6) return (false, null, null, null, null, null, null);
            var totalMb = ParseDouble(parts[2]);
            var usedMb = ParseDouble(parts[3]);
            return (
                true,
                parts[0],
                parts[1],
                totalMb > 0 ? FormatGb(totalMb) : null,
                $"{parts[5]}%",
                $"{parts[4]} °C",
                totalMb > 0 ? $"{usedMb / 1024d:0.0} / {totalMb / 1024d:0.#} ГБ" : null);
        }
        catch
        {
            return (false, null, null, null, null, null, null);
        }
    }

    private static string ReadHagsState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            var value = key?.GetValue("HwSchMode");
            if (value is null) return "Система вирішує автоматично";
            var numeric = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return numeric switch
            {
                2 => "Увімкнено",
                1 => "Вимкнено",
                _ => $"Стан: {numeric}"
            };
        }
        catch
        {
            return "Не вдалося прочитати";
        }
    }

    private static string? FindNvidiaSmi()
    {
        var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "nvidia-smi.exe");
        if (File.Exists(system)) return system;
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsm = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        return File.Exists(nvsm) ? nvsm : null;
    }

    private static string? FormatWmiDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8) return null;
        return DateTime.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("dd.MM.yyyy")
            : null;
    }

    private static double ParseDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string FormatGb(double mb) => $"{mb / 1024d:0.#} ГБ";

    private static string FirstValue(string? first, string? second, string fallback)
        => !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : fallback;
}
