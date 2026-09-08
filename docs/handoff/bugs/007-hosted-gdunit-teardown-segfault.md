# 007: Hosted gdUnit teardown segfault after all Godot suites passed

## Cause

Not isolated. On the GitHub-hosted `push` Verify run for release merge `0547d06` (run 34055933382), every C# suite and all three gdUnit suites passed, then the Godot process running `GameplayShellTest.gd` died with `Segmentation fault (core dumped)` during "Run dispose test resources", so `verify.sh` exited 139.

The identical tree passed on the pull-request run two minutes later and under `rexec` locally, which points at Godot 4.7 headless shutdown on the hosted image rather than at repository code.

## Fix

No code change. The failed job was re-run with `gh run rerun <id> --failed` and passed. The release was already merged and tagged; the rerun restored a green Verify record for the `main` commit.

## Lesson

A 139 exit after every suite reports `0 failures` is a Godot teardown crash, not a test failure; read the last lines before the `##[error]` marker. Re-run once and record it. If it recurs, capture the runner's core dump or a `--verbose` Godot log and isolate it instead of re-running again.
