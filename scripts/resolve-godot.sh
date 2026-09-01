#!/usr/bin/env bash
# Print the path to the exact Godot 4.7.2 .NET editor, downloading the official
# Linux x86_64 artifact into the user cache only when no matching editor exists.
# Requirements: Bash, curl, sha256sum, unzip, and a 64-bit x86 Linux host.

set -euo pipefail

readonly expected_version='4.7.2.stable.mono.official.ed1daf0bf'
readonly archive_sha='129f82db7bafd54ae14bb5bb284041c73860e8c7a009a3a026ca5e946cbff247'
readonly archive_url='https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_linux_x86_64.zip'
readonly cache_root="${XDG_CACHE_HOME:-${HOME}/.cache}/star-trek-alter-course/godot/4.7.2"
readonly archive_root="${cache_root}/Godot_v4.7.2-stable_mono_linux_x86_64"
readonly cached_binary="${archive_root}/Godot_v4.7.2-stable_mono_linux.x86_64"

is_expected_editor() {
  local candidate=$1
  [[ -x "${candidate}" ]] && [[ "$("${candidate}" --version)" == "${expected_version}" ]]
}

if [[ -n "${GODOT_BIN:-}" ]] && is_expected_editor "${GODOT_BIN}"; then
  printf '%s\n' "${GODOT_BIN}"
  exit 0
fi

if command -v godot > /dev/null 2>&1 && is_expected_editor "$(command -v godot)"; then
  command -v godot
  exit 0
fi

if is_expected_editor "${cached_binary}"; then
  printf '%s\n' "${cached_binary}"
  exit 0
fi

[[ "$(uname -s)" == 'Linux' && "$(uname -m)" == 'x86_64' ]] || {
  printf 'Godot bootstrap supports Linux x86_64 only.\n' >&2
  exit 1
}

mkdir -p "${cache_root}"
temp_dir="$(mktemp -d "${cache_root}/download.XXXXXX")"
trap 'rm -rf -- "${temp_dir}"' EXIT
curl --fail --location --silent --show-error "${archive_url}" --output "${temp_dir}/godot.zip"
printf '%s  %s\n' "${archive_sha}" "${temp_dir}/godot.zip" | sha256sum --check --status
unzip -q "${temp_dir}/godot.zip" -d "${temp_dir}/editor"
cp -a "${temp_dir}/editor/." "${cache_root}/"

is_expected_editor "${cached_binary}" || {
  printf 'Downloaded Godot editor did not report %s.\n' "${expected_version}" >&2
  exit 1
}

printf '%s\n' "${cached_binary}"
