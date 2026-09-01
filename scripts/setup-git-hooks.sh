#!/usr/bin/env bash
# Configure this checkout to use the repository-owned Git hooks and verify the
# tracked enforcement surface.
# Requirements: Bash and Git in a writable local checkout.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

git config --local core.hooksPath .githooks
./scripts/branch-policy.sh repository

configured_path="$(git config --local --get core.hooksPath)"
readonly configured_path
[[ "${configured_path}" == '.githooks' ]] || {
  printf 'Expected core.hooksPath=.githooks, got %s.\n' "${configured_path}" >&2
  exit 1
}

printf 'Configured repository Git hooks at .githooks.\n'
