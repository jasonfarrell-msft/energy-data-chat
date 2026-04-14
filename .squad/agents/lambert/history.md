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
- `/Example.EnergyAnalyticsMcp.Tests/RouteQuestionToolTests.cs` - 85 passing tests
- `/Example.EnergyAnalyticsMcp.Tests/GetMetricsToolTests.cs` - 55 tests (EF issue)
- `/Example.ChatApi.Tests/ChatControllerTests.cs` - 27 passing tests
