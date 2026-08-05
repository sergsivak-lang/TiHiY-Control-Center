namespace TiHiY.SystemOptimizer.Models;

public sealed class BackupState
{
    public DateTime CreatedAt { get; set; }
    public string[] Items { get; set; } = Array.Empty<string>();
    public List<RegistryValueState> RegistryValues { get; set; } = new();
    public string? PowerSchemeGuid { get; set; }
}

public sealed class RegistryValueState
{
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Existed { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Value { get; set; }
}
