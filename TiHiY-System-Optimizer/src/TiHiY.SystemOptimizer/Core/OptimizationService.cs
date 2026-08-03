using Microsoft.Win32;
using TiHiY.SystemOptimizer.Models;
namespace TiHiY.SystemOptimizer.Core;
public sealed class OptimizationService
{
    public async Task<IReadOnlyList<string>> ApplyAsync(IEnumerable<OptimizationItem> selected,IProgress<string>? progress=null)
    {
        var log=new List<string>(); foreach(var item in selected){ progress?.Report(item.Title); try { switch(item.Id){ case "game-mode": SetDword(RegistryHive.CurrentUser,@"SOFTWARE\Microsoft\GameBar","AutoGameModeEnabled",1); break; case "game-dvr": SetDword(RegistryHive.CurrentUser,@"System\GameConfigStore","GameDVR_Enabled",0); break; case "hags": SetDword(RegistryHive.LocalMachine,@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers","HwSchMode",2); break; case "mouse-accel": SetString(@"Control Panel\Mouse","MouseSpeed","0"); SetString(@"Control Panel\Mouse","MouseThreshold1","0"); SetString(@"Control Panel\Mouse","MouseThreshold2","0"); break; case "power-plan": await CommandRunner.RunAsync("powercfg","/setactive SCHEME_BALANCED"); break; } log.Add($"OK • {item.Title}"); } catch(Exception ex){ log.Add($"ПОМИЛКА • {item.Title}: {ex.Message}"); } } return log;
    }
    private static void SetDword(RegistryHive hive,string path,string name,int value){ using var b=RegistryKey.OpenBaseKey(hive,RegistryView.Registry64); using var k=b.CreateSubKey(path,true)??throw new InvalidOperationException(path); k.SetValue(name,value,RegistryValueKind.DWord); }
    private static void SetString(string path,string name,string value){ using var b=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64); using var k=b.CreateSubKey(path,true)??throw new InvalidOperationException(path); k.SetValue(name,value,RegistryValueKind.String); }
}
