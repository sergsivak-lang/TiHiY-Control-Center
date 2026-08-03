namespace TiHiY.SystemOptimizer.Models;
public sealed class SystemSnapshot
{
    public string Windows { get; init; } = "Невідомо";
    public string Cpu { get; init; } = "Невідомо";
    public string Gpu { get; init; } = "Невідомо";
    public string Ram { get; init; } = "Невідомо";
    public string Bios { get; init; } = "Невідомо";
    public string PowerPlan { get; init; } = "Невідомо";
}
