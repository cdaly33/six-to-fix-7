# Session Log: Stack Simplification Discussion

**Session:** 2026-05-16T03:02:10Z  
**Summary:** Architectural assessment of tech stack proportionality for 2–5 concurrent user deployment

---

## Agents Consulted

- **Morpheus** (Architect): Full stack review  
- **Trinity** (Blazor): SignalR vs polling recommendation  
- **Neo** (Backend): SignalR removal implications + infrastructure assessment

---

## Key Decisions

### 1. SignalR Hub Removal → PeriodicTimer Polling

**Consensus:** Remove dedicated `AuditRunHub` at `/hubs/audit-run`. Replace with `PeriodicTimer` in `AuditDetail.razor`.

**Rationale:**
- Hub has 2 security defects (missing JWT token, missing tenant ownership check)
- Creates second WebSocket per user tab (unnecessary complexity at 2–5 users)
- Polling on Blazor Server incurs no extra browser HTTP/WebSocket
- ~1.7 DB queries/second during audit runs (negligible)
- UI latency: 3-second max stale state (acceptable for 30–90s audit runs)

**Preservation:** Keep `IRealtimeNotifier` / `AuditRunHubNotifier` abstraction in Infrastructure for future upgrade path if scale grows.

### 2. Azure AI Search Index Consolidation

**Consensus:** 
- **Keep:** `six-to-fix-evidence` (core product feature for semantic evidence retrieval)
- **Remove:** `six-to-fix-skill-outputs` (unimplemented, data in PostgreSQL)
- **Remove:** `six-to-fix-calibration` (duplicates `calibration_deltas` table)

**Rationale:** Removes dead-end specs, reduces Azure AI Search costs, relies on direct PostgreSQL queries for non-semantic data.

### 3. Infrastructure — No Changes Required

- **pgBouncer:** Keep (already designed around, no removal ROI)
- **Channel\<HubSpotEvent\>:** Keep (justifies async decoupling from publish response)
- **Redis:** N/A (not in stack, not needed)
- **AI Council, Polly, Auth, Vault, App Insights:** Keep (all proportionate or already designed around)

---

## Actions

**Immediate:**
1. Implement polling in `AuditDetail.razor` with `PeriodicTimer`
2. Disable hub connection in component (do not delete hub code)

**Near-term:**
1. Remove `six-to-fix-skill-outputs` index from Bicep
2. Remove `six-to-fix-calibration` index from Bicep
3. Add dedicated `GET /api/audit-runs/{id}/status` endpoint for polling progress

**Documentation:**
- Update ADR-004 if needed (SignalR now advisory, polling is the execution path)
- Document pgBouncer port split (5432 migrations, 6432 runtime)

---

## Outcome

Stack simplification validated. Two technologies (SignalR hub, unused search indexes) removed. Remaining architecture remains intact and correctly designed for scale. No Redis, no session cache, no distributed lock infrastructure needed.

Next: Implementation phase (Blazor polling component + backend endpoint).
