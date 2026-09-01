#!/usr/bin/env bash
# Resolve the exact repository SDK without changing system-wide .NET installations.

set -euo pipefail

readonly sdk_version='10.0.111'
readonly sdk_sha256='6d1aa7e62438957580a5e0bc4a1598439abe54b3855c1b88db8b702de2eca5cf'
readonly sdk_url="https://builds.dotnet.microsoft.com/dotnet/Sdk/${sdk_version}/dotnet-sdk-${sdk_version}-linux-x64.tar.gz"
readonly runtime_version='8.0.30'
readonly runtime_sha256='253a45b29374f6fe45631ad5909f90a26d77ef1732b7518aedcf4f3ff4dfb465'
readonly runtime_url="https://builds.dotnet.microsoft.com/dotnet/Runtime/${runtime_version}/dotnet-runtime-${runtime_version}-linux-x64.tar.gz"

has_expected_runtime() {
  local dotnet_command=$1
  "${dotnet_command}" --list-runtimes | grep -Fq "Microsoft.NETCore.App ${runtime_version} "
}

if command -v dotnet > /dev/null 2>&1 &&
  [[ "$(dotnet --version)" == "${sdk_version}" ]] &&
  has_expected_runtime "$(command -v dotnet)"; then
  dirname "$(command -v dotnet)"
  exit 0
fi

if [[ "$(uname -s)" != 'Linux' || "$(uname -m)" != 'x86_64' ]]; then
  printf 'The repository SDK bootstrap supports Linux x86_64 only.\n' >&2
  exit 1
fi

readonly cache_root="${XDG_CACHE_HOME:-${HOME}/.cache}/star-trek-alter-course/dotnet"
readonly install_dir="${cache_root}/${sdk_version}"
readonly dotnet_bin="${install_dir}/dotnet"

if [[ ! -x "${dotnet_bin}" ]]; then
  archive="$(mktemp)"
  readonly archive
  staging="$(mktemp -d)"
  readonly staging
  trap 'rm -f -- "${archive}"; rm -rf -- "${staging}"' EXIT

  curl --fail --location --retry 3 --output "${archive}" "${sdk_url}"
  printf '%s  %s\n' "${sdk_sha256}" "${archive}" | sha256sum --check --status
  tar -xzf "${archive}" -C "${staging}"
  mkdir -p "${cache_root}"
  mv "${staging}" "${install_dir}"
fi

if ! has_expected_runtime "${dotnet_bin}"; then
  runtime_archive="$(mktemp)"
  readonly runtime_archive
  trap 'rm -f -- "${runtime_archive}"' EXIT

  curl --fail --location --retry 3 --output "${runtime_archive}" "${runtime_url}"
  printf '%s  %s\n' "${runtime_sha256}" "${runtime_archive}" | sha256sum --check --status
  tar -xzf "${runtime_archive}" -C "${install_dir}"
fi

if [[ "$("${dotnet_bin}" --version)" != "${sdk_version}" ]]; then
  printf 'Resolved dotnet does not report SDK %s.\n' "${sdk_version}" >&2
  exit 1
fi

if ! has_expected_runtime "${dotnet_bin}"; then
  printf 'Resolved dotnet does not provide Microsoft.NETCore.App %s.\n' "${runtime_version}" >&2
  exit 1
fi

printf '%s\n' "${install_dir}"
