using Microsoft.Win32;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

internal static class StartupSelfTest
{
    public static async Task RunAsync()
    {
        var testId = Guid.NewGuid().ToString("N");
        var keyPath = $@"Software\TiHiY\SystemOptimizerSelfTest\{testId}\Run";
        var valueName = "TiHiYStartupTest";
        var expected = @"C:\Test\TiHiY-Test.exe --silent";
        var tempRoot = Path.Combine(Path.GetTempPath(), "TiHiY-Startup-SelfTest", testId);
        Directory.CreateDirectory(tempRoot);
        var startupFile = Path.Combine(tempRoot, "TiHiY Test.lnk");
        await File.WriteAllTextAsync(startupFile, "test-shortcut-content");

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true))
            {
                key.SetValue(valueName, expected, RegistryValueKind.String);
            }

            var service = new StartupAuditService();
            var items = new[]
            {
                new StartupItem
                {
                    Id = "selftest-reg",
                    Name = "SelfTest Registry",
                    Kind = StartupItemKind.Registry,
                    RegistryHiveName = RegistryHive.CurrentUser.ToString(),
                    RegistryViewName = RegistryView.Registry64.ToString(),
                    RegistryKeyPath = keyPath,
                    RegistryValueName = valueName,
                    RegistryValueKindName = RegistryValueKind.String.ToString(),
                    Command = expected
                },
                new StartupItem
                {
                    Id = "selftest-file",
                    Name = "SelfTest File",
                    Kind = StartupItemKind.StartupFolder,
                    StartupFilePath = startupFile,
                    Command = startupFile
                }
            };

            var disabled = await service.DisableAsync(items);
            if (disabled.Changed != 2 || disabled.Failed != 0) throw new InvalidOperationException("Startup disable self-test failed.");
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                if (key?.GetValue(valueName) is not null) throw new InvalidOperationException("Registry startup value was not removed.");
            }
            if (File.Exists(startupFile)) throw new InvalidOperationException("Startup file was not moved to backup.");

            var restored = await service.RestoreLatestAsync();
            if (restored.Changed != 2 || restored.Failed != 0) throw new InvalidOperationException("Startup restore self-test failed.");
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                if (!string.Equals(Convert.ToString(key?.GetValue(valueName)), expected, StringComparison.Ordinal))
                    throw new InvalidOperationException("Registry startup value was not restored exactly.");
            }
            if (!File.Exists(startupFile)) throw new InvalidOperationException("Startup file was not restored.");
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\TiHiY\SystemOptimizerSelfTest\{testId}", throwOnMissingSubKey: false); } catch { }
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
