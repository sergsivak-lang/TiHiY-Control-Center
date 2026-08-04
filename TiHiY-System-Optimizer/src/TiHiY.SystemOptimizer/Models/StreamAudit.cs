namespace TiHiY.SystemOptimizer.Models;

public sealed class StreamAudit
{
    public bool ObsConfigFound { get; init; }
    public string? ObsConfigRoot { get; init; }
    public string? ObsProfileName { get; init; }
    public string? ObsProfilePath { get; init; }
    public string? ObsCanvasResolution { get; init; }
    public string? ObsOutputResolution { get; init; }
    public string? ObsFps { get; init; }
    public string? ObsOutputMode { get; init; }
    public string? ObsStreamEncoder { get; init; }
    public string? ObsAudioSampleRate { get; init; }

    public bool ObsRunning { get; init; }
    public bool DiscordRunning { get; init; }
    public bool SteelSeriesRunning { get; init; }
    public bool SonarRunning { get; init; }
}
