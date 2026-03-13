using System.ComponentModel;
using System.Globalization;
using Example.EnergyAnalyticsMcp.Data;
using Example.EnergyAnalyticsMcp.Models;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Example.EnergyAnalyticsMcp.Tools;

[McpServerToolType]
public class GetMetricsTool
{
    // ── Allowed parameter values ──
    private static readonly HashSet<string> AllowedGrains = ["day", "week", "month"];

    private static readonly HashSet<string> AllowedMetrics =
        ["average_mw", "min_mw", "max_mw", "min_mw_time", "max_mw_time", "load_factor"];

    // Max window per grain to keep results chat-friendly
    private static readonly Dictionary<string, int> MaxWindowDays = new()
    {
        ["day"] = 366,
        ["week"] = 730,
        ["month"] = 3650
    };

    private const int DefaultTopN = 1000;
    private const string MetricUnit = "MW";

    private readonly EnergyDbContext _db;

    public GetMetricsTool(EnergyDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves energy metrics from the appropriate rollup table (daily, weekly, or monthly)
    /// for a validated date range and set of metric names. Returns a structured JSON response
    /// containing metadata, a time-series payload, and data-quality flags.
    /// This tool does not accept raw SQL. It maps the requested grain to the correct
    /// pre-aggregated rollup table and executes parameterized queries only.
    /// Call RouteQuestion first to obtain the grain, metrics, and date range.
    /// </summary>
    [McpServerTool(Name = "GetMetrics")]
    [Description(
        "Retrieves energy metrics from pre-aggregated rollup tables. " +
        "Accepts: grain (day|week|month), a comma-separated list of metric names " +
        "(allowed: average_mw, min_mw, max_mw, min_mw_time, max_mw_time, load_factor), " +
        "start_date and end_date in yyyy-MM-dd format, and an optional topN limit. " +
        "Returns a JSON object with: metadata (grain, metric_names, unit, date range, point_count), " +
        "a series array of {period_start, period_end, values}, and data quality flags " +
        "(is_complete, expected_points, actual_points, has_gaps). " +
        "The tool selects the correct rollup table internally — callers never specify table names. " +
        "Call RouteQuestion first to determine the correct parameters for this tool.")]
    public async Task<GetMetricsOutput> Execute(
        [Description("Time grain that selects the rollup table: day, week, or month")]
        string grain,

        [Description("Comma-separated metric names to return. Allowed values: average_mw, min_mw, max_mw, min_mw_time, max_mw_time, load_factor")]
        string metrics,

        [Description("Inclusive start date in ISO format (yyyy-MM-dd)")]
        string startDate,

        [Description("Inclusive end date in ISO format (yyyy-MM-dd)")]
        string endDate,

        [Description("Optional maximum number of data points to return. Defaults to 1000. Use to keep responses concise for large ranges.")]
        int? topN = null)
    {
        // ── 1. Validation & normalization ──
        grain = grain.Trim().ToLowerInvariant();
        if (!AllowedGrains.Contains(grain))
            return Error($"Invalid grain '{grain}'. Allowed: day, week, month.");

        var requestedMetrics = metrics
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToLowerInvariant())
            .Distinct()
            .ToList();

        var invalidMetrics = requestedMetrics.Where(m => !AllowedMetrics.Contains(m)).ToList();
        if (invalidMetrics.Count > 0)
            return Error($"Invalid metric(s): {string.Join(", ", invalidMetrics)}. Allowed: {string.Join(", ", AllowedMetrics)}.");

        if (requestedMetrics.Count == 0)
            return Error("At least one metric must be specified.");

        if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return Error($"Invalid start_date '{startDate}'. Expected format: yyyy-MM-dd.");

        if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            return Error($"Invalid end_date '{endDate}'. Expected format: yyyy-MM-dd.");

        if (start > end)
            return Error($"start_date ({startDate}) must be <= end_date ({endDate}).");

        var windowDays = (end - start).TotalDays;
        var maxWindow = MaxWindowDays[grain];
        if (windowDays > maxWindow)
            return Error($"Date range of {windowDays:F0} days exceeds the maximum of {maxWindow} days for grain '{grain}'.");

        var limit = topN ?? DefaultTopN;
        if (limit < 1) limit = 1;
        if (limit > DefaultTopN) limit = DefaultTopN;

        // ── 2. Rollup selection & query ──
        var (series, toolNote) = grain switch
        {
            "day" => await QueryDailyAsync(start, end, requestedMetrics, limit),
            "week" => await QueryWeeklyAsync(start, end, requestedMetrics, limit),
            "month" => await QueryMonthlyAsync(start, end, requestedMetrics, limit),
            _ => (new List<MetricDataPoint>(), "")
        };

        // ── 3. Data-quality estimation ──
        var expectedPoints = EstimateExpectedPoints(grain, start, end);

        // ── 4. Output shaping ──
        return new GetMetricsOutput
        {
            Grain = grain,
            MetricNames = requestedMetrics,
            Unit = MetricUnit,
            StartDate = start.ToString("yyyy-MM-dd"),
            EndDate = end.ToString("yyyy-MM-dd"),
            PointCount = series.Count,
            Series = series,
            Quality = new DataQuality
            {
                IsComplete = series.Count >= expectedPoints,
                ExpectedPoints = expectedPoints,
                ActualPoints = series.Count
            },
            ToolNote = series.Count == 0
                ? $"No data found for grain '{grain}' between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}."
                : toolNote
        };
    }

    // ── Daily rollup query ──
    private async Task<(List<MetricDataPoint> Series, string ToolNote)> QueryDailyAsync(
        DateTime start, DateTime end, List<string> requestedMetrics, int limit)
    {
        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        var rows = await _db.DailyData
            .AsNoTracking()
            .Where(d => d.Day >= startDate && d.Day <= endDate)
            .OrderBy(d => d.Day)
            .Take(limit)
            .ToListAsync();

        var series = rows.Select(r =>
        {
            var point = new MetricDataPoint
            {
                PeriodStart = r.Day.ToString("yyyy-MM-dd")
            };
            ProjectMetrics(point, requestedMetrics, r.AverageMw, r.MinMw, r.MaxMw,
                r.MinMwTime, r.MaxMwTime, r.LoadFactor);
            return point;
        }).ToList();

        var note = series.Count == limit
            ? $"Results capped at {limit} points. Narrow the date range or increase topN for more."
            : $"Returned {series.Count} daily data points.";

        return (series, note);
    }

    // ── Weekly rollup query ──
    private async Task<(List<MetricDataPoint> Series, string ToolNote)> QueryWeeklyAsync(
        DateTime start, DateTime end, List<string> requestedMetrics, int limit)
    {
        var rows = await _db.WeeklyData
            .AsNoTracking()
            .Where(w => w.WeekStart >= start && w.WeekStart <= end)
            .OrderBy(w => w.WeekStart)
            .Take(limit)
            .ToListAsync();

        var series = rows.Select(r =>
        {
            var point = new MetricDataPoint
            {
                PeriodStart = r.WeekStart.ToString("yyyy-MM-dd"),
                PeriodEnd = r.WeekEnd.ToString("yyyy-MM-dd")
            };
            ProjectMetrics(point, requestedMetrics, r.AverageMw, r.MinMw, r.MaxMw,
                r.MinMwTime, r.MaxMwTime, r.LoadFactor);
            return point;
        }).ToList();

        var note = series.Count == limit
            ? $"Results capped at {limit} points. Narrow the date range or increase topN for more."
            : $"Returned {series.Count} weekly data points.";

        return (series, note);
    }

    // ── Monthly rollup query ──
    private async Task<(List<MetricDataPoint> Series, string ToolNote)> QueryMonthlyAsync(
        DateTime start, DateTime end, List<string> requestedMetrics, int limit)
    {
        var rows = await _db.MonthlyData
            .AsNoTracking()
            .Where(m => m.MonthStart >= start && m.MonthStart <= end)
            .OrderBy(m => m.MonthStart)
            .Take(limit)
            .ToListAsync();

        var series = rows.Select(r =>
        {
            var point = new MetricDataPoint
            {
                PeriodStart = r.MonthStart.ToString("yyyy-MM-dd"),
                PeriodEnd = r.MonthEnd.ToString("yyyy-MM-dd")
            };
            ProjectMetrics(point, requestedMetrics, r.AverageMw, r.MinMw, r.MaxMw,
                r.MinMwTime, r.MaxMwTime, r.LoadFactor);
            return point;
        }).ToList();

        var note = series.Count == limit
            ? $"Results capped at {limit} points. Narrow the date range or increase topN for more."
            : $"Returned {series.Count} monthly data points.";

        return (series, note);
    }

    // ── Project only the requested metrics into the data point ──
    private static void ProjectMetrics(
        MetricDataPoint point, List<string> requestedMetrics,
        decimal averageMw, decimal minMw, decimal maxMw,
        DateTime minMwTime, DateTime maxMwTime, decimal loadFactor)
    {
        foreach (var metric in requestedMetrics)
        {
            point.Values[metric] = metric switch
            {
                "average_mw" => averageMw,
                "min_mw" => minMw,
                "max_mw" => maxMw,
                "min_mw_time" => minMwTime.ToString("yyyy-MM-dd HH:mm:ss"),
                "max_mw_time" => maxMwTime.ToString("yyyy-MM-dd HH:mm:ss"),
                "load_factor" => loadFactor,
                _ => null
            };
        }
    }

    // ── Estimate expected data points for quality assessment ──
    private static int EstimateExpectedPoints(string grain, DateTime start, DateTime end)
    {
        var span = (end - start).TotalDays + 1;
        return grain switch
        {
            "day" => (int)span,
            "week" => Math.Max(1, (int)Math.Ceiling(span / 7.0)),
            "month" => ((end.Year - start.Year) * 12) + end.Month - start.Month + 1,
            _ => 0
        };
    }

    // ── Build a validation-error response ──
    private static GetMetricsOutput Error(string message) => new()
    {
        Grain = "error",
        MetricNames = [],
        Unit = "",
        StartDate = "",
        EndDate = "",
        PointCount = 0,
        Series = [],
        Quality = new DataQuality { IsComplete = false, ExpectedPoints = 0, ActualPoints = 0 },
        ToolNote = message
    };
}
