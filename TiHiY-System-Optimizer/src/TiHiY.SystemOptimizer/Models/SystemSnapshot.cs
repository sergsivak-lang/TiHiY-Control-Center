namespace TiHiY.SystemOptimizer.Models;

public sealed class SystemSnapshot
{
    public string Windows { get; init; } = "Невідомо";
    public string Cpu { get; init; } = "Невідомо";
    public string Gpu { get; init; } = "Невідомо";
    public string Ram { get; init; } = "Невідомо";
    public string Bios { get; init; } = "Невідомо";
    public string PowerPlan { get; init; } = "Невідомо";

    public string? RsiLauncherPath { get; init; }
    public string? ObsPath { get; init; }
    public string? DiscordPath { get; init; }
    public string? SteelSeriesPath { get; init; }
    public IReadOnlyList<StarCitizenInstallation> StarCitizenInstallations { get; init; } = Array.Empty<StarCitizenInstallation>();
    public IReadOnlyList<string> StarCitizenShaderCaches { get; init; } = Array.Empty<string>();

    public string? StarCitizenPath => StarCitizenInstallations.FirstOrDefault()?.Path;
    public bool StarCitizenFound => StarCitizenInstallations.Count > 0;
    public bool RsiLauncherFound => !string.IsNullOrWhiteSpace(RsiLauncherPath);
    public bool ObsFound => !string.IsNullOrWhiteSpace(ObsPath);
    public bool DiscordFound => !string.IsNullOrWhiteSpace(DiscordPath);
    public bool SteelSeriesFound => !string.IsNullOrWhiteSpace(SteelSeriesPath);
}
