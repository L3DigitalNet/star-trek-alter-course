#!/usr/bin/env bash
# Enforce the strict compiler properties and reject unreviewed diagnostic
# suppression in repository-owned C# and build configuration.
# Requirements: Bash, Git, grep, and the repository suppression allowlist.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

./scripts/branch-policy.sh repository

require_property() {
  local property=$1
  local value=$2
  grep -Eq "<${property}>[[:space:]]*${value}[[:space:]]*</${property}>" Directory.Build.props || {
    printf 'Directory.Build.props must set %s to %s.\n' "${property}" "${value}" >&2
    exit 1
  }
}

require_property Nullable enable
require_property TreatWarningsAsErrors true
require_property EnforceCodeStyleInBuild true
require_property AnalysisLevel latest-recommended

mapfile -d '' owned_files < <(
  git ls-files --cached --others --exclude-standard -z -- \
    '*.cs' '*.csproj' '*.props' '*.targets' '.editorconfig' 'src/**/.editorconfig' \
    ':(exclude)src/AlterCourse.Godot/addons/gdUnit4/**'
)

for file in "${owned_files[@]}"; do
  case "${file}" in
    *.cs)
      if grep -En '#pragma[[:space:]]+warning[[:space:]]+disable|SuppressMessage[[:space:]]*\(' "${file}"; then
        printf 'Unauthorized source-level diagnostic suppression in %s.\n' "${file}" >&2
        exit 1
      fi
      ;;
    *.csproj | *.props | *.targets)
      if grep -En '<NoWarn>|<WarningsNotAsErrors>' "${file}"; then
        printf 'Unauthorized MSBuild diagnostic suppression in %s.\n' "${file}" >&2
        exit 1
      fi
      ;;
  esac
done

while IFS=: read -r file line content; do
  diagnostic="$(sed -E 's/.*dotnet_diagnostic\.([A-Za-z0-9]+)\.severity[[:space:]]*=[[:space:]]*none.*/\1/' <<< "${content}")"
  if ! grep -Fqx "editorconfig|${file}|${diagnostic}|$(grep -F "editorconfig|${file}|${diagnostic}|" config/diagnostic-suppressions.allowlist | cut -d'|' -f4-)" config/diagnostic-suppressions.allowlist; then
    printf 'Unauthorized analyzer suppression %s in %s:%s.\n' "${diagnostic}" "${file}" "${line}" >&2
    exit 1
  fi
done < <(grep -HnE 'dotnet_diagnostic\.[A-Za-z0-9]+\.severity[[:space:]]*=[[:space:]]*none' .editorconfig src/AlterCourse.Godot/.editorconfig)
