#!/usr/bin/env bash
# Exercise resolve-dotnet.sh's .dotnet link in a disposable repository-shaped
# fixture, and prove global.json searches that link for the resolver's pinned SDK.
# Fake dotnet hosts and a pre-seeded cache keep this offline and independent of
# whichever system SDK is installed.
# Requirements: Bash, coreutils, and Node; the fixture is removed on every exit.

set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly root
fixture="$(mktemp -d)"
readonly fixture
trap 'rm -rf -- "${fixture}"' EXIT

fail() {
  printf 'test-resolve-dotnet.sh: %s\n' "$*" >&2
  exit 1
}

# The resolver owns the pinned versions; reading them here keeps this test from
# silently validating a stale copy after an SDK bump.
sdk_version="$(sed -nE "s/^readonly sdk_version='([^']+)'$/\1/p" "${root}/scripts/resolve-dotnet.sh")"
readonly sdk_version
runtime_version="$(sed -nE "s/^readonly runtime_version='([^']+)'$/\1/p" "${root}/scripts/resolve-dotnet.sh")"
readonly runtime_version
[[ -n "${sdk_version}" && -n "${runtime_version}" ]] || fail 'could not read pinned versions'

mapfile -t sdk_fields < <(
  node -e '
    const sdk = require(process.argv[1]).sdk;
    for (const value of [sdk.version, sdk.rollForward, JSON.stringify(sdk.paths), sdk.errorMessage]) {
      console.log(String(value));
    }
  ' "${root}/global.json"
)
readonly expected_paths="[\".dotnet\",\"\$host\$\"]"
[[ "${sdk_fields[0]}" == "${sdk_version}" ]] ||
  fail "global.json pins ${sdk_fields[0]} but the resolver installs ${sdk_version}"
[[ "${sdk_fields[1]}" == 'disable' ]] || fail 'global.json rollForward must stay disable'
[[ "${sdk_fields[2]}" == "${expected_paths}" ]] ||
  fail "global.json paths must be ${expected_paths}, not ${sdk_fields[2]}"
[[ "${sdk_fields[3]}" == *scripts/resolve-dotnet.sh* ]] ||
  fail 'global.json errorMessage must name the resolver'

mkdir -p "${fixture}/repo/scripts" "${fixture}/system-bin"
cp "${root}/scripts/resolve-dotnet.sh" "${fixture}/repo/scripts/resolve-dotnet.sh"

# A drifted system host must not satisfy the fast path.
cat > "${fixture}/system-bin/dotnet" << 'EOF'
#!/usr/bin/env bash
case "$1" in
  --version) printf '10.0.999\n' ;;
  --list-runtimes) printf 'Microsoft.NETCore.App 8.0.999 [/usr/lib]\n' ;;
esac
EOF
# Any download attempt means the seeded cache was not recognized.
cat > "${fixture}/system-bin/curl" << 'EOF'
#!/usr/bin/env bash
printf 'unexpected download\n' >&2
exit 97
EOF

install_dir="${fixture}/cache/star-trek-alter-course/dotnet/${sdk_version}"
readonly install_dir
mkdir -p "${install_dir}"
cat > "${install_dir}/dotnet" << EOF
#!/usr/bin/env bash
case "\$1" in
  --version) printf '%s\n' '${sdk_version}' ;;
  --list-runtimes) printf 'Microsoft.NETCore.App %s [%s]\n' '${runtime_version}' '${install_dir}/shared' ;;
esac
EOF
chmod +x "${fixture}/repo/scripts/resolve-dotnet.sh" "${fixture}/system-bin/dotnet" \
  "${fixture}/system-bin/curl" "${install_dir}/dotnet"

run_resolver() {
  PATH="${fixture}/system-bin:${PATH}" XDG_CACHE_HOME="${fixture}/cache" \
    "${fixture}/repo/scripts/resolve-dotnet.sh"
}

readonly sdk_link="${fixture}/repo/.dotnet"

output="$(run_resolver)"
[[ "${output}" == "${install_dir}" ]] || fail "resolver printed '${output}'"
[[ -L "${sdk_link}" ]] || fail '.dotnet link was not created'
[[ "$(readlink -- "${sdk_link}")" == "${install_dir}" ]] || fail '.dotnet link has the wrong target'

# A link left by an earlier pin is retargeted rather than trusted.
ln -sfn -- "${fixture}/stale-sdk" "${sdk_link}"
run_resolver > /dev/null
[[ "$(readlink -- "${sdk_link}")" == "${install_dir}" ]] || fail 'stale .dotnet link was not replaced'

rm -- "${sdk_link}"
mkdir -- "${sdk_link}"
touch -- "${sdk_link}/owned-by-someone-else"
if run_resolver > /dev/null 2>&1; then
  fail 'resolver succeeded over a real .dotnet directory'
fi
[[ -f "${sdk_link}/owned-by-someone-else" ]] || fail 'resolver modified a real .dotnet directory'

printf 'resolve-dotnet.sh behavior tests passed.\n'
