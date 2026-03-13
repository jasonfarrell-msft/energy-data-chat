using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.Projects;
using Example.EnergyAnalyticsMcp.Models;
using ModelContextProtocol.Server;
using OpenAI.Chat;

#pragma warning disable OPENAI001

namespace Example.EnergyAnalyticsMcp.Tools;

[McpServerToolType]
public class RouteQuestionTool
{
    private const string DeploymentName = "gpt-5.2-chat-deployment";

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
        (["average load", "avg load", "mean load", "average demand", "avg demand", "average mw", "avg mw", "mean mw"], "avg_load"),
        (["load factor", "capacity factor", "utilization"], "load_factor"),
        (["trend", "over time", "changed", "change", "how did", "how has"], "general_trend"),
        (["summary", "overview", "summarize", "report"], "summary"),
    ];

    private static readonly string IntentClassificationPrompt =
        $$"""
        You are an energy-analytics intent classifier.
        Given a user question about electrical load / energy data, respond with ONLY a JSON object — no markdown, no explanation.

        Valid intents and their meanings:
        - min_max_mw_trend : user asks about minimum and/or maximum megawatt trends
        - peak_demand      : user asks about the highest load or peak demand
        - min_demand       : user asks about the lowest load or minimum demand
        - avg_load         : user asks about average load or average megawatts
        - load_factor      : user asks about load factor, capacity factor, or utilization
        - general_trend    : user asks about trends, changes over time, or general patterns
        - summary          : user asks for an overview, summary, or report

        Response schema (strict):
        {"intent":"<one of the valid intents>","reason":"<one sentence explaining why>"}
        """;

    private readonly AIProjectClient _projectClient;

    public RouteQuestionTool(AIProjectClient projectClient)
    {
        _projectClient = projectClient;
    }

    /// <summary>
    /// Classifies a natural-language energy question into a structured query plan.
    /// Uses deterministic rules for date range and grain, and an LLM for intent classification
    /// with a keyword-based fallback.
    /// </summary>
    [McpServerTool(Name = "RouteQuestion")]
    [Description(
        "Classifies a natural-language energy question into a structured query plan. " +
        "Accepts a user question and optional conversation state (last_grain, last_range) " +
        "and returns: intent (e.g. min_max_mw_trend, avg_load, peak_demand), " +
        "grain (day | week | month), metrics list (e.g. min_mw, max_mw, avg_mw), " +
        "start_date, end_date, and a reason string. " +
        "Call this tool first to determine what data to fetch, then pass the result to GetMetrics.")]
    public async Task<RouteQuestionOutput> Execute(
        [Description("The natural-language energy question from the user, e.g. 'How did the daily minimum and maximum load change last month?'")]
        string question,

        [Description("Optional JSON-encoded conversation state containing last_grain (day|week|month) and last_range ({start, end} as ISO dates). Pass null or omit if no prior context exists.")]
        string? conversationState = null)
    {
        ConversationState? state = null;
        if (!string.IsNullOrWhiteSpace(conversationState))
        {
            state = JsonSerializer.Deserialize<ConversationState>(conversationState, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        var q = question.ToLowerInvariant();
        var today = DateTime.UtcNow.Date;

        // 1. Deterministic: resolve date range
        var (startDate, endDate, dateReason) = ResolveTimeRange(q, today, state);

        // 2. Deterministic: resolve grain
        var (grain, grainReason) = ResolveGrain(q, startDate, endDate, state);

        // 3. Hybrid: LLM for intent, keyword fallback
        var (intent, intentReason) = await ResolveIntentAsync(question);
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

    // ── LLM intent classification with keyword fallback ──
    private async Task<(string Intent, string Reason)> ResolveIntentAsync(string question)
    {
        try
        {
            ChatClient chatClient = _projectClient.OpenAI.GetChatClient(DeploymentName);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(IntentClassificationPrompt),
                new UserChatMessage(question)
            };

            var response = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
            {
                Temperature = 0f,
                MaxOutputTokenCount = 150
            });

            var content = response.Value.Content[0].Text.Trim();

            // Parse the LLM JSON response
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var intent = root.GetProperty("intent").GetString() ?? "general_trend";
            var reason = root.GetProperty("reason").GetString() ?? "LLM classified";

            // Validate against known intents
            if (!IntentMetrics.ContainsKey(intent))
            {
                return ResolveIntentByKeywords(question.ToLowerInvariant(),
                    $"LLM returned unknown intent '{intent}'; fell back to keywords");
            }

            return (intent, $"LLM classified intent as '{intent}': {reason}");
        }
        catch
        {
            // LLM unavailable — fall back to deterministic keyword matching
            return ResolveIntentByKeywords(question.ToLowerInvariant(),
                "LLM call failed; fell back to keyword matching");
        }
    }

    private static (string Intent, string Reason) ResolveIntentByKeywords(string q, string? prefixReason = null)
    {
        foreach (var rule in KeywordFallbackRules)
        {
            foreach (var kw in rule.Keywords)
            {
                if (q.Contains(kw, StringComparison.OrdinalIgnoreCase))
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

        // Fall back to conversation state
        if (state?.LastRange is { Start: not null, End: not null }
            && DateTime.TryParseExact(state.LastRange.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cs)
            && DateTime.TryParseExact(state.LastRange.End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ce))
        {
            return (cs, ce, $"Carried forward from conversation state: {cs:yyyy-MM-dd} to {ce:yyyy-MM-dd}");
        }

        // Ultimate fallback: last 30 days
        return (today.AddDays(-30), today, "No time range detected; defaulting to last 30 days");
    }

    // ── Grain resolution ──
    private static (string Grain, string Reason) ResolveGrain(
        string q, DateTime start, DateTime end, ConversationState? state)
    {
        // Explicit grain mention in the question takes priority
        if (Regex.IsMatch(q, @"\bdaily\b|\bper\s*day\b|\beach\s*day\b|\bday[\s-]over[\s-]day\b|\bby\s+day\b"))
            return ("day", "Grain 'day' explicitly mentioned in question");
        if (Regex.IsMatch(q, @"\bweekly\b|\bper\s*week\b|\beach\s*week\b|\bweek[\s-]over[\s-]week\b|\bby\s+week\b"))
            return ("week", "Grain 'week' explicitly mentioned in question");
        if (Regex.IsMatch(q, @"\bmonthly\b|\bper\s*month\b|\beach\s*month\b|\bmonth[\s-]over[\s-]month\b|\bby\s+month\b"))
            return ("month", "Grain 'month' explicitly mentioned in question");

        // Infer from window size
        var span = (end - start).TotalDays;
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
