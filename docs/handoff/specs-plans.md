# Specifications and Plans

## Design entry points

- [Project design wiki](../wiki/README.md) is the single source of truth for design; `docs/design/` and `docs/specs/` are supporting detail it links.
- [Political model](../wiki/factions-and-organizations.md) records approved conceptual decisions; no political runtime is implemented.
- [Faction assignment](../wiki/faction-intent-and-autonomous-assignment.md) is the approved next design: six decisions and a bounded first M5 proof, not implemented.
- [Open questions](../wiki/open-questions.md) records scoped resolutions and deferred refinements; it does not silently admit additional gameplay work.

## Next implementation boundary

- Admit a subsequent implementation feature against the approved faction contract; current documentation work does not start runtime implementation.
- Preserve direct control, narrow administrative knowledge, Ship/Faction scheduling, typed bootstrap, no-invention V7 migration, and no randomness or political UI.
- Runtime remains v0.5.0/content V4/save V6. M3 and M5 are incomplete; external intelligence sharing and campaign era remain deferred.

## Tracked artifacts

| Artifact | Status | Purpose |
| --- | --- | --- |
| Feature #36 | Merged / Done | Milestone 1 world-state bootstrap; Final PR #37 merged as `eb3c976` after all five hosted checks passed. |
| Feature #38 | Merged / Done | Milestone 2 active-world orders; Final PR #39 merged as `fba9b438` into `dev`. |
| Task #40 | Merged / Done | Consolidated the Milestone 1-2 gameplay shell; Final PR #41 merged as `ed8ca4b` into `dev`. |
| Task #42 | Merged / Done | Released source-only v0.2.0; Final PR #43 merged as `163b8e2` into `main`, then PR #44 synced `main` to `dev`. |
| Task #45 | Merged / Done | Tracked owner-supplied non-runtime command-shell mockups; Final PR #46 merged as `8d3876b` into `dev`. |
| Feature #47 | Merged / Done | Command Deck and Engineering UI merged through Final PR #48 as `b80c669` into `dev` and shipped in source-only v0.3.0. |
| Bug #49 | Merged / Done | Clean-launch asset import repair merged through Final PR #50 as `2064822`; launcher imports assets before the Command Deck loads. |
| Bug #51 | Merged / Done | Live Command Deck action stability repair merged through Final PR #52 as `c5602cc` into `dev`. |
| Task #53 | Merged / Done | Source-only v0.3.0 release Final PR #54 merged as `fae21bd` into `main`; supporting PR #55 restored release ancestry to `dev`. |
| Task #56 | Merged / Done | Project-scoped Figma tooling merged through Final PR #57 as `24cf7b0` into `dev`; it did not change gameplay. |
| Feature #58 | Merged / Done | Milestone 3A merged to `dev` through Final PR #61 as squash `a104e3f`; released in v0.4.0. |
| Task #59 | Merged / Done | Godot UID and import metadata merged through Final PR #60 as `ce454f5` into `dev`. |
| Feature #62 | Merged / Done | Milestone 4 Engineering merged through Final PR #63 as `0f2278e` into `dev`; released in v0.4.0. |
| Task #64 | Merged / Done | v0.4.0 Final PR #66 released as `b3b6635`; sync PR #67 put the release ancestry on `dev` at `2edd194`. |
| Task #68 | Merged / Done | Project design wiki consolidated under `docs/wiki/` through Final PR #69 as `75ebb55` into `dev`. |
| Task #70 | Merged / Done | Design wiki declared the single source of truth for design; Final PR #71 merged as `1b8eec4` into `dev`. |
| Feature #77 | Merged / Done | Strategic Contact Reporting merged through Final PR #78 as squash `80c3084` into `dev`; released in v0.5.0. |
| Task #80 | Merged / Done | v0.5.0 Final PR #82 released as `0547d06`; sync PR #83 put the release ancestry on `dev` at `e761249`. |
| Faction assignment wiki contract | Approved design / Not implemented | D-08 through D-13 record the owner's six decisions; the runtime feature follows documentation. |
