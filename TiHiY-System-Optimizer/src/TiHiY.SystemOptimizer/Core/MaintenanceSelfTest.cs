namespace TiHiY.SystemOptimizer.Core;

internal static class MaintenanceSelfTest
{
    public static async Task RunAsync()
    {
        var service = new StarCitizenMaintenanceService();
        string? previousManualPath = null;
        string? fakeRoot = null;
        string? shaderCache = null;

        try
        {
            previousManualPath = (await service.AnalyzeAsync()).ManualPath;

            fakeRoot = Path.Combine(Path.GetTempPath(), "TiHiY-SystemOptimizer-SelfTest", Guid.NewGuid().ToString("N"));
            var live = Path.Combine(fakeRoot, "StarCitizen", "LIVE");
            Directory.CreateDirectory(Path.Combine(live, "Bin64"));
            await File.WriteAllBytesAsync(Path.Combine(live, "Bin64", "StarCitizen.exe"), [0x4D, 0x5A]);
            await File.WriteAllTextAsync(
                Path.Combine(live, "USER.cfg"),
                "// self-test\r\ng_language = ukrainian\r\nr_DisplayInfo = 1\r\n");

            var validation = service.ValidateManualPath(Path.Combine(fakeRoot, "StarCitizen"));
            if (!validation.Success || validation.Installations.Count != 1)
            {
                throw new InvalidOperationException($"Manual path validation failed: {validation.Message}");
            }
            if (!validation.Installations[0].UserCfgExists || validation.Installations[0].UserCfgActiveLines != 2)
            {
                throw new InvalidOperationException("USER.cfg audit failed during maintenance self-test.");
            }

            service.SaveManualPath(Path.Combine(fakeRoot, "StarCitizen"));
            var withManualPath = await service.AnalyzeAsync();
            if (!withManualPath.Installations.Any(item => string.Equals(item.Path, live, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Saved manual Star Citizen path was not reused by the analyzer.");
            }

            shaderCache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Star Citizen",
                $"sc-alpha-tihiy-selftest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(shaderCache);
            await File.WriteAllBytesAsync(Path.Combine(shaderCache, "selftest.cache"), new byte[8192]);

            var cleanupSnapshot = await service.AnalyzeAsync();
            var shaderTarget = cleanupSnapshot.Targets.FirstOrDefault(target => target.Id == "sc-shaders");
            if (shaderTarget is null || !shaderTarget.Paths.Contains(shaderCache, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Shader cache discovery failed during maintenance self-test.");
            }

            var result = await service.CleanAsync(cleanupSnapshot, ["sc-shaders"]);
            if (Directory.Exists(shaderCache))
            {
                throw new InvalidOperationException("Shader cache cleanup did not remove the test directory.");
            }
            if (result.FailedItems != 0)
            {
                throw new InvalidOperationException($"Maintenance cleanup reported {result.FailedItems} failed item(s).");
            }
        }
        finally
        {
            try
            {
                if (previousManualPath is not null && service.ValidateManualPath(previousManualPath).Success)
                {
                    service.SaveManualPath(previousManualPath);
                }
                else
                {
                    service.ClearManualPath();
                }
            }
            catch
            {
                service.ClearManualPath();
            }

            try
            {
                if (fakeRoot is not null && Directory.Exists(fakeRoot)) Directory.Delete(fakeRoot, true);
            }
            catch { }

            try
            {
                if (shaderCache is not null && Directory.Exists(shaderCache)) Directory.Delete(shaderCache, true);
            }
            catch { }
        }
    }
}
