using Microsoft.Win32;
using System.Globalization;
using System.Text.Json;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class RestoreService
{
    private readonly BackupService _backup = new();

    public async Task<(string Folder, IReadOnlyList<string> Log)> RestoreLatestAsync(IProgress<string>? progress = null)
    {
        var folder = _backup.GetLatestBackupFolder()
            ?? throw new InvalidOperationException("Ще немає резервної копії, яку можна відновити.");

        var statePath = Path.Combine(folder, "state.json");
        var state = JsonSerializer.Deserialize<BackupState>(await File.ReadAllTextAsync(statePath))
            ?? throw new InvalidOperationException("Не вдалося прочитати дані резервної копії.");

        var log = new List<string>();

        foreach (var value in state.RegistryValues)
        {
            progress?.Report($"Відновлюємо {value.Name}");
            try
            {
                RestoreRegistryValue(value);
                log.Add($"OK • {value.Hive}\\{value.Path} • {value.Name}");
            }
            catch (Exception exception)
            {
                log.Add($"ПОМИЛКА • {value.Name}: {exception.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(state.PowerSchemeGuid))
        {
            progress?.Report("Повертаємо попередній план живлення");
            try
            {
                var result = await CommandRunner.RunAsync("powercfg", $"/setactive {state.PowerSchemeGuid}");
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
                }

                log.Add("OK • Попередній план живлення");
            }
            catch (Exception exception)
            {
                log.Add($"ПОМИЛКА • План живлення: {exception.Message}");
            }
        }

        await File.WriteAllLinesAsync(Path.Combine(folder, "restore.log"), log);
        return (folder, log);
    }

    private static void RestoreRegistryValue(RegistryValueState state)
    {
        if (!Enum.TryParse<RegistryHive>(state.Hive, out var hive))
        {
            throw new InvalidOperationException($"Невідомий розділ реєстру: {state.Hive}");
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

        if (!state.Existed)
        {
            using var existingKey = baseKey.OpenSubKey(state.Path, writable: true);
            existingKey?.DeleteValue(state.Name, throwOnMissingValue: false);
            return;
        }

        using var key = baseKey.CreateSubKey(state.Path, writable: true)
            ?? throw new InvalidOperationException(state.Path);

        if (!Enum.TryParse<RegistryValueKind>(state.Kind, out var kind))
        {
            kind = RegistryValueKind.String;
        }

        object value = kind switch
        {
            RegistryValueKind.DWord => int.Parse(state.Value ?? "0", CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(state.Value ?? "0", CultureInfo.InvariantCulture),
            RegistryValueKind.String or RegistryValueKind.ExpandString => state.Value ?? string.Empty,
            _ => state.Value ?? string.Empty
        };

        key.SetValue(state.Name, value, kind);
    }
}
