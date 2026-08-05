namespace TiHiY.SystemOptimizer.Models;

public sealed class StarCitizenInstallation
{
    public string Channel { get; init; } = "Невідомий канал";
    public string Path { get; init; } = string.Empty;
    public bool UserCfgExists { get; init; }
    public int UserCfgActiveLines { get; init; }
    public string? UserCfgLanguage { get; init; }
    public DateTime? UserCfgLastWriteTime { get; init; }
    public string UserCfgPath => System.IO.Path.Combine(Path, "USER.cfg");
}
