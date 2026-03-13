namespace Example.EnergyAnalyticsMcp.Models;

public class GetMetricsOutput
{
    // ── Metadata ──
    public required string Grain { get; set; }
    public required List<string> MetricNames { get; set; }
    public required string Unit { get; set; }
    public required string StartDate { get; set; }
    public required string EndDate { get; set; }
    public required int PointCount { get; set; }

    // ── Series payload ──
    public required List<MetricDataPoint> Series { get; set; }

    // ── Data-quality flags ──
    public required DataQuality Quality { get; set; }

    // ── Tool notes for the agent ──
    public string? ToolNote { get; set; }
}

public class MetricDataPoint
{
    public required string PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new();
}

public class DataQuality
{
    public bool IsComplete { get; set; }
    public int ExpectedPoints { get; set; }
    public int ActualPoints { get; set; }
    public bool HasGaps => ActualPoints < ExpectedPoints;
}
