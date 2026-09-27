#!/usr/bin/env bash
# Compose package-owned relationship and Ready checks for standalone/<slug> PRs.
# Requirements: Bash, Git, jq, mktemp, gh with read access to the target PR, and
# the base commit's installed gh-workflow package. PR text remains JSON data,
# never shell code; the proposed head cannot replace the package judging it.
# Semantic bounded low-risk eligibility remains the ADR 0013 reviewer judgment;
# this gate verifies the package contract without inventing a local risk taxonomy.

set -euo pipefail

(($# == 5)) || exit 2
readonly repository=$1 number=$2 head_sha=$3 head_branch=$4 base_sha=$5
[[ "${repository}" =~ ^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$ && "${number}" =~ ^[1-9][0-9]*$ && "${head_sha}" =~ ^[a-f0-9]{40}$ && "${base_sha}" =~ ^[a-f0-9]{40}$ ]] || exit 2
root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"
package_root="$(mktemp -d)"
readonly package_root
# SCOPE: only the temporary directory returned by mktemp is removed.
trap 'rm -rf -- "${package_root}"' EXIT
git show "${base_sha}:.agents/skills/github-workflow/bin/gh-workflow" > "${package_root}/gh-workflow"
git show "${base_sha}:.standards/packages/github-workflow/policy.toml" > "${package_root}/policy.toml"
git show "${base_sha}:.agents/skills/github-workflow/references/org-schema.yaml" > "${package_root}/org-schema.yaml"
chmod +x "${package_root}/gh-workflow"
readonly package="${package_root}/gh-workflow"

snapshot() {
  gh api "repos/${repository}/pulls/${number}" | jq -cSe --slurp \
    --arg repository "${repository}" --argjson number "${number}" \
    --arg head "${head_sha}" --arg branch "${head_branch}" '
      select(length == 1) | .[0] |
      select(.number == $number and .state == "open" and
        .head.sha == $head and .head.ref == $branch and
        .base.repo.full_name == $repository and .base.ref == "dev" and
        (.body | type) == "string" and (.draft | type) == "boolean") |
      {number, state, body, draft, head: .head, base: .base, updated_at, auto_merge}'
}

# The package's v1 envelope does not carry a head SHA. Bracket both package reads
# with live PR snapshots so a changed contract or head cannot reuse this verdict.
before="$(snapshot)"
receipt="$("${package}" receipt --repo "${repository}" --pr "${number}" --policy "${package_root}/policy.toml" --schema "${package_root}/org-schema.yaml" --output json)"
# A ready PR's receipt includes Merge findings, including this still-running CI
# check. Only the explicit Ready verdict below admits the contract; requiring a
# clear Merge receipt here would make the required check depend on itself.
jq -e --slurp --arg repository "${repository}" --argjson number "${number}" '
  select(length == 1) | .[0] |
  .schema_version == "1" and .command == "receipt" and
  ((.result == "clear" and .findings == []) or
    (.result == "domain-finding" and (.findings | type) == "array" and
      (.findings | length) > 0 and all(.findings[]; .phase == "merge"))) and
  .target.kind == "pull_request" and .target.repository == $repository and
  .target.number == $number and (.steps | type) == "array" and
  (.gaps | type) == "array" and
  .item.kind == "pull_request" and .item.number == $number and
  .item.state == "open" and .item.merged != true and .item.relationship == "Standalone" and
  ((.item.governing_issue // 0) == 0)
' <<< "${receipt}" > /dev/null
check="$("${package}" check --repo "${repository}" --pr "${number}" --policy "${package_root}/policy.toml" --schema "${package_root}/org-schema.yaml" --through ready --output json)"
jq -e --slurp --arg repository "${repository}" --argjson number "${number}" '
  select(length == 1) | .[0] |
  .schema_version == "1" and .command == "check" and .result == "clear" and
  .gate == "ready" and .target.kind == "pull_request" and
  .target.repository == $repository and .target.number == $number and .findings == [] and
  (.steps | type) == "array"
' <<< "${check}" > /dev/null
after="$(snapshot)"
[[ "${before}" == "${after}" ]]
