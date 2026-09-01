#!/usr/bin/env bash
# Apply deterministic repository-owned formatting only.
# Requirements: .NET SDK 10.0.111 plus curl, sha256sum, tar, and xz support.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

dotnet_dir="$(./scripts/resolve-dotnet.sh)"
readonly dotnet_dir
export PATH="${dotnet_dir}:${PATH}"

dotnet tool restore
tool_bin="$(./scripts/restore-native-tools.sh)"

dotnet csharpier format .
"${tool_bin}/shfmt" -w -i 2 -ci -sr scripts/*.sh
