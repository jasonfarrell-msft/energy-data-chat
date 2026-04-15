# History — Parker

## Core Context
FusionSun backend has two .NET 8 services:

**Chat API (Example.ChatApi)**
- Single ChatController.cs endpoint
- Uses AIProjectClient from Azure.AI.Projects to call AI Foundry agents
- Auto-approves MCP tool calls in a loop (max 5 iterations)
- Returns conversation ID for continuity

**MCP Server (Example.EnergyAnalyticsMcp)**
- Two tools: RouteQuestion (intent classification) and GetMetrics (data retrieval)
- RouteQuestion: deterministic date/grain resolution, keyword-based intent matching
- GetMetrics: queries daily/weekly/monthly rollup tables via EF Core
- Metrics: average_mw, min_mw, max_mw, min_mw_time, max_mw_time, load_factor
- Data range: Jan-Mar 2025

User: Jason Farrell

## Learnings

### 2026-04-14: Implemented production improvements
**Error Handling:**
- Added comprehensive try/catch blocks to RouteQuestionTool.Execute() and GetMetricsTool.Execute()
- Database query methods (QueryDailyAsync, QueryWeeklyAsync, QueryMonthlyAsync, QueryHourlyAsync) now catch exceptions and return meaningful error messages
- Input validation includes null/empty checks for all required parameters

**Performance:**
- Switched from AddDbContext to AddDbContextPool in MCP Server Program.cs for better connection pooling
- DbContext pooling reduces overhead for high-frequency MCP tool calls

**API Validation:**
- ChatController now validates request body and ChatMessage before processing
- Returns 400 BadRequest with clear error messages for invalid inputs

**Documentation & Monitoring:**
- Added Swagger/OpenAPI to ChatApi (available in Development mode at /swagger)
- Health check endpoints at /health for both ChatApi and MCP Server
- MCP Server health check includes DbContext connectivity validation

**Resilience:**
- Added Polly 8.5.0 for retry logic in ChatController
- Exponential backoff with 3 retry attempts for AI Foundry API calls
- Retry attempts logged with delay and exception details

**LLM Intent Classification:**
- RouteQuestionTool now uses AIProjectClient to classify intent via LLM (gpt-4o-mini)
- Keyword-based fallback ensures reliability when LLM is unavailable
- Tool made async (Task<RouteQuestionOutput>) to support LLM calls
- All tests updated to async/await pattern

**Hourly Rollup Support:**
- Created usp_RollupEnergyDataHourly stored procedure in DataLoader/stored_procedures/
- Created EnergyDataHourly entity mapped to [energy-data-hourly] table
- Added DbSet<EnergyDataHourly> to EnergyDbContext with precision configuration
- GetMetricsTool now supports "hour" grain with 31-day max window
- RouteQuestionTool recognizes hourly patterns: hourly, per hour, each hour, by the hour
- Auto-selects hour grain for windows ≤3 days

**Key File Paths:**
- Chat API controller: Example.ChatApi/Controllers/ChatController.cs
- MCP tools: Example.EnergyAnalyticsMcp/Tools/{RouteQuestionTool,GetMetricsTool}.cs
- DbContext: Example.EnergyAnalyticsMcp/Data/EnergyDbContext.cs
- Stored procedures: DataLoader/stored_procedures/*.sql
- Entities: Example.EnergyAnalyticsMcp/Entities/*.cs

**Architecture Decisions:**
- Polly ResiliencePipeline instantiated per-controller (not DI singleton) to keep configuration simple
- LLM intent classification is best-effort with graceful keyword fallback
- Health checks use Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.1 for EF Core integration

### 2026-04-15: Added "week of {date}" date pattern support
**RouteQuestionTool Date Parsing:**
- Added support for "week of {date}" patterns in ResolveTimeRange method
- Supported formats: ISO (2025-01-05), named (January 5, 2025), abbreviated (Jan 5, 2025), short (Jan 5)
- Pattern returns 7-day window starting from the specified date
- Position in priority: after explicit ISO ranges, before "last N days" patterns
- When year is omitted and date is in the future, defaults to previous year
- Explicit grain keywords (e.g., "hourly") take precedence via ResolveGrain (already worked)

**Test Updates:**
- Added 23 new tests for "week of" patterns covering ISO, named month, abbreviated month formats
- Fixed test `Execute_HourlyPeakDemandDuringWeekOf_CorrectGrainAndDateRange` to use keyword-matching phrase "peak load" instead of "peak demand"

