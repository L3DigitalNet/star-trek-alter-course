#!/usr/bin/env bash
# Exercise positive and negative branch-policy behavior in an isolated Git
# repository so tests cannot mutate the authoritative checkout.
# Requirements: Bash, Git, jq, mktemp, and standard GNU text utilities. Package
# and GitHub reads are mocked so admission tests need no credentials or network.

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
mkdir -p "${fixture}/.agents/skills/github-workflow/bin" "${fixture}/.agents/skills/github-workflow/references" "${fixture}/.standards/packages/github-workflow"
cat > "${fixture}/.agents/skills/github-workflow/bin/gh-workflow" << 'MOCK'
#!/usr/bin/env bash
set -euo pipefail
case "$1" in
  receipt) cat "$MOCK_RECEIPT"; exit "${MOCK_RECEIPT_EXIT:-0}" ;;
  check) [[ "$*" == *'--through ready'* ]]; cat "$MOCK_CHECK"; exit "${MOCK_CHECK_EXIT:-0}" ;;
  *) exit 2 ;;
esac
MOCK
chmod +x "${fixture}/.agents/skills/github-workflow/bin/gh-workflow"
printf 'base policy\n' > "${fixture}/.standards/packages/github-workflow/policy.toml"
printf 'base schema\n' > "${fixture}/.agents/skills/github-workflow/references/org-schema.yaml"
git -C "${fixture}" add README.md .agents .standards
git -C "${fixture}" commit -qm 'docs: initialize fixture'
base="$(git -C "${fixture}" rev-parse HEAD)"
git -C "${fixture}" update-ref refs/remotes/origin/main "${base}"

mkdir -p "${fixture}/docs/adr"
printf 'branch governance\n' > "${fixture}/docs/adr/0013-use-dev-for-development-and-main-for-releases.md"
git -C "${fixture}" add docs/adr/0013-use-dev-for-development-and-main-for-releases.md
git -C "${fixture}" commit -qm 'docs: adopt branch governance'
baseline="$(git -C "${fixture}" rev-parse HEAD)"
git -C "${fixture}" reset -q --hard "${base}"

cd "${fixture}"

expect_pass "${policy}" branch task/9-branch-release-governance
expect_pass "${policy}" branch standalone/prose-maintenance
expect_fail "${policy}" branch standalone/-invalid
expect_fail "${policy}" branch standalone/Uppercase
expect_fail "${policy}" pull-request dev standalone/prose-maintenance 'docs: update notes'
expect_fail "${policy}" pull-request feature/9-integration standalone/prose-maintenance 'docs: update notes'
expect_fail "${policy}" pull-request main standalone/prose-maintenance 'docs: update notes'
expect_fail "${policy}" pull-request standalone/prose-maintenance task/9-work 'docs: update notes'
expect_fail "${policy}" branch feature/no-issue
expect_pass "${policy}" pull-request dev task/9-branch-release-governance 'chore: enforce branch policy'
expect_pass "${policy}" pull-request main dev 'chore(release): v0.1.0'
expect_fail "${policy}" pull-request main dev 'chore(release): v0.1.0-rc.1'
expect_pass "${policy}" pull-request main dev 'chore(baseline): establish main baseline' "${base}" "${baseline}" L3DigitalNet/star-trek-alter-course L3DigitalNet/star-trek-alter-course "${baseline}"
expect_fail "${policy}" pull-request main dev 'chore(baseline): establish main baseline'
expect_fail "${policy}" pull-request main dev 'chore(baseline): establish main baseline' "${baseline}" "${baseline}" L3DigitalNet/star-trek-alter-course L3DigitalNet/star-trek-alter-course "${baseline}"
expect_fail "${policy}" pull-request main dev 'chore(baseline): establish main baseline' "${base}" "${baseline}" L3DigitalNet/star-trek-alter-course example/fork "${baseline}"
expect_pass "${policy}" pull-request main hotfix/9-release-fix 'fix(release): correct package'
expect_pass "${policy}" pull-request dev main 'chore(sync): merge main into dev'
expect_fail "${policy}" pull-request main task/9-branch-release-governance 'chore: bypass release flow'
expect_fail "${policy}" pull-request dev hotfix/9-release-fix 'fix: skip hotfix release'
expect_fail "${policy}" pull-request dev task/9-branch-release-governance 'Branch policy'

# This fixture installs only mock readers at the same paths used by production.
# The route must compose their verdicts; neither the name nor a receipt suffices.
mkdir -p scripts .agents/skills/github-workflow/bin mock-bin
cp "${root}/scripts/check-standalone-admission.sh" scripts/
cat > mock-bin/gh << 'MOCK'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == api && "$2" == repos/L3DigitalNet/star-trek-alter-course/pulls/42 ]]
if [[ "${MOCK_CHANGED:-0}" == 1 && -f "$MOCK_READ_MARKER" ]]; then
  jq '.body = "changed during evaluation"' "$MOCK_SNAPSHOT"
else
  cat "$MOCK_SNAPSHOT"
fi
touch "$MOCK_READ_MARKER"
exit "${MOCK_GH_EXIT:-0}"
MOCK
chmod +x .agents/skills/github-workflow/bin/gh-workflow mock-bin/gh
export PATH="${fixture}/mock-bin:${PATH}"
export MOCK_RECEIPT="${fixture}/receipt.json" MOCK_CHECK="${fixture}/check.json"
export MOCK_SNAPSHOT="${fixture}/snapshot.json" MOCK_READ_MARKER="${fixture}/read-marker"
cat > receipt-good.json << 'JSON'
{"schema_version":"1","command":"receipt","result":"clear","target":{"kind":"pull_request","number":42,"repository":"L3DigitalNet/star-trek-alter-course"},"gate":null,"findings":[],"steps":[],"item":{"kind":"pull_request","number":42,"state":"open","relationship":"Standalone"},"gaps":["a governing issue link"]}
JSON
jq '.command = "check" | .gate = "ready" | del(.item, .gaps)' receipt-good.json > check-good.json
jq -n --arg sha "${base}" '{number:42,state:"open",draft:true,body:"$(touch malicious-executed)",head:{sha:$sha,ref:"standalone/prose-maintenance"},base:{ref:"dev",repo:{full_name:"L3DigitalNet/star-trek-alter-course"}}}' > snapshot-good.json
malicious_title="$(jq -r '"docs: update notes " + .body' snapshot-good.json)"
readonly malicious_title
reset_admission() {
  cp receipt-good.json "${MOCK_RECEIPT}"
  cp check-good.json "${MOCK_CHECK}"
  cp snapshot-good.json "${MOCK_SNAPSHOT}"
  rm -f "${MOCK_READ_MARKER}"
  export MOCK_RECEIPT_EXIT=0 MOCK_CHECK_EXIT=0 MOCK_GH_EXIT=0 MOCK_CHANGED=0
}
standalone_route() {
  "${policy}" pull-request dev standalone/prose-maintenance "${malicious_title}" \
    "${base}" "${base}" L3DigitalNet/star-trek-alter-course example/fork "${base}" 42
}
reset_admission
expect_pass standalone_route
[[ ! -e malicious-executed ]]
# Admission must use the package in the base object, even when the PR checkout
# replaces its installed executable. Restore the fixture for later Git cases.
cp .agents/skills/github-workflow/bin/gh-workflow package-original
printf '#!/usr/bin/env bash\nexit 3\n' > .agents/skills/github-workflow/bin/gh-workflow
expect_pass standalone_route
cp package-original .agents/skills/github-workflow/bin/gh-workflow
reset_admission
jq '.result = "domain-finding" | .findings = [{phase:"merge",code:"GHW-PR-MERGE-CHECKS-PENDING"}]' receipt-good.json > "${MOCK_RECEIPT}"
expect_pass standalone_route
for phase in structural ready post-merge; do
  reset_admission
  jq --arg phase "${phase}" '.result = "domain-finding" | .findings = [{phase:$phase,code:"ineligible"}]' receipt-good.json > "${MOCK_RECEIPT}"
  expect_fail standalone_route
done
for mutation in \
  'del(.item.relationship)' \
  '.item.relationship = "Final" | .item.governing_issue = 9' \
  '.item.governing_issue = 9' \
  '.result = "domain-finding"' \
  '.findings = [{code:"contradictory-metadata"}]' \
  '.target.number = 43' \
  '.target.repository = "example/fork"' \
  '.item.number = 43' \
  '.item.state = "closed"' \
  '.item.merged = true' \
  '.gaps = "malformed"' \
  'del(.findings)' \
  'del(.steps)' \
  '.schema_version = "2"'; do
  reset_admission
  jq "${mutation}" receipt-good.json > "${MOCK_RECEIPT}"
  expect_fail standalone_route
done
for mutation in \
  '.result = "domain-finding"' \
  '.gate = "structural"' \
  '.findings = [{code:"ineligible"}]' \
  '.target.number = 43' \
  'del(.findings)' \
  '.command = "receipt"'; do
  reset_admission
  jq "${mutation}" check-good.json > "${MOCK_CHECK}"
  expect_fail standalone_route
done
for evidence_file in "${MOCK_RECEIPT}" "${MOCK_CHECK}"; do
  reset_admission
  : > "${evidence_file}"
  expect_fail standalone_route
  reset_admission
  printf 'malformed JSON\n' > "${evidence_file}"
  expect_fail standalone_route
  reset_admission
  cat receipt-good.json receipt-good.json > "${evidence_file}"
  expect_fail standalone_route
done
for failure in MOCK_RECEIPT_EXIT MOCK_CHECK_EXIT MOCK_GH_EXIT; do
  reset_admission
  export "${failure}=3"
  expect_fail standalone_route
done
reset_admission
export MOCK_CHANGED=1
expect_fail standalone_route
for mutation in '.head.sha = "0000000000000000000000000000000000000000"' '.base.ref = "main"' '.number = 43'; do
  reset_admission
  jq "${mutation}" snapshot-good.json > "${MOCK_SNAPSHOT}"
  expect_fail standalone_route
done
reset_admission
expect_pass standalone_route

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

git switch -q -c protected-t0-wiki "${base}"
mkdir -p docs/wiki
printf 'design\n' > docs/wiki/architecture.md
git add docs/wiki/architecture.md
git commit -qm 'docs: correct design prose' -m 'Workflow-Admission: T0'
expect_fail "${policy}" range dev "${base}" "$(git rev-parse HEAD)"

printf 'Branch policy tests passed.\n'
