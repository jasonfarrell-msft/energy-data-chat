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
