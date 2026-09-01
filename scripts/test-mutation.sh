#!/usr/bin/env bash
# Run the intentionally slow mutation suite against the pure simulation project.
# Requirements: .NET SDK 10.0.111 and restored repository-local .NET tools.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

dotnet_dir="$(./scripts/resolve-dotnet.sh)"
readonly dotnet_dir
export PATH="${dotnet_dir}:${PATH}"

dotnet tool restore
dotnet restore AlterCourse.sln --locked-mode
dotnet stryker --config-file stryker-config.json
