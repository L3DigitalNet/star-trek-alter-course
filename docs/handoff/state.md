# Session State

## Current focus

- Bug #141 is Done; PR #144 merged as `98d2e70`. Post-merge Verify `37106769083` passed.
- The full gameplay-semantic sweep (last 2026-09-26) is due 2026-10-03 and is NOT yet performed; do it next.
- No release, tag, or main promotion. V6, V1, and V10 remain unreleased; M6 is partial. Core 1411, AssetCtl 530.
- Stryker #3878: the 109 MB test was not the coverage cost; PR #146 reverted its exclusion. Full Core mutation run takes 68-77 min.

## Active incidents

- No active outage; the 2026-10-01 read-only Btrfs incident is resolved (`/` and `/home` rw).
- Bug #139 (pre-push historical-name deletion) awaits owner disposition; no enforcement code changed.
