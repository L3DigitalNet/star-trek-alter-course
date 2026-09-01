#!/usr/bin/env bash
# Exercise positive and negative branch-policy behavior in an isolated Git
# repository so tests cannot mutate the authoritative checkout.
# Requirements: Bash, Git, mktemp, and standard GNU text utilities.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
readonly policy="${root}/scripts/branch-policy.sh"
fixture="$(mktemp -d)"
readonly fixture
# SCOPE: only the directory returned by mktemp is removed on exit.
trap 'rm -rf -- "${fixture}"' EXIT

expect_pass() {
  "$@" > /dev/null
}

expect_fail() {
  if "$@" > /dev/null 2>&1; then
    printf 'Expected command to fail:' >&2
    printf ' %q' "$@" >&2
    printf '\n' >&2
    exit 1
  fi
}

pre_push_record() {
  printf '%s %s %s %s\n' "$1" "$2" "$3" "$4" | "${policy}" pre-push
}

git -C "${fixture}" init -q -b dev
mkdir "${fixture}/no-hooks"
git -C "${fixture}" config core.hooksPath "${fixture}/no-hooks"
git -C "${fixture}" config user.name 'Branch Policy Test'
git -C "${fixture}" config user.email 'branch-policy@example.invalid'
git -C "${fixture}" config commit.gpgsign false
printf 'initial\n' > "${fixture}/README.md"
git -C "${fixture}" add README.md
git -C "${fixture}" commit -qm 'docs: initialize fixture'
base="$(git -C "${fixture}" rev-parse HEAD)"
git -C "${fixture}" update-ref refs/remotes/origin/main "${base}"

cd "${fixture}"

expect_pass "${policy}" branch task/9-branch-release-governance
expect_fail "${policy}" branch feature/no-issue
expect_pass "${policy}" pull-request dev task/9-branch-release-governance 'chore: enforce branch policy'
expect_pass "${policy}" pull-request main dev 'chore(release): v0.1.0'
expect_fail "${policy}" pull-request main dev 'chore(release): v0.1.0-rc.1'
expect_pass "${policy}" pull-request main hotfix/9-release-fix 'fix(release): correct package'
expect_pass "${policy}" pull-request dev main 'chore(sync): merge main into dev'
expect_fail "${policy}" pull-request main task/9-branch-release-governance 'chore: bypass release flow'
expect_fail "${policy}" pull-request dev hotfix/9-release-fix 'fix: skip hotfix release'
expect_fail "${policy}" pull-request dev task/9-branch-release-governance 'Branch policy'
expect_pass pre_push_record refs/tags/v0.1.0 "${base}" refs/tags/v0.1.0 0000000000000000000000000000000000000000
expect_fail pre_push_record refs/tags/v0.1.0-rc.1 "${base}" refs/tags/v0.1.0-rc.1 0000000000000000000000000000000000000000
expect_fail pre_push_record refs/tags/v0.1.0 "${base}" refs/tags/v0.1.0 "${base}"

mkdir -p docs/handoff
printf 'state\n' > docs/handoff/state.md
git add docs/handoff/state.md
git commit -qm 'docs(handoff): record state' -m 'Workflow-Admission: Handoff'
handoff="$(git rev-parse HEAD)"
expect_pass "${policy}" range dev "${base}" "${handoff}"
expect_fail "${policy}" range main "${base}" "${handoff}"
expect_pass "${policy}" pull-request dev task/9-branch-release-governance 'chore: enforce branch policy' "${base}" "${handoff}"
expect_fail "${policy}" pull-request dev task/9-branch-release-governance 'chore: enforce branch policy' "${handoff}" "${base}"

git switch -q -c hotfix-origin "${base}"
printf 'hotfix\n' > hotfix.txt
git add hotfix.txt
git commit -qm 'fix: correct release'
hotfix="$(git rev-parse HEAD)"
git switch -q dev
expect_pass "${policy}" pull-request main hotfix/9-release-fix 'fix: correct release' "${base}" "${hotfix}" L3DigitalNet/star-trek-alter-course L3DigitalNet/star-trek-alter-course "${handoff}"
expect_fail "${policy}" pull-request main hotfix/9-release-fix 'fix: correct release' "${base}" "${hotfix}" L3DigitalNet/star-trek-alter-course example/fork "${handoff}"
expect_fail "${policy}" pull-request main hotfix/9-release-fix 'fix: correct release' "${base}" "${handoff}" L3DigitalNet/star-trek-alter-course L3DigitalNet/star-trek-alter-course "${handoff}"
expect_fail "${policy}" pull-request main dev 'chore(release): v0.1.0' "${base}" "${handoff}" L3DigitalNet/star-trek-alter-course example/fork "${handoff}"

git switch -q -c merge-side "${handoff}"
printf 'merge side\n' > docs/handoff/merge-side.md
git add docs/handoff/merge-side.md
git commit -qm 'docs(handoff): add merge side' -m 'Workflow-Admission: Handoff'
git switch -q -c merge-direct "${handoff}"
git merge -q --no-ff merge-side -m 'docs(handoff): merge state' -m 'Workflow-Admission: Handoff'
expect_fail "${policy}" range dev "${handoff}" "$(git rev-parse HEAD)"

git switch -q -c invalid-handoff "${base}"
printf 'source\n' > source.txt
git add source.txt
git commit -qm 'chore: change source' -m 'Workflow-Admission: Handoff'
expect_fail "${policy}" range dev "${base}" "$(git rev-parse HEAD)"

git switch -q -c valid-t0 "${base}"
printf 'corrected prose\n' > notes.md
git add notes.md
git commit -qm 'docs: correct prose' -m 'Workflow-Admission: T0'
expect_pass "${policy}" range dev "${base}" "$(git rev-parse HEAD)"

git switch -q -c protected-t0 "${base}"
mkdir -p docs/adr
printf 'decision\n' > docs/adr/0000-test.md
git add docs/adr/0000-test.md
git commit -qm 'docs: correct decision prose' -m 'Workflow-Admission: T0'
expect_fail "${policy}" range dev "${base}" "$(git rev-parse HEAD)"

printf 'Branch policy tests passed.\n'
