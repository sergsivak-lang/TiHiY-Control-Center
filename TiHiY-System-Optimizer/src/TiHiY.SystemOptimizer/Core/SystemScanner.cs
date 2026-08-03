using Microsoft.Win32;
using System.Management;
using TiHiY.SystemOptimizer.Models;
namespace TiHiY.SystemOptimizer.Core;
public sealed class SystemScanner
{
    public Task<SystemSnapshot> ScanAsync() => Task.Run(() => new SystemSnapshot { Windows = ReadWindows(), Cpu = ReadWmi("Win32_Processor", "Name"), Gpu = ReadWmi("Win32_VideoController", "Name"), Ram = ReadRam(), Bios = $"{ReadWmi("Win32_BaseBoard", "Manufacturer")} {ReadWmi("Win32_BaseBoard", "Product")} • BIOS {ReadWmi("Win32_BIOS", "SMBIOSBIOSVersion")}", PowerPlan = ReadPowerPlan() });
    private static string ReadWindows() { using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"); return $"{key?.GetValue("ProductName")} {key?.GetValue("DisplayVersion")} (build {key?.GetValue("CurrentBuildNumber")})"; }
    private static string ReadWmi(string cls, string prop) { try { using var s = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}"); var values = s.Get().Cast<ManagementObject>().Select(x => x[prop]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)); var result = string.Join(" / ", values); return result.Length > 0 ? result : "Невідомо"; } catch { return "Невідомо"; } }
    private static string ReadRam() { try { using var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"); var value = Convert.ToUInt64(s.Get().Cast<ManagementObject>().First()["TotalPhysicalMemory"]); return $"{Math.Round(value / 1024d / 1024d / 1024d)} ГБ"; } catch { return "Невідомо"; } }
    private static string ReadPowerPlan() { try { var line = CommandRunner.RunAsync("powercfg", "/getactivescheme").GetAwaiter().GetResult().Output.Trim(); var a = line.IndexOf('('); var b = line.LastIndexOf(')'); return a >= 0 && b > a ? line[(a + 1)..b] : line; } catch { return "Невідомо"; } }
}
