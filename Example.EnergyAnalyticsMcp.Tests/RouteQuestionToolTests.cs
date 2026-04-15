using Example.EnergyAnalyticsMcp.Tools;
using System.Text.Json;

namespace Example.EnergyAnalyticsMcp.Tests;

public class RouteQuestionToolTests
{
    private readonly RouteQuestionTool _tool;

    public RouteQuestionToolTests()
    {
        _tool = new RouteQuestionTool();
    }

    #region Intent Classification Tests

    [Theory]
    [InlineData("Show me min and max load for last month", "min_max_mw_trend")]
    [InlineData("What were the minimum and maximum values?", "min_max_mw_trend")]
    [InlineData("Display min/max MW trends", "min_max_mw_trend")]
    [InlineData("Show min & max power", "min_max_mw_trend")]
    public async Task Execute_MinMaxKeywords_ReturnsMinMaxMwTrendIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
    }

    [Theory]
    [InlineData("What was the peak demand last week?", "peak_demand")]
    [InlineData("Show me the highest load", "peak_demand")]
    [InlineData("Maximum demand for January", "peak_demand")]
    [InlineData("When was the peak load?", "peak_demand")]
    [InlineData("Max load yesterday", "peak_demand")]
    public async Task Execute_PeakDemandKeywords_ReturnsPeakDemandIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("max_mw", result.Metrics);
        Assert.Contains("max_mw_time", result.Metrics);
    }

    [Theory]
    [InlineData("What was the lowest load last month?", "min_demand")]
    [InlineData("Show minimum demand", "min_demand")]
    [InlineData("When was the min load?", "min_demand")]
    [InlineData("Minimum demand for February", "min_demand")]
    public async Task Execute_MinDemandKeywords_ReturnsMinDemandIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("min_mw_time", result.Metrics);
    }

    [Theory]
    [InlineData("What was the average load last month?", "avg_load")]
    [InlineData("Show me avg demand", "avg_load")]
    [InlineData("Mean load for January", "avg_load")]
    [InlineData("Average MW consumption", "avg_load")]
    [InlineData("What is the average demand?", "avg_load")]
    public async Task Execute_AvgLoadKeywords_ReturnsAvgLoadIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("average_mw", result.Metrics);
    }

    [Theory]
    [InlineData("What is the load factor?", "load_factor")]
    [InlineData("Show capacity factor for last month", "load_factor")]
    [InlineData("What was the utilization?", "load_factor")]
    public async Task Execute_LoadFactorKeywords_ReturnsLoadFactorIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("load_factor", result.Metrics);
    }

    [Theory]
    [InlineData("How did the load change last month?", "general_trend")]
    [InlineData("Show me the trend over time", "general_trend")]
    [InlineData("How has demand changed?", "general_trend")]
    public async Task Execute_TrendKeywords_ReturnsGeneralTrendIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("average_mw", result.Metrics);
        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
    }

    [Theory]
    [InlineData("Give me a summary", "summary")]
    [InlineData("Provide an overview of last month", "summary")]
    [InlineData("Summarize the data", "summary")]
    [InlineData("Show me a report", "summary")]
    public async Task Execute_SummaryKeywords_ReturnsSummaryIntent(string question, string expectedIntent)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Contains("average_mw", result.Metrics);
        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
        Assert.Contains("load_factor", result.Metrics);
    }

    [Fact]
    public async Task Execute_NoMatchingKeywords_ReturnsGeneralTrendFallback()
    {
        var result = await _tool.Execute("Tell me about the data");

        Assert.Equal("general_trend", result.Intent);
        Assert.Contains("average_mw", result.Metrics);
        Assert.Contains("defaulting to general_trend", result.Reason);
    }

    #endregion

    #region Date Range Parsing Tests

    [Fact]
    public async Task Execute_ExplicitDateRange_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data from 2025-01-15 to 2025-02-15");

        Assert.Equal("2025-01-15", result.StartDate);
        Assert.Equal("2025-02-15", result.EndDate);
        Assert.Contains("Explicit date range parsed", result.Reason);
    }

    [Fact]
    public async Task Execute_LastMonth_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for last month");

        var today = DateTime.UtcNow.Date;
        var expectedStart = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var expectedEnd = expectedStart.AddMonths(1).AddDays(-1);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(expectedEnd.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains("'last month'", result.Reason);
    }

    [Fact]
    public async Task Execute_LastWeek_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for last week");

        Assert.Contains("'last week'", result.Reason);
        var start = DateTime.Parse(result.StartDate);
        var end = DateTime.Parse(result.EndDate);
        Assert.Equal(6, (end - start).Days); // 7 days inclusive
    }

    [Fact]
    public async Task Execute_LastYear_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for last year");

        var today = DateTime.UtcNow.Date;
        var expectedStart = new DateTime(today.Year - 1, 1, 1);
        var expectedEnd = new DateTime(today.Year - 1, 12, 31);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(expectedEnd.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains("'last year'", result.Reason);
    }

    [Fact]
    public async Task Execute_ThisMonth_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for this month");

        var today = DateTime.UtcNow.Date;
        var expectedStart = new DateTime(today.Year, today.Month, 1);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(today.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains("'this month'", result.Reason);
    }

    [Fact]
    public async Task Execute_ThisWeek_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for this week");

        Assert.Contains("'this week'", result.Reason);
        var end = DateTime.Parse(result.EndDate);
        Assert.Equal(DateTime.UtcNow.Date, end);
    }

    [Fact]
    public async Task Execute_Today_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for today");

        var today = DateTime.UtcNow.Date;
        Assert.Equal(today.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(today.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains("'today'", result.Reason);
    }

    [Fact]
    public async Task Execute_Yesterday_ParsesCorrectly()
    {
        var result = await _tool.Execute("Show data for yesterday");

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        Assert.Equal(yesterday.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(yesterday.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains("'yesterday'", result.Reason);
    }

    [Theory]
    [InlineData("last 7 days", 7)]
    [InlineData("last 30 days", 30)]
    [InlineData("last 90 days", 90)]
    public async Task Execute_LastNDays_ParsesCorrectly(string phrase, int days)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        var today = DateTime.UtcNow.Date;
        var expectedStart = today.AddDays(-days);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(today.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains($"last {days} days", result.Reason);
    }

    [Theory]
    [InlineData("last 2 weeks", 2)]
    [InlineData("last 4 weeks", 4)]
    public async Task Execute_LastNWeeks_ParsesCorrectly(string phrase, int weeks)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        var today = DateTime.UtcNow.Date;
        var expectedStart = today.AddDays(-weeks * 7);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Equal(today.ToString("yyyy-MM-dd"), result.EndDate);
        Assert.Contains($"last {weeks} weeks", result.Reason);
    }

    [Theory]
    [InlineData("last 3 months", 3)]
    [InlineData("last 6 months", 6)]
    public async Task Execute_LastNMonths_ParsesCorrectly(string phrase, int months)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        var today = DateTime.UtcNow.Date;
        var monthStart = today.AddMonths(-months);
        var expectedStart = new DateTime(monthStart.Year, monthStart.Month, 1);

        Assert.Equal(expectedStart.ToString("yyyy-MM-dd"), result.StartDate);
        Assert.Contains($"last {months} months", result.Reason);
    }

    [Theory]
    [InlineData("January 2025", "2025-01-01", "2025-01-31")]
    [InlineData("February 2025", "2025-02-01", "2025-02-28")]
    [InlineData("March 2025", "2025-03-01", "2025-03-31")]
    [InlineData("Jan 2025", "2025-01-01", "2025-01-31")]
    [InlineData("Feb 2025", "2025-02-01", "2025-02-28")]
    public async Task Execute_NamedMonthYear_ParsesCorrectly(string phrase, string expectedStart, string expectedEnd)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        Assert.Equal(expectedStart, result.StartDate);
        Assert.Equal(expectedEnd, result.EndDate);
        Assert.Contains("Named month", result.Reason);
    }

    [Theory]
    [InlineData("in 2024", "2024")]
    [InlineData("for 2025", "2025")]
    [InlineData("during 2024", "2024")]
    public async Task Execute_YearOnly_ParsesCorrectly(string phrase, string year)
    {
        var result = await _tool.Execute($"Show data {phrase}");

        Assert.StartsWith(year, result.StartDate);
        Assert.Contains($"Year '{year}'", result.Reason);
    }

    [Fact]
    public async Task Execute_NoDateSpecified_UsesDefaultRange()
    {
        var result = await _tool.Execute("Show me the data");

        Assert.Equal("2025-01-01", result.StartDate);
        Assert.Equal("2025-03-31", result.EndDate);
        Assert.Contains("defaulting to available data range", result.Reason);
    }

    [Theory]
    [InlineData("week of 2025-01-05", "2025-01-05", "2025-01-11")]
    [InlineData("week of 2025-03-10", "2025-03-10", "2025-03-16")]
    public async Task Execute_WeekOfIsoDate_ParsesCorrectly(string phrase, string expectedStart, string expectedEnd)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        Assert.Equal(expectedStart, result.StartDate);
        Assert.Equal(expectedEnd, result.EndDate);
        Assert.Contains("week of", result.Reason);
    }

    [Theory]
    [InlineData("week of January 5, 2025", "2025-01-05", "2025-01-11")]
    [InlineData("week of February 10, 2025", "2025-02-10", "2025-02-16")]
    [InlineData("week of March 1, 2025", "2025-03-01", "2025-03-07")]
    public async Task Execute_WeekOfNamedDateWithYear_ParsesCorrectly(string phrase, string expectedStart, string expectedEnd)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        Assert.Equal(expectedStart, result.StartDate);
        Assert.Equal(expectedEnd, result.EndDate);
        Assert.Contains("week of", result.Reason);
    }

    [Theory]
    [InlineData("week of Jan 5, 2025", "2025-01-05", "2025-01-11")]
    [InlineData("week of Feb 15, 2025", "2025-02-15", "2025-02-21")]
    public async Task Execute_WeekOfAbbreviatedMonthWithYear_ParsesCorrectly(string phrase, string expectedStart, string expectedEnd)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        Assert.Equal(expectedStart, result.StartDate);
        Assert.Equal(expectedEnd, result.EndDate);
        Assert.Contains("week of", result.Reason);
    }

    [Fact]
    public async Task Execute_HourlyGrainDuringWeekOf_ExplicitGrainTakesPrecedence()
    {
        // "hourly" grain should still be recognized when combined with "week of"
        var result = await _tool.Execute("Show hourly peaks during the week of January 5, 2025");

        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
        Assert.Equal("hour", result.Grain);
        Assert.Contains("Grain 'hour' explicitly mentioned", result.Reason);
    }

    #endregion

    #region Grain Resolution Tests

    [Theory]
    [InlineData("Show daily data", "day")]
    [InlineData("Show per day breakdown", "day")]
    [InlineData("Show each day", "day")]
    [InlineData("Show day-over-day", "day")]
    [InlineData("Show by day", "day")]
    public async Task Execute_ExplicitDailyGrain_ReturnsDayGrain(string question, string expectedGrain)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedGrain, result.Grain);
        Assert.Contains("'day' explicitly mentioned", result.Reason);
    }

    [Theory]
    [InlineData("Show weekly data", "week")]
    [InlineData("Show per week breakdown", "week")]
    [InlineData("Show each week", "week")]
    [InlineData("Show week-over-week", "week")]
    [InlineData("Show by week", "week")]
    public async Task Execute_ExplicitWeeklyGrain_ReturnsWeekGrain(string question, string expectedGrain)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedGrain, result.Grain);
        Assert.Contains("'week' explicitly mentioned", result.Reason);
    }

    [Theory]
    [InlineData("Show monthly data", "month")]
    [InlineData("Show per month breakdown", "month")]
    [InlineData("Show each month", "month")]
    [InlineData("Show month-over-month", "month")]
    [InlineData("Show by month", "month")]
    public async Task Execute_ExplicitMonthlyGrain_ReturnsMonthGrain(string question, string expectedGrain)
    {
        var result = await _tool.Execute(question);

        Assert.Equal(expectedGrain, result.Grain);
        Assert.Contains("'month' explicitly mentioned", result.Reason);
    }

    [Fact]
    public async Task Execute_ShortWindow_AutoSelectsDayGrain()
    {
        var result = await _tool.Execute("Show data from 2025-01-01 to 2025-01-10");

        Assert.Equal("day", result.Grain);
        Assert.Contains("auto-selected grain 'day'", result.Reason);
    }

    [Fact]
    public async Task Execute_MediumWindow_AutoSelectsWeekGrain()
    {
        var result = await _tool.Execute("Show data from 2025-01-01 to 2025-02-28");

        Assert.Equal("week", result.Grain);
        Assert.Contains("auto-selected grain 'week'", result.Reason);
    }

    [Fact]
    public async Task Execute_LongWindow_AutoSelectsMonthGrain()
    {
        var result = await _tool.Execute("Show data from 2025-01-01 to 2025-12-31");

        Assert.Equal("month", result.Grain);
        Assert.Contains("auto-selected grain 'month'", result.Reason);
    }

    [Fact]
    public async Task Execute_ExplicitGrainOverridesAutoSelection()
    {
        // Long window would normally trigger monthly, but explicit daily should override
        var result = await _tool.Execute("Show daily data from 2025-01-01 to 2025-12-31");

        Assert.Equal("day", result.Grain);
        Assert.Contains("'day' explicitly mentioned", result.Reason);
    }

    #endregion

    #region Conversation State Tests

    [Fact]
    public async Task Execute_WithConversationState_CarriesForwardDateRange()
    {
        var state = new
        {
            last_grain = "week",
            last_range = new
            {
                start = "2025-01-01",
                end = "2025-01-31"
            }
        };

        var stateJson = JsonSerializer.Serialize(state);
        var result = await _tool.Execute("Show me more details", stateJson);

        Assert.Equal("2025-01-01", result.StartDate);
        Assert.Equal("2025-01-31", result.EndDate);
        Assert.Contains("Carried forward from conversation state", result.Reason);
    }

    [Fact]
    public async Task Execute_WithConversationState_CarriesForwardGrain()
    {
        var state = new
        {
            last_grain = "month",
            last_range = new
            {
                start = "2025-01-01",
                end = "2025-12-31"
            }
        };

        var stateJson = JsonSerializer.Serialize(state);
        var result = await _tool.Execute("What about load factor?", stateJson);

        Assert.Equal("month", result.Grain);
        Assert.Contains("carried from conversation state", result.Reason);
    }

    [Fact]
    public async Task Execute_ExplicitDateOverridesConversationState()
    {
        var state = new
        {
            last_grain = "week",
            last_range = new
            {
                start = "2025-01-01",
                end = "2025-01-31"
            }
        };

        var stateJson = JsonSerializer.Serialize(state);
        var result = await _tool.Execute("Show data for February 2025", stateJson);

        Assert.Equal("2025-02-01", result.StartDate);
        Assert.Equal("2025-02-28", result.EndDate);
        Assert.DoesNotContain("conversation state", result.Reason);
    }

    [Fact]
    public async Task Execute_ExplicitGrainOverridesConversationState()
    {
        var state = new
        {
            last_grain = "month",
            last_range = new
            {
                start = "2025-01-01",
                end = "2025-12-31"
            }
        };

        var stateJson = JsonSerializer.Serialize(state);
        var result = await _tool.Execute("Show daily breakdown", stateJson);

        Assert.Equal("day", result.Grain);
        Assert.Contains("'day' explicitly mentioned", result.Reason);
    }

    [Fact]
    public async Task Execute_NullConversationState_UsesDefaults()
    {
        var result = await _tool.Execute("Show me the data", null);

        Assert.Equal("2025-01-01", result.StartDate);
        Assert.Equal("2025-03-31", result.EndDate);
    }

    [Fact]
    public async Task Execute_EmptyConversationState_UsesDefaults()
    {
        var result = await _tool.Execute("Show me the data", "");

        Assert.Equal("2025-01-01", result.StartDate);
        Assert.Equal("2025-03-31", result.EndDate);
    }

    [Fact]
    public async Task Execute_InvalidConversationStateJson_DoesNotThrow()
    {
        var result = await _tool.Execute("Show me the data", "{invalid json");

        Assert.NotNull(result);
        Assert.Equal("2025-01-01", result.StartDate);
    }

    #endregion

    #region Week Of Date Pattern Tests

    [Fact]
    public async Task Execute_WeekOfShortMonthNoYear_ParsesCorrectly()
    {
        // "week of Jan 5" — short month name, no year
        // Should default to a reasonable year (current year if date is in past, previous year if in future)
        var result = await _tool.Execute("Show data for the week of Jan 5");

        Assert.Contains("week of", result.Reason);

        var start = DateTime.Parse(result.StartDate);
        var end = DateTime.Parse(result.EndDate);

        // Week spans 7 days (6 days difference, inclusive)
        Assert.Equal(6, (end - start).Days);
        // Start should be January 5th of some year
        Assert.Equal(1, start.Month);
        Assert.Equal(5, start.Day);
    }

    [Fact]
    public async Task Execute_WeekOfFullMonthWithYear_ParsesCorrectly()
    {
        // "week of January 5, 2025" — full month name with year
        var result = await _tool.Execute("Show data for the week of January 5, 2025");

        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
        Assert.Contains("week of", result.Reason);
    }

    [Fact]
    public async Task Execute_WeekOfIsoFormat_ParsesCorrectly()
    {
        // "week of 2025-01-05" — ISO format
        var result = await _tool.Execute("Show data for the week of 2025-01-05");

        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
        Assert.Contains("week of", result.Reason);
    }

    [Fact]
    public async Task Execute_HourlyPeakDemandDuringWeekOf_CorrectGrainAndDateRange()
    {
        // "hourly peak demand during the week of Jan 5" — ensure grain is "hour" and date range is correct
        // Using "peak load" which matches keyword fallback when LLM is unavailable
        var result = await _tool.Execute("Show hourly peak load during the week of January 5, 2025");

        Assert.Equal("hour", result.Grain);
        Assert.Contains("'hour' explicitly mentioned", result.Reason);
        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
        Assert.Equal("peak_demand", result.Intent);
    }

    [Fact]
    public async Task Execute_WeekOfFeb28_MonthBoundaryEdgeCase()
    {
        // "week of Feb 28" — edge case near month boundary (spans into March)
        var result = await _tool.Execute("Show data for the week of Feb 28, 2025");

        Assert.Equal("2025-02-28", result.StartDate);
        Assert.Equal("2025-03-06", result.EndDate);
        Assert.Contains("week of", result.Reason);

        var start = DateTime.Parse(result.StartDate);
        var end = DateTime.Parse(result.EndDate);
        Assert.Equal(6, (end - start).Days);
    }

    [Fact]
    public async Task Execute_WeekOfDec29_YearBoundaryEdgeCase()
    {
        // "week of Dec 29" — edge case crossing year boundary (spans into next year)
        var result = await _tool.Execute("Show data for the week of Dec 29, 2024");

        Assert.Equal("2024-12-29", result.StartDate);
        Assert.Equal("2025-01-04", result.EndDate);
        Assert.Contains("week of", result.Reason);

        var start = DateTime.Parse(result.StartDate);
        var end = DateTime.Parse(result.EndDate);
        Assert.Equal(6, (end - start).Days);
        Assert.Equal(2024, start.Year);
        Assert.Equal(2025, end.Year); // Crosses year boundary
    }

    [Fact]
    public async Task Execute_WeekOfWithDailyGrain_ExplicitGrainOverridesAuto()
    {
        // Week is 7 days, which normally auto-selects "day" grain
        // But let's confirm explicit grain works
        var result = await _tool.Execute("Show daily breakdown for week of January 5, 2025");

        Assert.Equal("day", result.Grain);
        Assert.Contains("'day' explicitly mentioned", result.Reason);
        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
    }

    [Fact]
    public async Task Execute_WeekOfWithWeeklyGrain_ExplicitGrainWorks()
    {
        var result = await _tool.Execute("Show weekly summary for week of January 5, 2025");

        Assert.Equal("week", result.Grain);
        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
    }

    [Fact]
    public async Task Execute_WeekOf_AutoSelectsDayGrain()
    {
        // A week (7 days) should auto-select day grain based on the ≤14 day rule
        var result = await _tool.Execute("Show data for week of January 5, 2025");

        Assert.Equal("day", result.Grain);
        Assert.Contains("auto-selected grain 'day'", result.Reason);
    }

    [Theory]
    [InlineData("week of jan 5, 2025", "2025-01-05", "2025-01-11")]
    [InlineData("week of JAN 5, 2025", "2025-01-05", "2025-01-11")]
    [InlineData("week of JANUARY 5, 2025", "2025-01-05", "2025-01-11")]
    public async Task Execute_WeekOfCaseInsensitive_ParsesCorrectly(string phrase, string expectedStart, string expectedEnd)
    {
        var result = await _tool.Execute($"Show data for {phrase}");

        Assert.Equal(expectedStart, result.StartDate);
        Assert.Equal(expectedEnd, result.EndDate);
    }

    [Fact]
    public async Task Execute_WeekOfShortMonth_AllShortMonthFormats()
    {
        // Test various short month formats work
        var result1 = await _tool.Execute("week of Mar 15, 2025");
        var result2 = await _tool.Execute("week of Sep 1, 2025");
        var result3 = await _tool.Execute("week of Oct 10, 2025");

        Assert.Equal("2025-03-15", result1.StartDate);
        Assert.Equal("2025-09-01", result2.StartDate);
        Assert.Equal("2025-10-10", result3.StartDate);
    }

    [Fact]
    public async Task Execute_WeekOfNoYearFutureDate_DefaultsToPreviousYear()
    {
        // If date is in future relative to today, should default to previous year
        var today = DateTime.UtcNow.Date;
        var futureMonth = today.AddMonths(2);
        var monthName = futureMonth.ToString("MMM");
        var dayOfMonth = 15;

        var result = await _tool.Execute($"Show data for week of {monthName} {dayOfMonth}");

        var start = DateTime.Parse(result.StartDate);
        // Should be in current year or previous year (not future)
        Assert.True(start <= today || (start.Year == today.Year - 1));
        Assert.Contains("week of", result.Reason);
    }

    [Fact]
    public async Task Execute_WeekOfWithLoadFactor_IntentAndDateCorrect()
    {
        var result = await _tool.Execute("What was the load factor for the week of January 5, 2025?");

        Assert.Equal("load_factor", result.Intent);
        Assert.Equal("2025-01-05", result.StartDate);
        Assert.Equal("2025-01-11", result.EndDate);
        Assert.Contains("load_factor", result.Metrics);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Execute_CaseInsensitiveKeywords_MatchesCorrectly()
    {
        var result1 = await _tool.Execute("Show PEAK DEMAND for last month");
        var result2 = await _tool.Execute("Show Peak Demand for last month");
        var result3 = await _tool.Execute("show peak demand for last month");

        Assert.Equal("peak_demand", result1.Intent);
        Assert.Equal("peak_demand", result2.Intent);
        Assert.Equal("peak_demand", result3.Intent);
    }

    [Fact]
    public async Task Execute_MultipleKeywordMatches_ReturnsFirstMatch()
    {
        // "peak" matches before "average" in the rule order
        var result = await _tool.Execute("Show peak and average load");

        Assert.Equal("peak_demand", result.Intent);
    }

    [Fact]
    public async Task Execute_EmptyQuestion_DoesNotThrow()
    {
        var result = await _tool.Execute("");

        Assert.NotNull(result);
        Assert.Equal("general_trend", result.Intent);
    }

    [Fact]
    public async Task Execute_WhitespaceOnlyQuestion_DoesNotThrow()
    {
        var result = await _tool.Execute("   ");

        Assert.NotNull(result);
        Assert.Equal("general_trend", result.Intent);
    }

    [Fact]
    public async Task Execute_VeryLongQuestion_DoesNotThrow()
    {
        var longQuestion = "Show me " + string.Concat(Enumerable.Repeat("the average load ", 1000));
        var result = await _tool.Execute(longQuestion);

        Assert.NotNull(result);
        Assert.Equal("avg_load", result.Intent);
    }

    [Fact]
    public async Task Execute_SpecialCharactersInQuestion_DoesNotThrow()
    {
        var result = await _tool.Execute("Show me data with sp3c!@l ch@rs & symbols");

        Assert.NotNull(result);
        Assert.NotNull(result.Intent);
    }

    [Fact]
    public async Task Execute_ReasonField_ContainsAllThreeComponents()
    {
        var result = await _tool.Execute("Show daily peak demand for last month");

        // Should contain intent, grain, and date reasoning
        Assert.Contains(';', result.Reason);
        var parts = result.Reason.Split(';');
        Assert.True(parts.Length >= 3, "Reason should have at least 3 components");
    }

    #endregion

    #region Metric Assignment Tests

    [Fact]
    public async Task Execute_MinMaxIntent_ReturnsCorrectMetrics()
    {
        var result = await _tool.Execute("Show min and max");

        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
        Assert.Equal(2, result.Metrics.Count);
    }

    [Fact]
    public async Task Execute_PeakDemandIntent_ReturnsCorrectMetrics()
    {
        var result = await _tool.Execute("Show peak demand");

        Assert.Contains("max_mw", result.Metrics);
        Assert.Contains("max_mw_time", result.Metrics);
        Assert.Equal(2, result.Metrics.Count);
    }

    [Fact]
    public async Task Execute_MinDemandIntent_ReturnsCorrectMetrics()
    {
        var result = await _tool.Execute("Show minimum load");

        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("min_mw_time", result.Metrics);
        Assert.Equal(2, result.Metrics.Count);
    }

    [Fact]
    public async Task Execute_AvgLoadIntent_ReturnsCorrectMetrics()
    {
        var result = await _tool.Execute("Show average load");

        Assert.Contains("average_mw", result.Metrics);
        Assert.Single(result.Metrics);
    }

    [Fact]
    public async Task Execute_LoadFactorIntent_ReturnsCorrectMetrics()
    {
        var result = await _tool.Execute("Show load factor");

        Assert.Contains("load_factor", result.Metrics);
        Assert.Contains("average_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
        Assert.Equal(3, result.Metrics.Count);
    }

    [Fact]
    public async Task Execute_SummaryIntent_ReturnsAllKeyMetrics()
    {
        var result = await _tool.Execute("Give me a summary");

        Assert.Contains("average_mw", result.Metrics);
        Assert.Contains("min_mw", result.Metrics);
        Assert.Contains("max_mw", result.Metrics);
        Assert.Contains("load_factor", result.Metrics);
        Assert.Equal(4, result.Metrics.Count);
    }

    #endregion
}
