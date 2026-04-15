using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.Projects;
using Example.EnergyAnalyticsMcp.Models;
using ModelContextProtocol.Server;

namespace Example.EnergyAnalyticsMcp.Tools;

[McpServerToolType]
public class RouteQuestionTool
{
    private readonly AIProjectClient? _projectClient;
    private readonly ILogger<RouteQuestionTool>? _logger;

    // ── Valid intents and their associated metrics ──
    private static readonly Dictionary<string, string[]> IntentMetrics = new()
    {
        ["min_max_mw_trend"] = ["min_mw", "max_mw"],
        ["peak_demand"]      = ["max_mw", "max_mw_time"],
        ["min_demand"]       = ["min_mw", "min_mw_time"],
        ["avg_load"]         = ["average_mw"],
        ["load_factor"]      = ["load_factor", "average_mw", "max_mw"],
        ["general_trend"]    = ["average_mw", "min_mw", "max_mw"],
        ["summary"]          = ["average_mw", "min_mw", "max_mw", "load_factor"],
    };

    // ── Keyword fallback rules (used when LLM is unavailable) ──
    private static readonly (string[] Keywords, string Intent)[] KeywordFallbackRules =
    [
        (["min and max", "min/max", "minimum and maximum", "min & max", "min max"], "min_max_mw_trend"),
        (["peak demand", "peak load", "highest load", "highest demand", "max load", "maximum load", "max demand"], "peak_demand"),
        (["lowest load", "minimum load", "min load", "lowest demand", "min demand", "minimum demand"], "min_demand"),
        (["average load", "avg load", "mean load", "average demand", "avg demand", "average mw", "avg mw", "mean mw", "average.*load", "average.*demand"], "avg_load"),
        (["load factor", "capacity factor", "utilization"], "load_factor"),
        (["trend", "over time", "changed", "change", "how did", "how has"], "general_trend"),
        (["summary", "overview", "summarize", "report"], "summary"),
    ];

    public RouteQuestionTool(AIProjectClient? projectClient = null, ILogger<RouteQuestionTool>? logger = null)
    {
        _projectClient = projectClient;
        _logger = logger;
    }

    /// <summary>
    /// Classifies a natural-language energy question into a structured query plan.
    /// Uses deterministic rules for date range, grain, and intent classification.
    /// </summary>
    [McpServerTool(Name = "RouteQuestion")]
    [Description(
        "Classifies a natural-language energy question into a structured query plan. " +
        "Accepts a user question and optional conversation state (last_grain, last_range) " +
        "and returns: intent (e.g. min_max_mw_trend, avg_load, peak_demand), " +
        "grain (hour | day | week | month), metrics list (e.g. min_mw, max_mw, avg_mw), " +
        "start_date, end_date, and a reason string. " +
        "Call this tool first to determine what data to fetch, then pass the result to GetMetrics.")]
    public async Task<RouteQuestionOutput> Execute(
        [Description("The natural-language energy question from the user, e.g. 'How did the daily minimum and maximum load change last month?'")]
        string question,

        [Description("Optional JSON-encoded conversation state containing last_grain (day|week|month) and last_range ({start, end} as ISO dates). Pass null or omit if no prior context exists.")]
        string? conversationState = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return ErrorOutput("Question cannot be null or empty.");
            }

            ConversationState? state = null;
            if (!string.IsNullOrWhiteSpace(conversationState))
            {
                try
                {
                    state = JsonSerializer.Deserialize<ConversationState>(conversationState, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    return ErrorOutput($"Failed to parse conversation state: {ex.Message}");
                }
            }

            var q = question.ToLowerInvariant();
            var today = DateTime.UtcNow.Date;

            // 1. Deterministic: resolve date range
            var (startDate, endDate, dateReason) = ResolveTimeRange(q, today, state);

            // 2. Deterministic: resolve grain
            var (grain, grainReason) = ResolveGrain(q, startDate, endDate, state);

            // 3. LLM-based intent classification (with keyword fallback)
            var (intent, intentReason) = await ResolveIntentAsync(q).ConfigureAwait(false);
            var metrics = IntentMetrics.TryGetValue(intent, out var m)
                ? m.ToList()
                : ["average_mw", "min_mw", "max_mw"];

            return new RouteQuestionOutput
            {
                Intent = intent,
                Grain = grain,
                Metrics = metrics,
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                Reason = $"{intentReason}; {grainReason}; {dateReason}"
            };
        }
        catch (Exception ex)
        {
            return ErrorOutput($"Failed to route question: {ex.Message}");
        }
    }

    private static RouteQuestionOutput ErrorOutput(string message) => new()
    {
        Intent = "error",
        Grain = "day",
        Metrics = [],
        StartDate = "",
        EndDate = "",
        Reason = message
    };

    private async Task<(string Intent, string Reason)> ResolveIntentAsync(string q)
    {
        if (_projectClient != null)
        {
            try
            {
                var llmIntent = await ClassifyIntentWithLLMAsync(q);
                if (!string.IsNullOrEmpty(llmIntent) && IntentMetrics.ContainsKey(llmIntent))
                {
                    return (llmIntent, $"Intent '{llmIntent}' classified by LLM");
                }
                _logger?.LogWarning("LLM returned invalid intent '{Intent}', falling back to keywords", llmIntent);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LLM intent classification failed, falling back to keywords");
            }
        }

        return ResolveIntentByKeywords(q, "LLM unavailable or failed");
    }

    private async Task<string?> ClassifyIntentWithLLMAsync(string question)
    {
        var prompt = $@"You are an energy analytics intent classifier. Classify the following question into exactly ONE of these intents:

- min_max_mw_trend: Questions about minimum and maximum MW values together
- peak_demand: Questions about peak load, highest demand, or maximum MW specifically
- min_demand: Questions about minimum load or lowest demand
- avg_load: Questions about average load or mean demand
- load_factor: Questions about load factor, capacity factor, or utilization
- general_trend: Questions about trends, changes over time, or how values changed
- summary: Questions asking for a summary, overview, or report

Question: {question}

Respond with ONLY the intent name, nothing else.";

        var chatClient = _projectClient!.OpenAI.GetChatClient("gpt-4o-mini");
        var response = await chatClient.CompleteChatAsync(prompt);
        var intent = response.Value.Content[0].Text.Trim().ToLowerInvariant();
        return intent;
    }

    private static (string Intent, string Reason) ResolveIntentByKeywords(string q, string? prefixReason = null)
    {
        foreach (var rule in KeywordFallbackRules)
        {
            foreach (var kw in rule.Keywords)
            {
                bool matched = kw.Contains('.')
                    ? Regex.IsMatch(q, kw, RegexOptions.IgnoreCase)
                    : q.Contains(kw, StringComparison.OrdinalIgnoreCase);

                if (matched)
                {
                    var reason = $"Intent '{rule.Intent}' matched keyword '{kw}'";
                    return (rule.Intent, prefixReason != null ? $"{prefixReason} → {reason}" : reason);
                }
            }
        }

        var fallback = "No specific intent keywords matched; defaulting to general_trend";
        return ("general_trend", prefixReason != null ? $"{prefixReason} → {fallback}" : fallback);
    }

    // ── Time-range resolution (deterministic) ──
    private static (DateTime Start, DateTime End, string Reason) ResolveTimeRange(
        string q, DateTime today, ConversationState? state)
    {
        // Explicit ISO date range in the question: "from 2026-01-01 to 2026-02-28"
        var explicitRange = Regex.Match(q, @"from\s+(\d{4}-\d{2}-\d{2})\s+to\s+(\d{4}-\d{2}-\d{2})");
        if (explicitRange.Success
            && DateTime.TryParseExact(explicitRange.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var es)
            && DateTime.TryParseExact(explicitRange.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ee))
        {
            return (es, ee, $"Explicit date range parsed: {es:yyyy-MM-dd} to {ee:yyyy-MM-dd}");
        }

        // "week of {date}" — supports: "week of Jan 5", "week of January 5, 2025", "week of 2025-01-05"
        var weekOfMatch = Regex.Match(q,
            @"week\s+of\s+(" +
            // ISO format: 2025-01-05
            @"\d{4}-\d{2}-\d{2}|" +
            // Month day, year: January 5, 2025 or Jan 5, 2025
            @"(?:january|february|march|april|may|june|july|august|september|october|november|december|" +
            @"jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)\s+\d{1,2}(?:,?\s+\d{4})?)",
            RegexOptions.IgnoreCase);
        if (weekOfMatch.Success)
        {
            var dateStr = weekOfMatch.Groups[1].Value.Trim();
            DateTime? weekStart = null;

            // Try ISO format first
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            {
                weekStart = isoDate;
            }
            // Try "Month Day, Year" or "Month Day" formats
            else if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                // If no year was specified, default to the reference year (today's year or previous year if date is in future)
                if (!Regex.IsMatch(dateStr, @"\d{4}"))
                {
                    parsedDate = new DateTime(today.Year, parsedDate.Month, parsedDate.Day);
                    if (parsedDate > today)
                    {
                        parsedDate = parsedDate.AddYears(-1);
                    }
                }
                weekStart = parsedDate;
            }

            if (weekStart.HasValue)
            {
                var weekEnd = weekStart.Value.AddDays(6);
                return (weekStart.Value, weekEnd,
                    $"'week of {dateStr}' → {weekStart.Value:yyyy-MM-dd} to {weekEnd:yyyy-MM-dd}");
            }
        }

        // "last N days"
        var lastNDays = Regex.Match(q, @"last\s+(\d+)\s+days?");
        if (lastNDays.Success && int.TryParse(lastNDays.Groups[1].Value, out var nDays))
        {
            return (today.AddDays(-nDays), today,
                $"Relative range: last {nDays} days");
        }

        // "last N weeks"
        var lastNWeeks = Regex.Match(q, @"last\s+(\d+)\s+weeks?");
        if (lastNWeeks.Success && int.TryParse(lastNWeeks.Groups[1].Value, out var nWeeks))
        {
            return (today.AddDays(-nWeeks * 7), today,
                $"Relative range: last {nWeeks} weeks");
        }

        // "last N months"
        var lastNMonths = Regex.Match(q, @"last\s+(\d+)\s+months?");
        if (lastNMonths.Success && int.TryParse(lastNMonths.Groups[1].Value, out var nMonths))
        {
            var monthStart = today.AddMonths(-nMonths);
            monthStart = new DateTime(monthStart.Year, monthStart.Month, 1);
            var monthEnd = new DateTime(today.Year, today.Month, 1).AddDays(-1);
            return (monthStart, monthEnd,
                $"Relative range: last {nMonths} months → {monthStart:yyyy-MM-dd} to {monthEnd:yyyy-MM-dd}");
        }

        // "last month"
        if (Regex.IsMatch(q, @"\blast\s+month\b"))
        {
            var firstOfLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            var endOfLastMonth = firstOfLastMonth.AddMonths(1).AddDays(-1);
            return (firstOfLastMonth, endOfLastMonth,
                $"'last month' → {firstOfLastMonth:yyyy-MM-dd} to {endOfLastMonth:yyyy-MM-dd}");
        }

        // "last week"
        if (Regex.IsMatch(q, @"\blast\s+week\b"))
        {
            var startOfThisWeek = today.AddDays(-(int)today.DayOfWeek + (int)System.DayOfWeek.Monday);
            if (startOfThisWeek > today) startOfThisWeek = startOfThisWeek.AddDays(-7);
            var startOfLastWeek = startOfThisWeek.AddDays(-7);
            var endOfLastWeek = startOfThisWeek.AddDays(-1);
            return (startOfLastWeek, endOfLastWeek,
                $"'last week' → {startOfLastWeek:yyyy-MM-dd} to {endOfLastWeek:yyyy-MM-dd}");
        }

        // "last year"
        if (Regex.IsMatch(q, @"\blast\s+year\b"))
        {
            var start = new DateTime(today.Year - 1, 1, 1);
            var end = new DateTime(today.Year - 1, 12, 31);
            return (start, end, $"'last year' → {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
        }

        // "this month"
        if (Regex.IsMatch(q, @"\bthis\s+month\b"))
        {
            var start = new DateTime(today.Year, today.Month, 1);
            return (start, today, $"'this month' → {start:yyyy-MM-dd} to {today:yyyy-MM-dd}");
        }

        // "this week"
        if (Regex.IsMatch(q, @"\bthis\s+week\b"))
        {
            var start = today.AddDays(-(int)today.DayOfWeek + (int)System.DayOfWeek.Monday);
            if (start > today) start = start.AddDays(-7);
            return (start, today, $"'this week' → {start:yyyy-MM-dd} to {today:yyyy-MM-dd}");
        }

        // "today"
        if (q.Contains("today"))
        {
            return (today, today, "'today' → single day");
        }

        // "yesterday"
        if (q.Contains("yesterday"))
        {
            var y = today.AddDays(-1);
            return (y, y, $"'yesterday' → {y:yyyy-MM-dd}");
        }

        // Named month + year: "January 2024", "in Feb 2025", "for March 2024", etc.
        var namedMonthYear = Regex.Match(q,
            @"\b(january|february|march|april|may|june|july|august|september|october|november|december|" +
            @"jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\s+(\d{4})\b");
        if (namedMonthYear.Success
            && DateTime.TryParseExact(
                namedMonthYear.Groups[1].Value + " " + namedMonthYear.Groups[2].Value,
                new[] { "MMMM yyyy", "MMM yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var namedMonth))
        {
            var monthStart = new DateTime(namedMonth.Year, namedMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            return (monthStart, monthEnd,
                $"Named month '{namedMonthYear.Value}' → {monthStart:yyyy-MM-dd} to {monthEnd:yyyy-MM-dd}");
        }

        // Standalone year: "in 2024", "for 2024", "during 2024", or bare "2024"
        var yearOnly = Regex.Match(q, @"\b(in|for|during)?\s*(\d{4})\b");
        if (yearOnly.Success && int.TryParse(yearOnly.Groups[2].Value, out var year)
            && year >= 2000 && year <= today.Year + 1)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);
            if (yearEnd > today) yearEnd = today;
            return (yearStart, yearEnd,
                $"Year '{year}' → {yearStart:yyyy-MM-dd} to {yearEnd:yyyy-MM-dd}");
        }

        // Fall back to conversation state
        if (state?.LastRange is { Start: not null, End: not null }
            && DateTime.TryParseExact(state.LastRange.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cs)
            && DateTime.TryParseExact(state.LastRange.End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ce))
        {
            return (cs, ce, $"Carried forward from conversation state: {cs:yyyy-MM-dd} to {ce:yyyy-MM-dd}");
        }

        // Ultimate fallback: POC data range (Jan–Mar 2025)
        return (new DateTime(2025, 1, 1), new DateTime(2025, 3, 31), "No time range detected; defaulting to available data range Jan–Mar 2025");
    }

    // ── Grain resolution ──
    private static (string Grain, string Reason) ResolveGrain(
        string q, DateTime start, DateTime end, ConversationState? state)
    {
        // Explicit grain mention in the question takes priority
        if (Regex.IsMatch(q, @"\bhourly\b|\bper\s*hour\b|\beach\s*hour\b|\bhour[\s-]over[\s-]hour\b|\bby\s+(?:the\s+)?hour\b"))
            return ("hour", "Grain 'hour' explicitly mentioned in question");
        if (Regex.IsMatch(q, @"\bdaily\b|\bper\s*day\b|\beach\s*day\b|\bday[\s-]over[\s-]day\b|\bby\s+day\b"))
            return ("day", "Grain 'day' explicitly mentioned in question");
        if (Regex.IsMatch(q, @"\bweekly\b|\bper\s*week\b|\beach\s*week\b|\bweek[\s-]over[\s-]week\b|\bby\s+week\b"))
            return ("week", "Grain 'week' explicitly mentioned in question");
        if (Regex.IsMatch(q, @"\bmonthly\b|\bper\s*month\b|\beach\s*month\b|\bmonth[\s-]over[\s-]month\b|\bby\s+month\b"))
            return ("month", "Grain 'month' explicitly mentioned in question");

        // Infer from window size
        var span = (end - start).TotalDays;
        if (span <= 3)
            return ("hour", $"Window is {span:F0} days; auto-selected grain 'hour'");
        if (span <= 14)
            return ("day", $"Window is {span:F0} days; auto-selected grain 'day'");
        if (span <= 90)
            return ("week", $"Window is {span:F0} days; auto-selected grain 'week'");
        if (span <= 730)
            return ("month", $"Window is {span:F0} days; auto-selected grain 'month'");

        // Carry forward from conversation state
        if (!string.IsNullOrWhiteSpace(state?.LastGrain))
            return (state.LastGrain, $"Grain '{state.LastGrain}' carried from conversation state");

        return ("day", "No grain signal; defaulting to 'day'");
    }
}
