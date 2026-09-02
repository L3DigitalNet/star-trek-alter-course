#!/usr/bin/env bash
# Launch the Godot project only after its locked dependencies and Debug editor
# assembly are current. Usage: scripts/launch-game.sh [Godot engine args] [-- application args].
# Requirements: Bash plus the repository resolvers; they pin the SDK and editor
# so this launcher never silently uses a system toolchain with different output.

set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly root
cd "${root}"

readonly godot_project='src/AlterCourse.Godot'
dotnet_dir="$(./scripts/resolve-dotnet.sh)"
readonly dotnet_dir
export PATH="${dotnet_dir}:${PATH}"

# Build follows the locked restore: allowing an editor launch after either step
# fails would run stale managed code that no longer represents the source tree.
dotnet restore AlterCourse.sln --locked-mode
dotnet build "${godot_project}/AlterCourse.Godot.csproj" -c Debug --no-restore --warnaserror

godot_bin="$(./scripts/resolve-godot.sh)"
readonly godot_bin
exec "${godot_bin}" --path "${godot_project}" "$@"
