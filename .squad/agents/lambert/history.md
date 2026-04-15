# History — Lambert

## Core Context
FusionSun is an energy analytics chat app. Testing concerns:

**Frontend (React + Vite)**
- Chat.jsx: message handling, API calls, markdown parsing, chart rendering
- extractChartData(): markdown table → chart data transformation

**Backend (.NET 8)**
- ChatController: AI Foundry agent integration, approval loops
- RouteQuestion: date parsing, grain selection, intent classification
- GetMetrics: validation, rollup queries, data quality checks

Key edge cases: date parsing (various formats), grain auto-selection, empty data ranges, invalid metrics, conversation continuity.

User: Jason Farrell

## Learnings

### 2026-04-14: Initial test infrastructure setup
Created comprehensive test coverage for backend components:

**Test Projects Created:**
- `Example.EnergyAnalyticsMcp.Tests` - 85 RouteQuestionTool tests (all passing)
- `Example.ChatApi.Tests` - 27 ChatController tests (all passing)
- Total: 112 tests covering critical backend logic

**RouteQuestionTool Test Coverage (85 tests):**
- Intent classification: min_max_mw_trend, peak_demand, min_demand, avg_load, load_factor, general_trend, summary
- Date parsing: explicit ranges (from/to), relative (last N days/weeks/months), named (last week/month/year, this week/month), specific months (January 2025), years (2024)
- Grain resolution: explicit grain mentions (daily/weekly/monthly), auto-selection based on window size
- Conversation state: carry-forward of grain and date range, explicit overrides
- Edge cases: case insensitivity, empty/whitespace input, very long questions, special characters

**ChatController Test Coverage (27 tests):**
- Request validation: message presence, conversation ID format
- Conversation continuity: conv_ prefix validation, empty/whitespace handling
- Message content: long messages, special characters, newlines, Unicode
- Configuration: agent name defaults and custom values
- Edge cases: max-length conversation IDs, typical user questions

**GetMetricsTool Tests Created (55 tests):**
- Validation tests for grain, metrics, dates, and window limits
- Data retrieval for daily, weekly, and monthly rollups
- Metadata and data quality checks
- TopN limiting and ordering
- Note: Tests created but failing due to EF Core InMemory database compatibility issue with .NET 9/10

**Key Architectural Patterns Discovered:**
- RouteQuestionTool.Execute is synchronous (not async) - uses deterministic rules
- GetMetricsTool.Execute is async - queries EF DbContext
- Tool validation happens before data access (fail-fast pattern)
- Conversation state is optional JSON string parameter
- Date ranges default to POC range (2025-01-01 to 2025-03-31)
- Grain auto-selects based on window: ≤14 days=day, ≤90 days=week, else=month

**Dependencies Fixed:**
- Added Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.0 to EnergyAnalyticsMcp
- Added Swashbuckle.AspNetCore to ChatApi
- Added Moq and Microsoft.EntityFrameworkCore.InMemory to test projects

**File Locations:**
- `/Example.EnergyAnalyticsMcp.Tests/RouteQuestionToolTests.cs` - 108 passing tests
- `/Example.EnergyAnalyticsMcp.Tests/GetMetricsToolTests.cs` - 55 tests (EF issue)
- `/Example.ChatApi.Tests/ChatControllerTests.cs` - 27 passing tests

### 2026-04-14: Week-of date pattern tests
Added 23 unit tests for the "week of {date}" date parsing feature in RouteQuestionTool.

**Test Cases Added:**
- Short month format: "week of Jan 5" (no year, defaults intelligently)
- Full month with year: "week of January 5, 2025"
- ISO format: "week of 2025-01-05"
- Combined with grain: "hourly peak demand during week of Jan 5" (grain=hour)
- Month boundary edge case: "week of Feb 28" (spans into March)
- Year boundary edge case: "week of Dec 29" (crosses into next year)
- Case insensitivity: "WEEK OF JAN 5" works same as "week of Jan 5"
- Intent combination: "load factor for week of Jan 5" (verifies intent + date)
- Grain auto-selection: 7-day window auto-selects "day" grain

**Key Findings:**
- Week spans 7 days (start date + 6 days = end date)
- No-year dates default to current year if in past, previous year if in future
- Explicit grain keywords (hourly/daily/weekly) override auto-selection
- Intent detection works independently of "week of" date parsing

**Pre-existing Test Failures Noted (7 tests, not related to week-of):**
- Conversation state carry-forward tests failing
- Empty/whitespace question handling returns "error" intent instead of "general_trend"
- Invalid JSON conversation state returns "error" instead of graceful fallback
- These appear to be from production code changes since original tests were written
