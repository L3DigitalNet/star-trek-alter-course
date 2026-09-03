#!/usr/bin/env bash
# Exercise launch-game.sh in a disposable repository-shaped fixture. The fake
# tools make command order and argument boundaries observable without restoring,
# building, or opening the real game.
# Requirements: Bash, coreutils, and diff; the fixture is removed on every exit
# so this test cannot leave generated project content behind.

set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly root
fixture="$(mktemp -d)"
readonly fixture
trap 'rm -rf -- "${fixture}"' EXIT

fail() {
  printf 'test-launch-game.sh: %s\n' "$*" >&2
  exit 1
}

write_fixture() {
  mkdir -p "${fixture}/scripts" "${fixture}/fake-dotnet" "${fixture}/fake-godot" \
    "${fixture}/src/AlterCourse.Godot"
  cp "${root}/scripts/launch-game.sh" "${fixture}/scripts/launch-game.sh"

  cat > "${fixture}/scripts/resolve-dotnet.sh" << 'EOF'
#!/usr/bin/env bash
printf '%s\n' "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../fake-dotnet" && pwd -P)"
EOF
  cat > "${fixture}/scripts/resolve-godot.sh" << 'EOF'
#!/usr/bin/env bash
fixture_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
printf '%s\n' "${fixture_root}/fake-godot/godot"
EOF
  cat > "${fixture}/fake-dotnet/dotnet" << 'EOF'
#!/usr/bin/env bash
printf '%s\n' "$*" >>"${LAUNCH_LOG}"
if [[ "${FAIL_DOTNET_STAGE:-}" == "$1" ]]; then
  exit 19
fi
EOF
  cat > "${fixture}/fake-godot/godot" << 'EOF'
#!/usr/bin/env bash
stage=launch
for argument in "$@"; do
  if [[ "${argument}" == '--import' ]]; then
    stage=import
    break
  fi
done
printf '%s\n' "$@" >"${GODOT_ARGS_PREFIX}${stage}.args"
printf 'godot-%s\n' "${stage}" >>"${LAUNCH_LOG}"
if [[ "${FAIL_GODOT_STAGE:-}" == "${stage}" ]]; then
  exit 23
fi
EOF
  chmod +x "${fixture}/scripts/launch-game.sh" "${fixture}/scripts/resolve-dotnet.sh" \
    "${fixture}/scripts/resolve-godot.sh" "${fixture}/fake-dotnet/dotnet" \
    "${fixture}/fake-godot/godot"
}

assert_file() {
  local expected=$1
  local actual=$2
  diff -u "${expected}" "${actual}" || fail "unexpected contents in ${actual}"
}

write_fixture

success_log="${fixture}/success.log"
success_args_prefix="${fixture}/success-"
LAUNCH_LOG="${success_log}" GODOT_ARGS_PREFIX="${success_args_prefix}" \
  "${fixture}/scripts/launch-game.sh" --editor --rendering-driver opengl3 -- 'argument with spaces' --application-flag

cat > "${fixture}/expected-success.log" << 'EOF'
restore AlterCourse.sln --locked-mode
build src/AlterCourse.Godot/AlterCourse.Godot.csproj -c Debug --no-restore --warnaserror
godot-import
godot-launch
EOF
cat > "${fixture}/expected-success-import.args" << 'EOF'
--headless
--path
src/AlterCourse.Godot
--import
EOF
cat > "${fixture}/expected-success-launch.args" << 'EOF'
--path
src/AlterCourse.Godot
--editor
--rendering-driver
opengl3
--
argument with spaces
--application-flag
EOF
assert_file "${fixture}/expected-success.log" "${success_log}"
assert_file "${fixture}/expected-success-import.args" "${success_args_prefix}import.args"
assert_file "${fixture}/expected-success-launch.args" "${success_args_prefix}launch.args"

restore_log="${fixture}/restore-failure.log"
if LAUNCH_LOG="${restore_log}" GODOT_ARGS_PREFIX="${fixture}/restore-failure-" FAIL_DOTNET_STAGE=restore \
  "${fixture}/scripts/launch-game.sh"; then
  fail 'launcher succeeded after restore failed'
fi
[[ ! -e "${fixture}/restore-failure-import.args" ]] || fail 'Godot imported after restore failed'
[[ ! -e "${fixture}/restore-failure-launch.args" ]] || fail 'Godot launched after restore failed'
cat > "${fixture}/expected-restore-failure.log" << 'EOF'
restore AlterCourse.sln --locked-mode
EOF
assert_file "${fixture}/expected-restore-failure.log" "${restore_log}"

build_log="${fixture}/build-failure.log"
if LAUNCH_LOG="${build_log}" GODOT_ARGS_PREFIX="${fixture}/build-failure-" FAIL_DOTNET_STAGE=build \
  "${fixture}/scripts/launch-game.sh"; then
  fail 'launcher succeeded after build failed'
fi
[[ ! -e "${fixture}/build-failure-import.args" ]] || fail 'Godot imported after build failed'
[[ ! -e "${fixture}/build-failure-launch.args" ]] || fail 'Godot launched after build failed'
cat > "${fixture}/expected-build-failure.log" << 'EOF'
restore AlterCourse.sln --locked-mode
build src/AlterCourse.Godot/AlterCourse.Godot.csproj -c Debug --no-restore --warnaserror
EOF
assert_file "${fixture}/expected-build-failure.log" "${build_log}"

import_log="${fixture}/import-failure.log"
if LAUNCH_LOG="${import_log}" GODOT_ARGS_PREFIX="${fixture}/import-failure-" FAIL_GODOT_STAGE=import \
  "${fixture}/scripts/launch-game.sh"; then
  fail 'launcher succeeded after import failed'
fi
[[ -e "${fixture}/import-failure-import.args" ]] || fail 'Godot import did not run'
[[ ! -e "${fixture}/import-failure-launch.args" ]] || fail 'Godot launched after import failed'
cat > "${fixture}/expected-import-failure.log" << 'EOF'
restore AlterCourse.sln --locked-mode
build src/AlterCourse.Godot/AlterCourse.Godot.csproj -c Debug --no-restore --warnaserror
godot-import
EOF
assert_file "${fixture}/expected-import-failure.log" "${import_log}"

printf 'launch-game.sh behavior tests passed.\n'
