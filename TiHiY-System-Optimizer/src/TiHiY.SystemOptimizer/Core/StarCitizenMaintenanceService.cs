using System.Diagnostics;
using System.Text.Json;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class StarCitizenMaintenanceService
{
    private static readonly string[] KnownChannels = ["LIVE", "PTU", "EPTU", "TECH-PREVIEW"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<StarCitizenMaintenanceSnapshot> AnalyzeAsync()
    {
        var system = await new SystemScanner().ScanAsync();
        var manualPath = LoadSettings().ManualStarCitizenPath;
        var manualInstallations = ResolveInstallations(manualPath);
        var installations = system.StarCitizenInstallations
            .Concat(manualInstallations)
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => Array.IndexOf(KnownChannels, item.Channel))
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var targets = BuildCleanupTargets(installations, system.RsiLauncherPath);

        if (IsSnapshotMode())
        {
            targets =
            [
                NewSnapshotTarget("sc-shaders", "Кеш шейдерів Star Citizen", "Шейдери гри. Після очищення створяться заново.", 1_420_000_000L, true),
                NewSnapshotTarget("rsi-launcher-cache", "Кеш RSI Launcher", "Тимчасові web/GPU-кеші лаунчера. Авторизацію та бібліотеку не видаляємо.", 182_000_000L, true, requiresLauncherClosed: true),
                NewSnapshotTarget("sc-logs", "Логи та звіти про збої", "Старі Game.log, crash dumps і резервні логи. Налаштування гри не зачіпаються.", 96_000_000L, true),
                NewSnapshotTarget("windows-dx-cache", "DirectX shader cache Windows", "Глобальний кеш DirectX для всіх ігор. Перші запуски можуть трохи підфризувати.", 620_000_000L, false, affectsAllGames: true),
                NewSnapshotTarget("gpu-driver-cache", "Кеш драйвера GPU", "Глобальні NVIDIA/AMD shader cache. Не змінює профілі драйвера.", 810_000_000L, false, affectsAllGames: true)
            ];
        }

        return new StarCitizenMaintenanceSnapshot
        {
            ManualPath = manualPath,
            Installations = installations,
            RsiLauncherPath = system.RsiLauncherPath,
            Targets = targets
        };
    }

    public ManualPathValidation ValidateManualPath(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return new ManualPathValidation(false, "Папку не вибрано.", null, Array.Empty<StarCitizenInstallation>());
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        }
        catch
        {
            return new ManualPathValidation(false, "Шлях має некоректний формат.", null, Array.Empty<StarCitizenInstallation>());
        }

        var installations = ResolveInstallations(fullPath);
        if (installations.Count == 0)
        {
            return new ManualPathValidation(
                false,
                "У цій папці не знайдено Bin64\\StarCitizen.exe. Оберіть LIVE/PTU/EPTU/TECH-PREVIEW або папку StarCitizen, що містить ці канали.",
                fullPath,
                installations);
        }

        return new ManualPathValidation(
            true,
            installations.Count == 1
                ? $"Знайдено Star Citizen: {installations[0].Channel}."
                : $"Знайдено каналів Star Citizen: {installations.Count}.",
            fullPath,
            installations);
    }

    public void SaveManualPath(string path)
    {
        var validation = ValidateManualPath(path);
        if (!validation.Success || validation.NormalizedPath is null)
        {
            throw new InvalidOperationException(validation.Message);
        }

        var settings = LoadSettings();
        settings.ManualStarCitizenPath = validation.NormalizedPath;
        SaveSettings(settings);
    }

    public void ClearManualPath()
    {
        var settings = LoadSettings();
        settings.ManualStarCitizenPath = null;
        SaveSettings(settings);
    }

    public async Task<StarCitizenCleanupResult> CleanAsync(
        StarCitizenMaintenanceSnapshot snapshot,
        IEnumerable<string> selectedTargetIds,
        IProgress<string>? progress = null)
    {
        var selected = selectedTargetIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => snapshot.Targets.FirstOrDefault(target => string.Equals(target.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(target => target is not null)
            .Cast<StarCitizenCleanupTarget>()
            .ToArray();

        if (selected.Length == 0)
        {
            throw new InvalidOperationException("Не вибрано жодної категорії для очищення.");
        }

        if (Process.GetProcessesByName("StarCitizen").Length > 0)
        {
            throw new InvalidOperationException("Star Citizen зараз запущений. Закрийте гру перед глибоким очищенням.");
        }

        if (selected.Any(target => target.RequiresLauncherClosed) && IsRsiLauncherRunning())
        {
            throw new InvalidOperationException("RSI Launcher зараз запущений. Закрийте лаунчер, щоб безпечно очистити його кеш.");
        }

        var log = new List<string>();
        long plannedBytes = 0;
        long removedBytes = 0;
        var removedItems = 0;
        var failedItems = 0;

        foreach (var target in selected)
        {
            progress?.Report(target.Title);
            plannedBytes += target.EstimatedBytes;

            foreach (var path in target.Paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var before = GetPathSize(path);
                    DeleteKnownSafePath(target.Id, path);
                    removedBytes += before;
                    removedItems++;
                    log.Add($"OK • {target.Title} • {path}");
                }
                catch (Exception exception)
                {
                    failedItems++;
                    log.Add($"ПОМИЛКА • {target.Title} • {path} • {exception.Message}");
                }
            }
        }

        var logRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TiHiY",
            "SystemOptimizer",
            "CleanupLogs");
        Directory.CreateDirectory(logRoot);
        var logPath = Path.Combine(logRoot, $"star-citizen-cleanup-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await File.WriteAllLinesAsync(logPath,
        [
            $"TiHiY System Optimizer • Star Citizen cleanup • {DateTime.Now:O}",
            $"Planned bytes: {plannedBytes}",
            $"Removed bytes: {removedBytes}",
            $"Removed items: {removedItems}",
            $"Failed items: {failedItems}",
            "",
            .. log
        ]);

        return new StarCitizenCleanupResult
        {
            PlannedBytes = plannedBytes,
            RemovedBytes = removedBytes,
            RemovedItems = removedItems,
            FailedItems = failedItems,
            LogPath = logPath,
            Log = log
        };
    }

    private static IReadOnlyList<StarCitizenCleanupTarget> BuildCleanupTargets(
        IReadOnlyList<StarCitizenInstallation> installations,
        string? rsiLauncherPath)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var shaderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var starCitizenLocal = Path.Combine(local, "Star Citizen");
        TryAddDirectories(shaderPaths, starCitizenLocal, "sc-alpha-*");
        foreach (var installation in installations)
        {
            AddIfDirectory(shaderPaths, Path.Combine(installation.Path, "USER", "Client", "0", "shaders"));
        }

        var launcherCachePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var launcherDataRoots = new[]
        {
            Path.Combine(roaming, "rsilauncher"),
            Path.Combine(local, "rsilauncher")
        };
        foreach (var root in launcherDataRoots)
        {
            foreach (var relative in new[]
                     {
                         "Cache",
                         "Code Cache",
                         "GPUCache",
                         "DawnCache",
                         "DawnGraphiteCache",
                         "DawnWebGPUCache",
                         "ShaderCache"
                     })
            {
                AddIfDirectory(launcherCachePaths, Path.Combine(root, relative));
            }
        }

        var logPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in installations)
        {
            AddIfFile(logPaths, Path.Combine(installation.Path, "Game.log"));
            TryAddFiles(logPaths, installation.Path, "Game.log.*");
            AddIfDirectory(logPaths, Path.Combine(installation.Path, "logbackups"));
        }
        AddIfDirectory(logPaths, Path.Combine(starCitizenLocal, "Crashes"));
        AddIfDirectory(logPaths, Path.Combine(starCitizenLocal, "Logs"));
        AddIfDirectory(logPaths, Path.Combine(roaming, "rsilauncher", "logs"));

        var dxPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfDirectory(dxPaths, Path.Combine(local, "D3DSCache"));

        var gpuPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfDirectory(gpuPaths, Path.Combine(local, "NVIDIA", "DXCache"));
        AddIfDirectory(gpuPaths, Path.Combine(local, "NVIDIA", "GLCache"));
        AddIfDirectory(gpuPaths, Path.Combine(programData, "NVIDIA Corporation", "NV_Cache"));
        AddIfDirectory(gpuPaths, Path.Combine(local, "AMD", "DxCache"));
        AddIfDirectory(gpuPaths, Path.Combine(local, "AMD", "VkCache"));
        AddIfDirectory(gpuPaths, Path.Combine(local, "AMD", "GLCache"));

        return
        [
            NewTarget(
                "sc-shaders",
                "Кеш шейдерів Star Citizen",
                "Основний кеш шейдерів гри. USER.cfg, керування, графічні налаштування та Data.p4k не зачіпаються.",
                shaderPaths,
                selectedByDefault: true),
            NewTarget(
                "rsi-launcher-cache",
                "Кеш RSI Launcher",
                "Лише Cache / Code Cache / GPUCache та shader web-cache. Cookies, авторизацію, бібліотеку й маніфести не видаляємо.",
                launcherCachePaths,
                selectedByDefault: true,
                requiresLauncherClosed: true),
            NewTarget(
                "sc-logs",
                "Логи та звіти про збої",
                "Game.log, старі crash/log папки та логи лаунчера. Це діагностика, а не налаштування гри.",
                logPaths,
                selectedByDefault: true),
            NewTarget(
                "windows-dx-cache",
                "DirectX shader cache Windows",
                "Глобальний кеш DirectX. Безпечний для налаштувань, але зачіпає всі ігри й може спричинити короткі підфризи під час повторної компіляції.",
                dxPaths,
                selectedByDefault: false,
                affectsAllGames: true),
            NewTarget(
                "gpu-driver-cache",
                "Кеш драйвера GPU",
                "Глобальні NVIDIA/AMD DX/GL/Vulkan cache. Профілі драйвера не змінюються; перші запуски ігор можуть компілювати шейдери заново.",
                gpuPaths,
                selectedByDefault: false,
                affectsAllGames: true)
        ];
    }

    private static StarCitizenCleanupTarget NewTarget(
        string id,
        string title,
        string description,
        IEnumerable<string> paths,
        bool selectedByDefault,
        bool affectsAllGames = false,
        bool requiresLauncherClosed = false)
    {
        var existing = paths
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new StarCitizenCleanupTarget
        {
            Id = id,
            Title = title,
            Description = description,
            Paths = existing,
            EstimatedBytes = existing.Sum(GetPathSize),
            SelectedByDefault = selectedByDefault,
            AffectsAllGames = affectsAllGames,
            RequiresLauncherClosed = requiresLauncherClosed
        };
    }

    private static StarCitizenCleanupTarget NewSnapshotTarget(
        string id,
        string title,
        string description,
        long bytes,
        bool selected,
        bool affectsAllGames = false,
        bool requiresLauncherClosed = false)
        => new()
        {
            Id = id,
            Title = title,
            Description = description,
            Paths = [$@"C:\TiHiY-Snapshot\{id}"],
            EstimatedBytes = bytes,
            SelectedByDefault = selected,
            AffectsAllGames = affectsAllGames,
            RequiresLauncherClosed = requiresLauncherClosed
        };

    private static IReadOnlyList<StarCitizenInstallation> ResolveInstallations(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath)) return Array.Empty<StarCitizenInstallation>();

        var result = new List<StarCitizenInstallation>();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string normalized;
        try
        {
            normalized = Path.GetFullPath(selectedPath);
        }
        catch
        {
            return result;
        }

        if (File.Exists(normalized) && string.Equals(Path.GetFileName(normalized), "StarCitizen.exe", StringComparison.OrdinalIgnoreCase))
        {
            var bin64 = Path.GetDirectoryName(normalized);
            var channel = bin64 is null ? null : Path.GetDirectoryName(bin64);
            if (channel is not null) candidates.Add(channel);
        }
        else if (Directory.Exists(normalized))
        {
            candidates.Add(normalized);
            candidates.Add(Path.Combine(normalized, "StarCitizen"));
            candidates.Add(Path.Combine(normalized, "Roberts Space Industries", "StarCitizen"));
        }
        else
        {
            return result;
        }

        foreach (var candidate in candidates.ToArray())
        {
            if (File.Exists(Path.Combine(candidate, "Bin64", "StarCitizen.exe")))
            {
                AddInstallation(result, candidate);
            }

            foreach (var channel in KnownChannels)
            {
                var channelPath = Path.Combine(candidate, channel);
                if (File.Exists(Path.Combine(channelPath, "Bin64", "StarCitizen.exe")))
                {
                    AddInstallation(result, channelPath, channel);
                }
            }
        }

        return result
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddInstallation(List<StarCitizenInstallation> result, string path, string? channel = null)
    {
        var folder = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var resolvedChannel = channel
            ?? KnownChannels.FirstOrDefault(value => string.Equals(value, folder, StringComparison.OrdinalIgnoreCase))
            ?? "CUSTOM";
        var cfgPath = Path.Combine(path, "USER.cfg");
        var cfg = ReadUserCfg(cfgPath);
        result.Add(new StarCitizenInstallation
        {
            Channel = resolvedChannel,
            Path = path,
            UserCfgExists = cfg.Exists,
            UserCfgActiveLines = cfg.ActiveLines,
            UserCfgLanguage = cfg.Language,
            UserCfgLastWriteTime = cfg.LastWriteTime
        });
    }

    private static (bool Exists, int ActiveLines, string? Language, DateTime? LastWriteTime) ReadUserCfg(string path)
    {
        if (!File.Exists(path)) return (false, 0, null, null);
        try
        {
            var active = 0;
            string? language = null;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith('#') || line.StartsWith(';')) continue;
                active++;
                if (line.StartsWith("g_language", StringComparison.OrdinalIgnoreCase))
                {
                    var separator = line.IndexOf('=');
                    language = separator >= 0 ? line[(separator + 1)..].Trim().Trim('"') : line["g_language".Length..].Trim().Trim('"');
                }
            }
            return (true, active, language, File.GetLastWriteTime(path));
        }
        catch
        {
            return (true, 0, null, null);
        }
    }

    private static void DeleteKnownSafePath(string targetId, string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsAllowedPath(targetId, fullPath))
        {
            throw new InvalidOperationException("Захист TiHiY заблокував видалення невідомого шляху.");
        }

        if (File.Exists(fullPath))
        {
            File.SetAttributes(fullPath, FileAttributes.Normal);
            File.Delete(fullPath);
            return;
        }

        if (Directory.Exists(fullPath))
        {
            ClearReadOnly(fullPath);
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static bool IsAllowedPath(string targetId, string fullPath)
    {
        var local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var roaming = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        var programData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        return targetId switch
        {
            "sc-shaders" =>
                IsUnder(fullPath, Path.Combine(local, "Star Citizen"))
                || fullPath.EndsWith(Path.Combine("USER", "Client", "0", "shaders"), StringComparison.OrdinalIgnoreCase),
            "rsi-launcher-cache" =>
                IsUnder(fullPath, Path.Combine(roaming, "rsilauncher"))
                || IsUnder(fullPath, Path.Combine(local, "rsilauncher")),
            "sc-logs" =>
                IsUnder(fullPath, Path.Combine(local, "Star Citizen"))
                || IsUnder(fullPath, Path.Combine(roaming, "rsilauncher"))
                || string.Equals(Path.GetFileName(fullPath), "Game.log", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(fullPath).StartsWith("Game.log.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(fullPath), "logbackups", StringComparison.OrdinalIgnoreCase),
            "windows-dx-cache" => IsUnder(fullPath, Path.Combine(local, "D3DSCache")),
            "gpu-driver-cache" =>
                IsUnder(fullPath, Path.Combine(local, "NVIDIA"))
                || IsUnder(fullPath, Path.Combine(local, "AMD"))
                || IsUnder(fullPath, Path.Combine(programData, "NVIDIA Corporation", "NV_Cache")),
            _ => false
        };
    }

    private static bool IsUnder(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static long GetPathSize(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;

            long total = 0;
            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(current))
                    {
                        try { total += new FileInfo(file).Length; } catch { }
                    }
                    foreach (var directory in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0) pending.Push(directory);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static void ClearReadOnly(string root)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
        }
        catch { }
    }

    private static bool IsRsiLauncherRunning()
    {
        try
        {
            return Process.GetProcesses().Any(process =>
            {
                try
                {
                    var name = process.ProcessName;
                    return name.Contains("RSI", StringComparison.OrdinalIgnoreCase)
                           && name.Contains("Launcher", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });
        }
        catch
        {
            return false;
        }
    }

    private static void AddIfDirectory(HashSet<string> paths, string path)
    {
        if (Directory.Exists(path)) paths.Add(path);
    }

    private static void AddIfFile(HashSet<string> paths, string path)
    {
        if (File.Exists(path)) paths.Add(path);
    }

    private static void TryAddDirectories(HashSet<string> paths, string root, string pattern)
    {
        try
        {
            if (Directory.Exists(root))
                foreach (var path in Directory.GetDirectories(root, pattern, SearchOption.TopDirectoryOnly)) paths.Add(path);
        }
        catch { }
    }

    private static void TryAddFiles(HashSet<string> paths, string root, string pattern)
    {
        try
        {
            if (Directory.Exists(root))
                foreach (var path in Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly)) paths.Add(path);
        }
        catch { }
    }

    private static OptimizerSettings LoadSettings()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path)) return new OptimizerSettings();
            return JsonSerializer.Deserialize<OptimizerSettings>(File.ReadAllText(path)) ?? new OptimizerSettings();
        }
        catch
        {
            return new OptimizerSettings();
        }
    }

    private static void SaveSettings(OptimizerSettings settings)
    {
        var path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static string GetSettingsPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TiHiY", "SystemOptimizer", "settings.json");

    private static bool IsSnapshotMode()
        => string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal);

    private sealed class OptimizerSettings
    {
        public string? ManualStarCitizenPath { get; set; }
    }
}

public sealed record ManualPathValidation(
    bool Success,
    string Message,
    string? NormalizedPath,
    IReadOnlyList<StarCitizenInstallation> Installations);

public sealed class StarCitizenMaintenanceSnapshot
{
    public string? ManualPath { get; init; }
    public string? RsiLauncherPath { get; init; }
    public IReadOnlyList<StarCitizenInstallation> Installations { get; init; } = Array.Empty<StarCitizenInstallation>();
    public IReadOnlyList<StarCitizenCleanupTarget> Targets { get; init; } = Array.Empty<StarCitizenCleanupTarget>();
}

public sealed class StarCitizenCleanupTarget
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();
    public long EstimatedBytes { get; init; }
    public bool SelectedByDefault { get; init; }
    public bool AffectsAllGames { get; init; }
    public bool RequiresLauncherClosed { get; init; }
    public bool Available => Paths.Count > 0;
}

public sealed class StarCitizenCleanupResult
{
    public long PlannedBytes { get; init; }
    public long RemovedBytes { get; init; }
    public int RemovedItems { get; init; }
    public int FailedItems { get; init; }
    public string LogPath { get; init; } = string.Empty;
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}
