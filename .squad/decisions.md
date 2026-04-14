# Squad Decisions

## Active Decisions

### 2026-04-14: Team formation
**By:** Squad (Coordinator)
**What:** Hired initial team: Ripley (Lead), Dallas (Frontend), Parker (Backend), Lambert (Tester), Scribe (Logger)
**Why:** Project needs improvements to existing energy analytics chat application

### 2026-04-14: Codebase review — 14 improvement opportunities
**By:** Ripley (Lead)
**What:** Comprehensive review identified security, performance, testing, and UX improvements across frontend, backend, and tooling
**Priority:** 4 HIGH, 5 MEDIUM, 5 LOW
**Assignments:**
- **Parker (Backend):** Auth (HIGH), error handling (HIGH), conversation state (MED), DbContext pooling (MED), request validation (MED), Swagger (LOW), health checks (LOW), retry logic (LOW)
- **Dallas (Frontend):** Hardcoded URLs (HIGH), error messages (MED), loading states (MED), chart colors (LOW), a11y attributes (LOW)
- **Lambert (Testing):** Test coverage (HIGH)
**Next Steps:** Prioritize auth and test coverage as blocking for production readiness

### 2026-04-14T18:42: User directives — LLM intent and hourly rollup
**By:** Jason Farrell (via Copilot)
**What:**
1. Use LLM for intent classification in RouteQuestion instead of keyword matching
2. Add hourly aggregation rollup — raw data is 5-second intervals, not 5-minute
3. Existing stored procedures handle daily/weekly/monthly; need hourly added

**Why:** User request for production readiness. Keyword-based classification is limited; LLM classification is more flexible. Hourly granularity required for "1hr granularity" mentioned in requirements.
**Status:** Implemented by Parker

### 2026-04-14: Frontend environment variables pattern
**By:** Dallas (Frontend Developer)
**What:** Use Vite environment variables for all frontend configuration
```javascript
const CONFIG = import.meta.env.VITE_VARIABLE_NAME || 'fallback-value'
```
**Context:** Hardcoded API URL prevents switching between environments (dev/staging/prod)
**Implementation:**
- All client-side env vars require `VITE_` prefix
- Always provide sensible fallback values
- Document in `.env.example`
- Use `import.meta.env`, not `process.env`

**Files:** `frontend/.env.example`, `frontend/src/components/Chat.jsx`
**Status:** Complete

### 2026-04-14: Backend production improvements — comprehensive readiness
**By:** Parker (Backend Developer)
**What:** Implemented 8 production-readiness improvements based on Ripley's review + user directives
**Key Changes:**
- Error handling: try/catch on MCP tools and database queries with structured error messages
- DbContext pooling for high-frequency MCP scenarios
- Request validation with clear 400 BadRequest responses
- Swagger/OpenAPI documentation at /swagger (Development mode)
- Health check endpoints at /health with DbContext connectivity
- Polly 8.5.0 retry logic: exponential backoff, 3 attempts for AI Foundry calls
- LLM intent classification: Azure AI Foundry gpt-4o-mini with keyword fallback
- Hourly rollup: stored procedure + entity + GetMetricsTool grain support

**Status:** Implemented and tested

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
