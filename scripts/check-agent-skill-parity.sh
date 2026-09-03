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
  figma-code-connect
  figma-create-new-file
  figma-design-to-code
  figma-generate-design
  figma-generate-diagram
  figma-generate-library
  figma-generative-plugins
  figma-implement-motion
  figma-shaders
  figma-swiftui
  figma-use
  figma-use-figjam
  figma-use-motion
  figma-use-slides
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
