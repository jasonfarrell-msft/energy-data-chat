# History — Ripley

## Core Context
FusionSun is a PSEG energy chat assistant. Three components:
- React frontend (Chat.jsx) with auto-charting via Recharts
- .NET Chat API using Azure AI Foundry agents
- .NET MCP Server with RouteQuestion and GetMetrics tools

Data model: Raw 5-min MW readings → daily/weekly/monthly rollups with average_mw, min_mw, max_mw, load_factor metrics.

User: Jason Farrell

## Learnings

### 2025-01-27: Codebase Review
- **Architecture:** Three-tier with React frontend → .NET Chat API (Azure AI Foundry agents) → MCP Server with EF Core on SQL Server
- **Key files:** `Chat.jsx` (main UI + charting), `ChatController.cs` (agent orchestration with approval loop), `GetMetricsTool.cs`/`RouteQuestionTool.cs` (MCP tools)
- **Data model:** Raw 5-min MW readings in `energy-data-raw`, rolled up to daily/weekly/monthly tables
- **Findings:** 14 improvement items identified (see `decisions/inbox/ripley-codebase-review.md`)
- **Critical gaps:** No API auth, zero tests, hardcoded API URL, no error handling in MCP tools
- **Infra:** Azure Container Apps with CORS configured for localhost:5173 + frontend hostname
