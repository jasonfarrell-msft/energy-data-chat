# History — Dallas

## Core Context
FusionSun frontend is a React + Vite app. Main component is Chat.jsx which:
- Manages chat messages and conversation state
- Posts to Chat API endpoint
- Renders bot responses with ReactMarkdown + remarkGfm
- Auto-extracts markdown tables and renders Recharts (bar for ≤6 points, line otherwise)
- Uses PSEG brand colors: #0079c1, #f0512c, #448359, #142c41, #365453

User: Jason Farrell

## Learnings

### 2026-04-14: Ripley Review Improvements
Completed 5 frontend improvements from Ripley's code review:

**Environment Configuration:**
- Moved hardcoded API URL to import.meta.env.VITE_API_URL with fallback
- Created .env.example with VITE_API_URL variable
- Pattern: Use import.meta.env for all Vite environment variables

**Error Handling:**
- Replaced generic "something went wrong" with HTTP status-specific messages
- Added error parsing from API response JSON (error/message fields)
- Status codes: 400 (bad request), 401/403 (auth), 404 (not found), 429 (rate limit), 500+ (server error)
- Kept fallback for network errors

**Loading States:**
- Added initializing state on app mount with spinning icon
- Shows "Initializing FusionSun..." for 500ms on first load
- Improves perceived performance and user confidence

**Chart Colors:**
- Expanded CHART_COLORS from 5 to 10 colors
- Included colorblind-friendly options (purple, gold, varied blues/greens)
- Maintains PSEG brand colors as primary palette

**Accessibility (a11y):**
- Added ARIA labels to all interactive elements (buttons, textarea)
- Added role attributes (main, article, status, region)
- Added aria-live="polite" for dynamic content (typing indicator, loading)
- Added aria-pressed for toggle buttons
- Added sr-only class for screen-reader hints
- Pattern: Always include aria-label on buttons, aria-hidden on decorative icons

**Key Files Modified:**
- frontend/src/components/Chat.jsx (main component)
- frontend/src/App.css (loading screen styles, sr-only utility)
- frontend/.env.example (created)

**Technical Patterns:**
- Vite env vars require VITE_ prefix for client-side access
- Use import.meta.env, not process.env
- Always provide fallback values for env variables
- ARIA best practices: role, aria-label, aria-live, aria-hidden
