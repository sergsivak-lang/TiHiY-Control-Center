using System.Text.Json;
namespace TiHiY.SystemOptimizer.Core;
public sealed class BackupService
{
    public string CreateBackup(IEnumerable<string> ids)
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"TiHiY","SystemOptimizer","Backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"manifest.json"),JsonSerializer.Serialize(new { createdAt=DateTime.Now,items=ids.ToArray() },new JsonSerializerOptions{WriteIndented=true}));
        Export(root,"HKCU",@"Software\Microsoft\GameBar","gamebar.reg"); Export(root,"HKCU",@"System\GameConfigStore","gameconfig.reg"); Export(root,"HKCU",@"Control Panel\Mouse","mouse.reg"); Export(root,"HKLM",@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers","graphics.reg");
        File.WriteAllText(Path.Combine(root,"power-plan.txt"),CommandRunner.RunAsync("powercfg","/getactivescheme").GetAwaiter().GetResult().Output); return root;
    }
    private static void Export(string root,string hive,string path,string file){ try { CommandRunner.RunAsync("reg.exe",$"export \"{hive}\\{path}\" \"{Path.Combine(root,file)}\" /y").GetAwaiter().GetResult(); } catch { } }
}
