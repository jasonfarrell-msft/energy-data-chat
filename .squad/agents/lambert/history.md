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
