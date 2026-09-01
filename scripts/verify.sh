#!/usr/bin/env bash
# Run the canonical, read-only quality gate used by developers, agents, and CI.
# Requirements: Git, .NET SDK 10.0.111, and the Linux x86_64 bootstrap tools
# named by restore-native-tools.sh and resolve-godot.sh.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

tracked_state() {
  {
    git diff --no-ext-diff --binary
    git diff --no-ext-diff --binary --cached
  } | sha256sum | cut -d' ' -f1
}

before_state="$(tracked_state)"
readonly before_state
dotnet_dir="$(./scripts/resolve-dotnet.sh)"
readonly dotnet_dir
tool_bin="$(./scripts/restore-native-tools.sh)"
readonly tool_bin
godot_bin="$(./scripts/resolve-godot.sh)"
readonly godot_bin
readonly godot_project='src/AlterCourse.Godot'

export PATH="${dotnet_dir}:${tool_bin}:${PATH}"

./scripts/check-agent-skill-parity.sh

mapfile -d '' structured_files < <(
  git ls-files -z -- \
    '*.md' '*.json' '*.jsonc' '*.yml' '*.yaml' \
    ':(exclude).agents/skills/agent-handoff/**' \
    ':(exclude).claude/skills/agent-handoff/**'
)
mapfile -d '' markdown_files < <(
  git ls-files -z -- \
    '*.md' \
    ':(exclude).agents/skills/agent-handoff/**' \
    ':(exclude).claude/skills/agent-handoff/**'
)

dotnet tool restore
dotnet restore AlterCourse.sln --locked-mode
dotnet csharpier check .
npx --yes prettier@3.9.6 --check -- "${structured_files[@]}"
npx --yes markdownlint-cli2@0.23.1 "${markdown_files[@]}"
shfmt -d -i 2 -ci -sr .githooks/* scripts/*.sh
shellcheck .githooks/* scripts/*.sh
actionlint
gitleaks git --config .gitleaks.toml --redact --no-banner
gitleaks dir . --config .gitleaks.toml --redact --no-banner
./scripts/check-policy.sh
./scripts/test-branch-policy.sh
dotnet build AlterCourse.sln -c Release --no-restore --warnaserror
dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj -c Release --no-build --no-restore

"${godot_bin}" --headless --path "${godot_project}" --editor --quit-after 2
"${godot_bin}" \
  --headless \
  --path "${godot_project}" \
  --script res://addons/gdUnit4/bin/GdUnitCmdTool.gd \
  --ignoreHeadlessMode \
  -a res://tests/IntegrationProbeTest.gd \
  -rd .godot/gdunit-reports
"${godot_bin}" --headless --path "${godot_project}" res://tests/SmokeRunner.tscn

after_state="$(tracked_state)"
readonly after_state
if [[ "${before_state}" != "${after_state}" ]]; then
  printf 'verify.sh modified tracked repository content.\n' >&2
  exit 1
fi

printf 'Canonical verification passed.\n'
