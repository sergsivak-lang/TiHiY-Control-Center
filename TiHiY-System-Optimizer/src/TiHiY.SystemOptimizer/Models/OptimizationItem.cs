namespace TiHiY.SystemOptimizer.Models;
public enum RecommendationLevel { Good, Recommended, Attention }
public sealed class OptimizationItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Details { get; init; }
    public required string CurrentValue { get; init; }
    public required string RecommendedValue { get; init; }
    public RecommendationLevel Level { get; init; }
    public bool Selected { get; set; }
    public bool RequiresRestart { get; init; }
}
