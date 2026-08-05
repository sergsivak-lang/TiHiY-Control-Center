using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class StartupAuditService
{
    private static readonly string BackupRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TiHiY", "SystemOptimizer", "StartupBackups");

    private static readonly string[] KeepTokens =
    [
        "securityhealth", "windows security", "defender", "realtek", "nahimic",
        "steelseries", "sonar", "logitech", "razer", "corsair", "amd", "nvidia",
        "intel", "audio", "driver"
    ];

    private static readonly string[] OptionalTokens =
    [
        "discord", "steam", "spotify", "epicgameslauncher", "epic games launcher",
        "teams", "telegram", "skype", "battle.net", "battlenet", "gog galaxy",
        "ubisoft", "ea app", "adobe", "creative cloud"
    ];

    public Task<StartupAuditSnapshot> ScanAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("TIHIY_UI_SNAPSHOT"), "1", StringComparison.Ordinal))
        {
            return Task.FromResult(new StartupAuditSnapshot
            {
                Items =
                [
                    Demo("Discord", @"C:\Users\User\AppData\Local\Discord\Update.exe --processStart Discord.exe", StartupAdvice.Optional, "Месенджер. Можна не запускати разом із Windows — вручну відкриється як завжди."),
                    Demo("Steam", @"C:\Program Files (x86)\Steam\steam.exe -silent", StartupAdvice.Optional, "Ігровий клієнт. Вимкнення автозапуску не видаляє Steam та ігри."),
                    Demo("SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe", StartupAdvice.Keep, "Компонент безпеки Windows — рекомендуємо залишити."),
                    Demo("SteelSeries GG", @"C:\Program Files\SteelSeries\GG\SteelSeriesGG.exe", StartupAdvice.Keep, "Потрібний для Sonar/маршрутизації звуку — краще залишити."),
                    Demo("Adobe Creative Cloud", @"C:\Program Files\Adobe\Adobe Creative Cloud\ACC\Creative Cloud.exe", StartupAdvice.Optional, "Фоновий клієнт Adobe. Якщо не потрібен одразу після входу — автозапуск можна вимкнути.")
                ]
            });
        }

        return Task.Run(() => new StartupAuditSnapshot { Items = ScanInternal() });
    }

    public async Task<StartupChangeResult> DisableAsync(IEnumerable<StartupItem> selected, IProgress<string>? progress = null)
    {
        var items = selected.ToArray();
        if (items.Length == 0) return new StartupChangeResult();

        var session = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(session);
        var state = new StartupBackupState { CreatedAt = DateTime.Now };
        var log = new List<string>();
        var changed = 0;
        var failed = 0;

        foreach (var item in items)
        {
            progress?.Report(item.Name);
            try
            {
                switch (item.Kind)
                {
                    case StartupItemKind.Registry:
                        state.Items.Add(DisableRegistryItem(item));
                        break;
                    case StartupItemKind.StartupFolder:
                        state.Items.Add(DisableStartupFile(item, session));
                        break;
                }
                changed++;
                log.Add($"OK • {item.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                log.Add($"ПОМИЛКА • {item.Name}: {exception.Message}");
            }
        }

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(session, "state.json"), json);
        await File.WriteAllLinesAsync(Path.Combine(session, "change.log"), log);
        return new StartupChangeResult { Changed = changed, Failed = failed, BackupFolder = session, Log = log };
    }

    public async Task<StartupChangeResult> RestoreLatestAsync(IProgress<string>? progress = null)
    {
        var folder = GetLatestBackupFolder();
        if (folder is null) throw new InvalidOperationException("Резервної копії автозапуску ще немає.");
        var statePath = Path.Combine(folder, "state.json");
        var state = JsonSerializer.Deserialize<StartupBackupState>(await File.ReadAllTextAsync(statePath))
                    ?? throw new InvalidOperationException("Резервну копію не вдалося прочитати.");

        var changed = 0;
        var failed = 0;
        var log = new List<string>();
        foreach (var item in state.Items)
        {
            progress?.Report(item.Name);
            try
            {
                if (item.Kind == StartupItemKind.Registry) RestoreRegistryItem(item);
                else RestoreStartupFile(item);
                changed++;
                log.Add($"OK • {item.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                log.Add($"ПОМИЛКА • {item.Name}: {exception.Message}");
            }
        }

        await File.WriteAllLinesAsync(Path.Combine(folder, "restore.log"), log);
        return new StartupChangeResult { Changed = changed, Failed = failed, BackupFolder = folder, Log = log };
    }

    public string? GetLatestBackupFolder()
    {
        if (!Directory.Exists(BackupRoot)) return null;
        return Directory.GetDirectories(BackupRoot)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "state.json")));
    }

    private static IReadOnlyList<StartupItem> ScanInternal()
    {
        var result = new List<StartupItem>();
        ReadRunKey(result, RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Ваш профіль");
        ReadRunKey(result, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Для всіх користувачів");
        ReadRunKey(result, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "Для всіх користувачів (32-bit)");
        ReadStartupFolder(result, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Папка автозапуску");
        ReadStartupFolder(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Загальна папка автозапуску");

        return result
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => AdviceOrder(item.Advice))
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void ReadRunKey(List<StartupItem> result, RegistryHive hive, RegistryView view, string keyPath, string source)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (raw is null) continue;
                var command = Convert.ToString(raw) ?? string.Empty;
                var exe = ExtractExecutablePath(command);
                var advice = Classify(name, command, exe);
                result.Add(new StartupItem
                {
                    Id = $"reg|{hive}|{view}|{keyPath}|{name}",
                    Name = string.IsNullOrWhiteSpace(name) ? "Без назви" : name,
                    Command = command,
                    SourceLabel = source,
                    Kind = StartupItemKind.Registry,
                    Advice = advice.Advice,
                    AdviceText = advice.Text,
                    Company = ReadCompany(exe),
                    FilePath = exe,
                    RegistryHiveName = hive.ToString(),
                    RegistryViewName = view.ToString(),
                    RegistryKeyPath = keyPath,
                    RegistryValueName = name,
                    RegistryValueKindName = key.GetValueKind(name).ToString()
                });
            }
        }
        catch
        {
            // One inaccessible location should not fail the full audit.
        }
    }

    private static void ReadStartupFolder(List<StartupItem> result, string folder, string source)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var advice = Classify(name, file, file);
                result.Add(new StartupItem
                {
                    Id = $"file|{file}",
                    Name = name,
                    Command = file,
                    SourceLabel = source,
                    Kind = StartupItemKind.StartupFolder,
                    Advice = advice.Advice,
                    AdviceText = advice.Text,
                    Company = ReadCompany(file),
                    FilePath = file,
                    StartupFilePath = file
                });
            }
        }
        catch
        {
        }
    }

    private static (StartupAdvice Advice, string Text) Classify(string name, string command, string? exe)
    {
        var text = $"{name} {command} {exe}".ToLowerInvariant();
        if (KeepTokens.Any(text.Contains))
        {
            return (StartupAdvice.Keep, "Системний, драйверний або потрібний для обладнання компонент. Рекомендуємо залишити в автозапуску.");
        }
        if (OptionalTokens.Any(text.Contains))
        {
            return (StartupAdvice.Optional, "Звичайна користувацька програма. Автозапуск можна вимкнути — сама програма залишиться встановленою і відкриватиметься вручну.");
        }
        return (StartupAdvice.Unknown, "Програма не віднесена до безпечних шаблонів. TiHiY не буде пропонувати її вимкнення автоматично — рішення залишається за користувачем.");
    }

    private static StartupBackupItem DisableRegistryItem(StartupItem item)
    {
        var hive = Enum.Parse<RegistryHive>(item.RegistryHiveName ?? throw new InvalidOperationException("Не визначено hive"));
        var view = Enum.Parse<RegistryView>(item.RegistryViewName ?? RegistryView.Registry64.ToString());
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(item.RegistryKeyPath!, writable: true)
                        ?? throw new InvalidOperationException("Ключ автозапуску не знайдено.");
        var value = key.GetValue(item.RegistryValueName!, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) throw new InvalidOperationException("Пункт автозапуску вже відсутній.");
        var kind = key.GetValueKind(item.RegistryValueName!);
        var backup = new StartupBackupItem
        {
            Name = item.Name,
            Kind = StartupItemKind.Registry,
            RegistryHiveName = hive.ToString(),
            RegistryViewName = view.ToString(),
            RegistryKeyPath = item.RegistryKeyPath,
            RegistryValueName = item.RegistryValueName,
            RegistryValueKindName = kind.ToString(),
            RegistryStringValue = Convert.ToString(value)
        };
        key.DeleteValue(item.RegistryValueName!, throwOnMissingValue: false);
        return backup;
    }

    private static StartupBackupItem DisableStartupFile(StartupItem item, string session)
    {
        var original = item.StartupFilePath ?? throw new InvalidOperationException("Не визначено файл автозапуску.");
        if (!File.Exists(original)) throw new FileNotFoundException("Файл автозапуску вже відсутній.", original);
        var disabledFolder = Path.Combine(session, "files");
        Directory.CreateDirectory(disabledFolder);
        var destination = Path.Combine(disabledFolder, Path.GetFileName(original));
        if (File.Exists(destination)) destination = Path.Combine(disabledFolder, $"{Guid.NewGuid():N}-{Path.GetFileName(original)}");
        File.Move(original, destination);
        return new StartupBackupItem
        {
            Name = item.Name,
            Kind = StartupItemKind.StartupFolder,
            OriginalFilePath = original,
            BackupFilePath = destination
        };
    }

    private static void RestoreRegistryItem(StartupBackupItem item)
    {
        var hive = Enum.Parse<RegistryHive>(item.RegistryHiveName!);
        var view = Enum.Parse<RegistryView>(item.RegistryViewName!);
        var kind = Enum.TryParse<RegistryValueKind>(item.RegistryValueKindName, out var parsed) ? parsed : RegistryValueKind.String;
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.CreateSubKey(item.RegistryKeyPath!, writable: true)
                        ?? throw new InvalidOperationException("Не вдалося відкрити ключ автозапуску.");
        key.SetValue(item.RegistryValueName!, item.RegistryStringValue ?? string.Empty, kind);
    }

    private static void RestoreStartupFile(StartupBackupItem item)
    {
        if (string.IsNullOrWhiteSpace(item.BackupFilePath) || !File.Exists(item.BackupFilePath))
            throw new FileNotFoundException("Резервний файл не знайдено.");
        if (string.IsNullOrWhiteSpace(item.OriginalFilePath)) throw new InvalidOperationException("Не відомо, куди відновлювати файл.");
        Directory.CreateDirectory(Path.GetDirectoryName(item.OriginalFilePath)!);
        if (File.Exists(item.OriginalFilePath)) throw new IOException("У папці автозапуску вже існує файл з такою назвою.");
        File.Move(item.BackupFilePath, item.OriginalFilePath);
    }

    private static string? ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (expanded.StartsWith('"'))
        {
            var end = expanded.IndexOf('"', 1);
            if (end > 1) return expanded[1..end];
        }
        var match = Regex.Match(expanded, @"^(.+?\.exe)(?:\s|$)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim('"') : null;
    }

    private static string? ReadCompany(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            return FileVersionInfo.GetVersionInfo(path).CompanyName;
        }
        catch
        {
            return null;
        }
    }

    private static int AdviceOrder(StartupAdvice advice) => advice switch
    {
        StartupAdvice.Optional => 0,
        StartupAdvice.Unknown => 1,
        _ => 2
    };

    private static StartupItem Demo(string name, string command, StartupAdvice advice, string text) => new()
    {
        Id = $"demo-{name}",
        Name = name,
        Command = command,
        SourceLabel = "Автозапуск Windows",
        Kind = StartupItemKind.Registry,
        Advice = advice,
        AdviceText = text,
        Company = name.Contains("Security", StringComparison.OrdinalIgnoreCase) ? "Microsoft Corporation" : null
    };
}
