namespace TiHiY.SystemOptimizer.Models;

public sealed class GpuAuditSnapshot
{
    public bool NvidiaFound { get; init; }
    public string GpuName { get; init; } = "Не визначено";
    public string DriverVersion { get; init; } = "Не визначено";
    public string DriverDate { get; init; } = "Не визначено";
    public string Vram { get; init; } = "Не визначено";
    public string Utilization { get; init; } = "Не визначено";
    public string Temperature { get; init; } = "Не визначено";
    public string MemoryUsage { get; init; } = "Не визначено";
    public string HagsState { get; init; } = "Не визначено";
    public bool NvidiaSmiAvailable { get; init; }
}
