# Project Tasks

## User tasks

- None currently.

## Agent tasks

- Merge Final PR #123 for [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) on green checks, then record the merge SHA.
- Do not start Damage Control or tactical refinement until PR #123 merges; that work is separately governed and not automatic.
- Preserve the bounded faction slice; do not expand into deferred intelligence, organizations, hierarchy, RNG, or political UI.
- Treat Q-02/Q-03 and the unresolved portion of Q-04 as future design work. Keep supported historical migrations noninventive.
- If the hosted Godot teardown segfault ([bug 007](handoff/bugs/007-hosted-gdunit-teardown-segfault.md)) recurs, isolate it rather than rerunning.
- If the remote Godot first-run import segfault or a gdUnit missing-imported-fonts failure recurs, isolate the cause; both cleared on rerun once each.
