# Squad Team

> pseg-energy-chat — FusionSun Energy Analytics Chat

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Ripley | Lead | .squad/agents/ripley/charter.md | 🏗️ Active |
| Dallas | Frontend Dev | .squad/agents/dallas/charter.md | ⚛️ Active |
| Parker | Backend Dev | .squad/agents/parker/charter.md | 🔧 Active |
| Lambert | Tester | .squad/agents/lambert/charter.md | 🧪 Active |
| Scribe | Session Logger | .squad/agents/scribe/charter.md | 📋 Active |
| Ralph | Work Monitor | — | 🔄 Monitor |

## Project Context

- **Project:** pseg-energy-chat (FusionSun)
- **User:** Jason Farrell
- **Created:** 2026-04-14
- **Stack:** React + Vite frontend, .NET 8 Chat API (Azure AI Foundry), .NET 8 MCP Server (EF Core + SQL Server)
- **Description:** Energy analytics chat assistant for PSEG. Users ask natural language questions about energy load trends. The system routes questions through an MCP server that classifies intent, resolves date ranges/grains, and queries pre-aggregated rollup tables (daily/weekly/monthly). Supports 1hr to yearly granularity via intelligent grain selection. Frontend auto-renders charts from markdown tables.
