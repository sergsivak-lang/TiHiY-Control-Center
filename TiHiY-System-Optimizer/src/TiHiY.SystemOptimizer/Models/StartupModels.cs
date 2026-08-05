namespace TiHiY.SystemOptimizer.Models;

public enum StartupItemKind
{
    Registry,
    StartupFolder
}

public enum StartupAdvice
{
    Keep,
    Optional,
    Unknown
}

public sealed class StartupItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public StartupItemKind Kind { get; init; }
    public StartupAdvice Advice { get; init; }
    public string AdviceText { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? FilePath { get; init; }
    public string? RegistryHiveName { get; init; }
    public string? RegistryViewName { get; init; }
    public string? RegistryKeyPath { get; init; }
    public string? RegistryValueName { get; init; }
    public string? RegistryValueKindName { get; init; }
    public string? StartupFilePath { get; init; }
}

public sealed class StartupAuditSnapshot
{
    public IReadOnlyList<StartupItem> Items { get; init; } = Array.Empty<StartupItem>();
    public int OptionalCount => Items.Count(item => item.Advice == StartupAdvice.Optional);
    public int KeepCount => Items.Count(item => item.Advice == StartupAdvice.Keep);
}

public sealed class StartupBackupState
{
    public DateTime CreatedAt { get; init; }
    public List<StartupBackupItem> Items { get; init; } = [];
}

public sealed class StartupBackupItem
{
    public string Name { get; init; } = string.Empty;
    public StartupItemKind Kind { get; init; }
    public string? RegistryHiveName { get; init; }
    public string? RegistryViewName { get; init; }
    public string? RegistryKeyPath { get; init; }
    public string? RegistryValueName { get; init; }
    public string? RegistryValueKindName { get; init; }
    public string? RegistryStringValue { get; init; }
    public string? OriginalFilePath { get; init; }
    public string? BackupFilePath { get; init; }
}

public sealed class StartupChangeResult
{
    public int Changed { get; init; }
    public int Failed { get; init; }
    public string BackupFolder { get; init; } = string.Empty;
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}
