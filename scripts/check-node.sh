#!/usr/bin/env bash
# Validate the Node runtime contract used by repository-owned npm tooling.
# Requirements: Bash, Git, Node, and npx on PATH; this script does not install them.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
readonly version_file="${root}/.node-version"

if [[ ! -r "${version_file}" ]]; then
  printf 'Missing Node version contract: %s\n' "${version_file}" >&2
  exit 1
fi

required_major="$(< "${version_file}")"
readonly required_major
if [[ ! "${required_major}" =~ ^[0-9]+$ ]]; then
  printf '.node-version must contain one Node major version.\n' >&2
  exit 1
fi

if ! command -v node > /dev/null 2>&1 || ! command -v npx > /dev/null 2>&1; then
  printf 'Node %s and npx are required for repository tooling.\n' "${required_major}" >&2
  exit 1
fi

actual_version="$(node --version)"
readonly actual_version
actual_major="${actual_version#v}"
actual_major="${actual_major%%.*}"
readonly actual_major
if [[ "${actual_major}" != "${required_major}" ]]; then
  printf 'Node %s is required; found %s.\n' "${required_major}" "${actual_version}" >&2
  exit 1
fi

printf 'Node runtime contract satisfied: %s.\n' "${actual_version}"
