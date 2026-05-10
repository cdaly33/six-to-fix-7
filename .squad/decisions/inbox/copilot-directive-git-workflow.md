### 2026-05-10T21:54:40Z: User directive — Git workflow
**By:** Chris (via Copilot)
**What:** All team members must work on dev/feature branches with regular commits, then open a PR to merge into main. No direct commits to main.
**Why:** User request — captured for team memory. Applies to all agents including Scribe.
**Impact:**
- Scribe: commit Phase 1 work to a dev/phase-1-foundation branch, open PR to main
- All future phases: each agent works on a named branch, commits regularly, merges via PR
- Branch naming convention: dev/phase-{N}-{slug} or eat/{slug} per phase/feature
- Tank: ensure ci.yml branch protection rules reflect this (no direct push to main)
