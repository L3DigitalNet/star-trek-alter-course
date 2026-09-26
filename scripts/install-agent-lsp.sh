#!/usr/bin/env bash
# Install standalone upstream language tooling for this checkout's CLI agents.
# Requires Linux x86_64, Bash, Node >=18, curl, tar, sha256sum, and the existing
# SDK/editor resolvers. Downloads never depend on VS Code or change CLI globals.
# The cclsp npm artifact includes its dependencies as one bundled Node program.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly root
readonly install_dir="${root}/.tools/agent-lsp"
readonly roslyn_version='5.12.0-1.26426.8'
readonly cclsp_version='0.7.0'
readonly cclsp_sha='bf43194c11b71cd58bf95fd7da8ee995dc45fa374758ec07cad8c1e2d8bca597'

[[ "$(uname -s)" == Linux && "$(uname -m)" == x86_64 ]] || {
  printf 'Agent language tooling currently supports Linux x86_64.\n' >&2
  exit 1
}
node -e 'if (Number(process.versions.node.split(".")[0]) < 18) process.exit(1)'
mkdir -p "${install_dir}"
cd "${root}"
dotnet_root="$(./scripts/resolve-dotnet.sh)"
godot_binary="$(./scripts/resolve-godot.sh)"
readonly dotnet_root godot_binary
export DOTNET_ROOT="${dotnet_root}"
export PATH="${dotnet_root}:${PATH}"

if [[ -x "${install_dir}/roslyn/roslyn-language-server" ]]; then
  dotnet tool update roslyn-language-server --tool-path "${install_dir}/roslyn" \
    --version "${roslyn_version}" --source https://api.nuget.org/v3/index.json
else
  dotnet tool install roslyn-language-server --tool-path "${install_dir}/roslyn" \
    --version "${roslyn_version}" --source https://api.nuget.org/v3/index.json
fi

staging="$(mktemp -d "${install_dir}/download.XXXXXX")"
trap 'rm -rf -- "${staging}"' EXIT
curl --fail --location --silent --show-error --retry 3 \
  "https://registry.npmjs.org/cclsp/-/cclsp-${cclsp_version}.tgz" --output "${staging}/cclsp.tgz"
printf '%s  %s\n' "${cclsp_sha}" "${staging}/cclsp.tgz" | sha256sum --check --status
mkdir -p "${staging}/cclsp"
tar -xzf "${staging}/cclsp.tgz" -C "${staging}/cclsp"
mkdir -p "${install_dir}/cclsp"
cp -a "${staging}/cclsp/." "${install_dir}/cclsp/"

node --input-type=module - "${install_dir}" "${dotnet_root}" "${godot_binary}" << 'NODE'
import { writeFileSync } from 'node:fs';
import path from 'node:path';
const [directory, dotnetRoot, godotBinary] = process.argv.slice(2);
writeFileSync(path.join(directory, 'runtime.json'), JSON.stringify({ dotnetRoot, godotBinary }, null, 2) + '\n');
NODE
printf 'Installed Roslyn %s and cclsp %s in %s\n' "${roslyn_version}" "${cclsp_version}" "${install_dir}"
printf 'Godot: %s\nRestart Codex CLI and Claude Code to load the project MCP entries.\n' "${godot_binary}"
