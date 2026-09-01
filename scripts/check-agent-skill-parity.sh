#!/usr/bin/env bash

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

skills=(
  godot-csharp
  godot-nodes-scenes
  godot-ui-control
  godot-signals-groups
  stac-architecture
)

for skill in "${skills[@]}"; do
  claude_dir=".claude/skills/${skill}"
  codex_dir=".codex/skills/${skill}"

  if ! diff --brief --recursive --no-dereference "${claude_dir}" "${codex_dir}"; then
    printf 'Agent skill parity failed for %s.\n' "${skill}" >&2
    exit 1
  fi
done

printf 'Agent skill parity passed for %d skills.\n' "${#skills[@]}"
