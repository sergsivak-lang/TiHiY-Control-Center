namespace TiHiY.SystemOptimizer.Models;

public sealed class StarCitizenInstallation
{
    public string Channel { get; init; } = "Невідомий канал";
    public string Path { get; init; } = string.Empty;
    public bool UserCfgExists { get; init; }
    public string UserCfgPath => System.IO.Path.Combine(Path, "USER.cfg");
}
