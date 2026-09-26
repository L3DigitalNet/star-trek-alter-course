# Project Tasks

## User tasks

- None currently.

## Agent tasks

- Implement [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121), the ADR 0014 ship-system substrate migration, before additional systems or Damage Control gameplay. Follow the [owning contract](wiki/ship-system-substrate.md), not the superseded recovery prompt.
- Preserve canonical M6A behavior while replacing the fixed-field implementation through content, runtime, persistence, projections, and controls. Prove heterogeneous live loadouts and the documented conformance requirements.
- Preserve the bounded faction slice; do not expand into deferred intelligence, organizations, hierarchy, RNG, or political UI.
- Treat Q-02/Q-03 and the unresolved portion of Q-04 as future design work. Keep supported historical migrations noninventive.
- Investigate the nonfatal `grab_focus` diagnostic recorded in [PR #117](https://github.com/L3DigitalNet/star-trek-alter-course/pull/117) while changing controls; require reproduction and a correct-layer regression rather than suppression.
- If the hosted Godot teardown segfault ([bug 007](handoff/bugs/007-hosted-gdunit-teardown-segfault.md)) recurs, isolate it rather than rerunning.
