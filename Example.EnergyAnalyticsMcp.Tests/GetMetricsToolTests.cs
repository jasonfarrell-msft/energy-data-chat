using Example.EnergyAnalyticsMcp.Data;
using Example.EnergyAnalyticsMcp.Entities;
using Example.EnergyAnalyticsMcp.Tools;
using Microsoft.EntityFrameworkCore;

namespace Example.EnergyAnalyticsMcp.Tests;

public class GetMetricsToolTests : IDisposable
{
    private readonly EnergyDbContext _context;
    private readonly GetMetricsTool _tool;

    public GetMetricsToolTests()
    {
        var options = new DbContextOptionsBuilder<EnergyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EnergyDbContext(options);
        _tool = new GetMetricsTool(_context);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        // Seed daily data for January 2025
        _context.DailyData.AddRange(
            new EnergyDataDaily
            {
                Day = new DateOnly(2025, 1, 1),
                AverageMw = 100.5m,
                MinMw = 80.0m,
                MaxMw = 120.0m,
                MinMwTime = new DateTime(2025, 1, 1, 3, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 1, 14, 0, 0),
                LoadFactor = 0.837m
            },
            new EnergyDataDaily
            {
                Day = new DateOnly(2025, 1, 2),
                AverageMw = 105.2m,
                MinMw = 85.0m,
                MaxMw = 125.0m,
                MinMwTime = new DateTime(2025, 1, 2, 4, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 2, 15, 0, 0),
                LoadFactor = 0.842m
            },
            new EnergyDataDaily
            {
                Day = new DateOnly(2025, 1, 3),
                AverageMw = 110.0m,
                MinMw = 90.0m,
                MaxMw = 130.0m,
                MinMwTime = new DateTime(2025, 1, 3, 2, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 3, 16, 0, 0),
                LoadFactor = 0.846m
            }
        );

        // Seed weekly data
        _context.WeeklyData.AddRange(
            new EnergyDataWeekly
            {
                WeekStart = new DateTime(2025, 1, 6),
                WeekEnd = new DateTime(2025, 1, 12),
                AverageMw = 102.5m,
                MinMw = 78.0m,
                MaxMw = 128.0m,
                MinMwTime = new DateTime(2025, 1, 7, 3, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 10, 14, 0, 0),
                LoadFactor = 0.801m
            },
            new EnergyDataWeekly
            {
                WeekStart = new DateTime(2025, 1, 13),
                WeekEnd = new DateTime(2025, 1, 19),
                AverageMw = 108.0m,
                MinMw = 82.0m,
                MaxMw = 132.0m,
                MinMwTime = new DateTime(2025, 1, 14, 4, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 17, 15, 0, 0),
                LoadFactor = 0.818m
            }
        );

        // Seed monthly data
        _context.MonthlyData.AddRange(
            new EnergyDataMonthly
            {
                MonthStart = new DateTime(2025, 1, 1),
                MonthEnd = new DateTime(2025, 1, 31),
                AverageMw = 105.0m,
                MinMw = 75.0m,
                MaxMw = 135.0m,
                MinMwTime = new DateTime(2025, 1, 5, 3, 0, 0),
                MaxMwTime = new DateTime(2025, 1, 20, 14, 0, 0),
                LoadFactor = 0.778m
            },
            new EnergyDataMonthly
            {
                MonthStart = new DateTime(2025, 2, 1),
                MonthEnd = new DateTime(2025, 2, 28),
                AverageMw = 110.0m,
                MinMw = 80.0m,
                MaxMw = 140.0m,
                MinMwTime = new DateTime(2025, 2, 8, 4, 0, 0),
                MaxMwTime = new DateTime(2025, 2, 15, 15, 0, 0),
                LoadFactor = 0.786m
            }
        );

        _context.SaveChanges();
    }

    #region Validation Tests

    [Theory]
    [InlineData("invalid", "average_mw", "2025-01-01", "2025-01-31")]
    [InlineData("daily", "average_mw", "2025-01-01", "2025-01-31")]
    [InlineData("Day", "average_mw", "2025-01-01", "2025-01-31")]
    [InlineData("MONTH", "average_mw", "2025-01-01", "2025-01-31")]
    public async Task Execute_InvalidGrain_ReturnsError(string grain, string metrics, string start, string end)
    {
        var result = await _tool.Execute(grain, metrics, start, end);

        if (grain.ToLowerInvariant() is "day" or "week" or "month")
        {
            Assert.NotEqual("error", result.Grain);
        }
        else
        {
            Assert.Equal("error", result.Grain);
            Assert.Contains("Invalid grain", result.ToolNote);
        }
    }

    [Theory]
    [InlineData("day", "invalid_metric", "2025-01-01", "2025-01-31")]
    [InlineData("day", "average_mw,invalid", "2025-01-01", "2025-01-31")]
    [InlineData("day", "bad_metric,another_bad", "2025-01-01", "2025-01-31")]
    public async Task Execute_InvalidMetrics_ReturnsError(string grain, string metrics, string start, string end)
    {
        var result = await _tool.Execute(grain, metrics, start, end);

        Assert.Equal("error", result.Grain);
        Assert.Contains("Invalid metric", result.ToolNote);
    }

    [Theory]
    [InlineData("day", "", "2025-01-01", "2025-01-31")]
    [InlineData("day", "  ", "2025-01-01", "2025-01-31")]
    [InlineData("day", ",,,", "2025-01-01", "2025-01-31")]
    public async Task Execute_EmptyMetrics_ReturnsError(string grain, string metrics, string start, string end)
    {
        var result = await _tool.Execute(grain, metrics, start, end);

        Assert.Equal("error", result.Grain);
        Assert.Contains("At least one metric must be specified", result.ToolNote);
    }

    [Theory]
    [InlineData("day", "average_mw", "invalid-date", "2025-01-31")]
    [InlineData("day", "average_mw", "2025-01-01", "invalid-date")]
    [InlineData("day", "average_mw", "01/01/2025", "01/31/2025")]
    [InlineData("day", "average_mw", "2025/01/01", "2025/01/31")]
    public async Task Execute_InvalidDateFormat_ReturnsError(string grain, string metrics, string start, string end)
    {
        var result = await _tool.Execute(grain, metrics, start, end);

        Assert.Equal("error", result.Grain);
        Assert.Contains("Invalid", result.ToolNote);
    }

    [Fact]
    public async Task Execute_StartDateAfterEndDate_ReturnsError()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-31", "2025-01-01");

        Assert.Equal("error", result.Grain);
        Assert.Contains("start_date", result.ToolNote);
        Assert.Contains("end_date", result.ToolNote);
    }

    [Fact]
    public async Task Execute_DayGrainExceedsMaxWindow_ReturnsError()
    {
        var result = await _tool.Execute("day", "average_mw", "2020-01-01", "2025-01-01");

        Assert.Equal("error", result.Grain);
        Assert.Contains("exceeds the maximum", result.ToolNote);
    }

    [Fact]
    public async Task Execute_WeekGrainExceedsMaxWindow_ReturnsError()
    {
        var result = await _tool.Execute("week", "average_mw", "2020-01-01", "2025-01-01");

        Assert.Equal("error", result.Grain);
        Assert.Contains("exceeds the maximum", result.ToolNote);
    }

    [Fact]
    public async Task Execute_ValidMetrics_PassesValidation()
    {
        var result = await _tool.Execute("day", "average_mw,min_mw,max_mw,load_factor,min_mw_time,max_mw_time",
            "2025-01-01", "2025-01-03");

        Assert.NotEqual("error", result.Grain);
        Assert.Equal(6, result.MetricNames.Count);
    }

    #endregion

    #region Daily Grain Tests

    [Fact]
    public async Task Execute_DailyGrain_ReturnsDailyData()
    {
        var result = await _tool.Execute("day", "average_mw,min_mw,max_mw", "2025-01-01", "2025-01-03");

        Assert.Equal("day", result.Grain);
        Assert.Equal(3, result.Series.Count);
        Assert.Equal("2025-01-01", result.Series[0].PeriodStart);
        Assert.Equal("2025-01-02", result.Series[1].PeriodStart);
        Assert.Equal("2025-01-03", result.Series[2].PeriodStart);
    }

    [Fact]
    public async Task Execute_DailyGrain_ProjectsRequestedMetricsOnly()
    {
        var result = await _tool.Execute("day", "average_mw,max_mw", "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        Assert.Equal(2, result.Series[0].Values.Count);
        Assert.True(result.Series[0].Values.ContainsKey("average_mw"));
        Assert.True(result.Series[0].Values.ContainsKey("max_mw"));
        Assert.False(result.Series[0].Values.ContainsKey("min_mw"));
    }

    [Fact]
    public async Task Execute_DailyGrain_ReturnsCorrectValues()
    {
        var result = await _tool.Execute("day", "average_mw,min_mw,max_mw,load_factor",
            "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        var point = result.Series[0];
        Assert.Equal(100.5m, point.Values["average_mw"]);
        Assert.Equal(80.0m, point.Values["min_mw"]);
        Assert.Equal(120.0m, point.Values["max_mw"]);
        Assert.Equal(0.837m, point.Values["load_factor"]);
    }

    [Fact]
    public async Task Execute_DailyGrain_ReturnsTimestampMetrics()
    {
        var result = await _tool.Execute("day", "min_mw_time,max_mw_time", "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        var point = result.Series[0];
        Assert.Contains("2025-01-01 03:00:00", point.Values["min_mw_time"]?.ToString());
        Assert.Contains("2025-01-01 14:00:00", point.Values["max_mw_time"]?.ToString());
    }

    [Fact]
    public async Task Execute_DailyGrain_NoDataInRange_ReturnsEmptySeries()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-12-01", "2025-12-31");

        Assert.Empty(result.Series);
        Assert.Equal(0, result.PointCount);
        Assert.Contains("No data found", result.ToolNote);
    }

    [Fact]
    public async Task Execute_DailyGrain_RespectsDailyPeriodEnd()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        Assert.Null(result.Series[0].PeriodEnd);
    }

    #endregion

    #region Weekly Grain Tests

    [Fact]
    public async Task Execute_WeeklyGrain_ReturnsWeeklyData()
    {
        var result = await _tool.Execute("week", "average_mw,min_mw,max_mw", "2025-01-01", "2025-01-31");

        Assert.Equal("week", result.Grain);
        Assert.Equal(2, result.Series.Count);
    }

    [Fact]
    public async Task Execute_WeeklyGrain_IncludesPeriodEnd()
    {
        var result = await _tool.Execute("week", "average_mw", "2025-01-01", "2025-01-31");

        Assert.NotNull(result.Series[0].PeriodEnd);
        Assert.Equal("2025-01-12", result.Series[0].PeriodEnd);
    }

    [Fact]
    public async Task Execute_WeeklyGrain_ReturnsCorrectValues()
    {
        var result = await _tool.Execute("week", "average_mw,load_factor", "2025-01-06", "2025-01-12");

        Assert.Single(result.Series);
        Assert.Equal(102.5m, result.Series[0].Values["average_mw"]);
        Assert.Equal(0.801m, result.Series[0].Values["load_factor"]);
    }

    #endregion

    #region Monthly Grain Tests

    [Fact]
    public async Task Execute_MonthlyGrain_ReturnsMonthlyData()
    {
        var result = await _tool.Execute("month", "average_mw,min_mw,max_mw", "2025-01-01", "2025-02-28");

        Assert.Equal("month", result.Grain);
        Assert.Equal(2, result.Series.Count);
    }

    [Fact]
    public async Task Execute_MonthlyGrain_IncludesPeriodEnd()
    {
        var result = await _tool.Execute("month", "average_mw", "2025-01-01", "2025-02-28");

        Assert.NotNull(result.Series[0].PeriodEnd);
        Assert.Equal("2025-01-31", result.Series[0].PeriodEnd);
        Assert.Equal("2025-02-28", result.Series[1].PeriodEnd);
    }

    [Fact]
    public async Task Execute_MonthlyGrain_ReturnsCorrectValues()
    {
        var result = await _tool.Execute("month", "average_mw,min_mw,max_mw,load_factor",
            "2025-01-01", "2025-01-31");

        Assert.Single(result.Series);
        var point = result.Series[0];
        Assert.Equal(105.0m, point.Values["average_mw"]);
        Assert.Equal(75.0m, point.Values["min_mw"]);
        Assert.Equal(135.0m, point.Values["max_mw"]);
        Assert.Equal(0.778m, point.Values["load_factor"]);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task Execute_ReturnsCorrectMetadata()
    {
        var result = await _tool.Execute("day", "average_mw,max_mw", "2025-01-01", "2025-01-03");

        Assert.Equal("day", result.Grain);
        Assert.Equal("MW", result.Unit);
        Assert.Equal("2025-01-01", result.StartDate);
        Assert.Equal("2025-01-03", result.EndDate);
        Assert.Equal(3, result.PointCount);
        Assert.Equal(2, result.MetricNames.Count);
        Assert.Contains("average_mw", result.MetricNames);
        Assert.Contains("max_mw", result.MetricNames);
    }

    #endregion

    #region Data Quality Tests

    [Fact]
    public async Task Execute_CompleteData_MarksQualityAsComplete()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03");

        Assert.True(result.Quality.IsComplete);
        Assert.Equal(3, result.Quality.ExpectedPoints);
        Assert.Equal(3, result.Quality.ActualPoints);
        Assert.False(result.Quality.HasGaps);
    }

    [Fact]
    public async Task Execute_IncompleteData_MarksQualityAsIncomplete()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-10");

        Assert.False(result.Quality.IsComplete);
        Assert.Equal(10, result.Quality.ExpectedPoints);
        Assert.Equal(3, result.Quality.ActualPoints);
        Assert.True(result.Quality.HasGaps);
    }

    [Fact]
    public async Task Execute_WeeklyGrain_CalculatesExpectedPointsCorrectly()
    {
        var result = await _tool.Execute("week", "average_mw", "2025-01-01", "2025-01-31");

        Assert.Equal(5, result.Quality.ExpectedPoints); // ~31 days / 7 = 5 weeks
    }

    [Fact]
    public async Task Execute_MonthlyGrain_CalculatesExpectedPointsCorrectly()
    {
        var result = await _tool.Execute("month", "average_mw", "2025-01-01", "2025-02-28");

        Assert.Equal(2, result.Quality.ExpectedPoints);
    }

    #endregion

    #region TopN Limit Tests

    [Fact]
    public async Task Execute_WithTopN_LimitsResults()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03", topN: 2);

        Assert.Equal(2, result.Series.Count);
        Assert.Contains("capped at 2 points", result.ToolNote);
    }

    [Fact]
    public async Task Execute_TopNZero_UsesMinimumOfOne()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03", topN: 0);

        Assert.Single(result.Series);
    }

    [Fact]
    public async Task Execute_TopNNegative_UsesMinimumOfOne()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03", topN: -5);

        Assert.Single(result.Series);
    }

    [Fact]
    public async Task Execute_TopNExceedsMaximum_CapsAtMaximum()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03", topN: 9999);

        Assert.Equal(3, result.Series.Count);
    }

    [Fact]
    public async Task Execute_NoTopN_UsesDefault()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03");

        Assert.Equal(3, result.Series.Count);
    }

    #endregion

    #region Metric Normalization Tests

    [Fact]
    public async Task Execute_MetricsWithWhitespace_NormalizesCorrectly()
    {
        var result = await _tool.Execute("day", " average_mw , max_mw , min_mw ", "2025-01-01", "2025-01-01");

        Assert.Equal(3, result.MetricNames.Count);
        Assert.Contains("average_mw", result.MetricNames);
        Assert.Contains("max_mw", result.MetricNames);
        Assert.Contains("min_mw", result.MetricNames);
    }

    [Fact]
    public async Task Execute_DuplicateMetrics_RemovesDuplicates()
    {
        var result = await _tool.Execute("day", "average_mw,average_mw,max_mw", "2025-01-01", "2025-01-01");

        Assert.Equal(2, result.MetricNames.Count);
        Assert.Contains("average_mw", result.MetricNames);
        Assert.Contains("max_mw", result.MetricNames);
    }

    [Fact]
    public async Task Execute_MixedCaseMetrics_NormalizesToLowerCase()
    {
        var result = await _tool.Execute("day", "Average_MW,MAX_MW", "2025-01-01", "2025-01-01");

        Assert.Equal(2, result.MetricNames.Count);
        Assert.Contains("average_mw", result.MetricNames);
        Assert.Contains("max_mw", result.MetricNames);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public async Task Execute_DailyData_OrdersByDayAscending()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-03");

        Assert.Equal("2025-01-01", result.Series[0].PeriodStart);
        Assert.Equal("2025-01-02", result.Series[1].PeriodStart);
        Assert.Equal("2025-01-03", result.Series[2].PeriodStart);
    }

    [Fact]
    public async Task Execute_WeeklyData_OrdersByWeekStartAscending()
    {
        var result = await _tool.Execute("week", "average_mw", "2025-01-01", "2025-01-31");

        Assert.Equal("2025-01-06", result.Series[0].PeriodStart);
        Assert.Equal("2025-01-13", result.Series[1].PeriodStart);
    }

    [Fact]
    public async Task Execute_MonthlyData_OrdersByMonthStartAscending()
    {
        var result = await _tool.Execute("month", "average_mw", "2025-01-01", "2025-02-28");

        Assert.Equal("2025-01-01", result.Series[0].PeriodStart);
        Assert.Equal("2025-02-01", result.Series[1].PeriodStart);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Execute_SingleDayRange_ReturnsOnePoint()
    {
        var result = await _tool.Execute("day", "average_mw", "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        Assert.Equal("2025-01-01", result.Series[0].PeriodStart);
    }

    [Fact]
    public async Task Execute_AllMetrics_ReturnsAllRequestedValues()
    {
        var result = await _tool.Execute("day",
            "average_mw,min_mw,max_mw,min_mw_time,max_mw_time,load_factor",
            "2025-01-01", "2025-01-01");

        Assert.Single(result.Series);
        Assert.Equal(6, result.Series[0].Values.Count);
        Assert.True(result.Series[0].Values.ContainsKey("average_mw"));
        Assert.True(result.Series[0].Values.ContainsKey("min_mw"));
        Assert.True(result.Series[0].Values.ContainsKey("max_mw"));
        Assert.True(result.Series[0].Values.ContainsKey("min_mw_time"));
        Assert.True(result.Series[0].Values.ContainsKey("max_mw_time"));
        Assert.True(result.Series[0].Values.ContainsKey("load_factor"));
    }

    #endregion
}
