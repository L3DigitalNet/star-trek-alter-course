#!/usr/bin/env bash
# Validate repository branch names, pull-request topology, commit subjects, and
# direct admission to protected refs.
# Requirements: Bash 5, Git, and standard GNU text utilities. Git history is
# required because direct-push validation classifies every newly reachable commit.

set -euo pipefail

root="$(git rev-parse --show-toplevel)"
readonly root
cd "${root}"

readonly zero_sha='0000000000000000000000000000000000000000'
readonly topic_pattern='^(feature|fix|task|docs|hotfix)/[1-9][0-9]*-[a-z0-9]+(-[a-z0-9]+)*$'
readonly conventional_pattern='^(feat|fix|docs|chore|refactor|test|build|ci|perf|revert)(\([a-z0-9][a-z0-9._/-]*\))?!?:[[:space:]][^[:space:]].*$'
readonly semver_pattern='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
readonly branch_governance_marker='docs/adr/0013-use-dev-for-development-and-main-for-releases.md'

fail() {
  printf 'Branch policy: %s\n' "$1" >&2
  exit 1
}

validate_branch_name() {
  local branch=$1
  if [[ "${branch}" == 'main' || "${branch}" == 'dev' || "${branch}" =~ ${topic_pattern} ]]; then
    return
  fi
  fail "branch '${branch}' must be main, dev, or <feature|fix|task|docs|hotfix>/<issue>-<slug>."
}

validate_conventional_subject() {
  local subject=$1
  [[ "${subject}" =~ ${conventional_pattern} ]] ||
    fail "subject '${subject}' is not a Conventional Commit subject."
}

validate_commit_message_file() {
  local message_file=$1
  local subject
  IFS= read -r subject < "${message_file}" || true
  validate_conventional_subject "${subject}"
}

is_initial_baseline_promotion() {
  local base_sha=$1
  local head_sha=$2

  # The governance ADR is the adoption boundary: requiring it only on the head
  # makes this exception self-expire after the first baseline reaches main.
  ! git cat-file -e "${base_sha}:${branch_governance_marker}" 2> /dev/null &&
    git cat-file -e "${head_sha}:${branch_governance_marker}" 2> /dev/null
}

validate_pull_request() {
  local base=$1
  local head=$2
  local title=$3
  local base_sha=${4:-}
  local head_sha=${5:-}
  local base_repo=${6:-}
  local head_repo=${7:-}
  local dev_sha=${8:-}
  local release_version
  local shared_base

  validate_branch_name "${base}"
  validate_branch_name "${head}"
  validate_conventional_subject "${title}"

  case "${base}" in
    dev)
      if [[ "${head}" == 'main' ]]; then
        [[ "${title}" == 'chore(sync): merge main into dev' ]] ||
          fail "main-to-dev synchronization must use title 'chore(sync): merge main into dev'."
      elif [[ "${head}" == 'dev' || "${head}" =~ ^hotfix/ ]]; then
        fail "pull requests into dev must come from a development topic branch or main synchronization."
      fi
      ;;
    main)
      if [[ "${head}" == 'dev' ]]; then
        if [[ "${title}" == 'chore(baseline): establish main baseline' ]]; then
          [[ -n "${base_sha}" && -n "${head_sha}" ]] ||
            fail 'the initial baseline promotion requires base and head SHAs.'
          is_initial_baseline_promotion "${base_sha}" "${head_sha}" ||
            fail 'the initial baseline promotion is permitted only while main predates the branch-governance ADR.'
        else
          release_version="${title#'chore(release): '}"
          [[ "${title}" == "chore(release): ${release_version}" && "${release_version}" =~ ${semver_pattern} ]] ||
            fail "dev-to-main promotions must use the initial baseline title or 'chore(release): vMAJOR.MINOR.PATCH'."
        fi
      elif [[ "${head}" =~ ^hotfix/ ]]; then
        [[ "${title}" =~ ^fix(\([a-z0-9][a-z0-9._/-]*\))?!?: ]] ||
          fail 'hotfix pull requests must use a fix Conventional Commit title.'
      else
        fail 'main accepts pull requests only from dev or a hotfix branch.'
      fi
      ;;
    feature/*)
      [[ "${head}" != 'main' && "${head}" != 'dev' && ! "${head}" =~ ^hotfix/ ]] ||
        fail 'feature integration accepts only development topic branches.'
      ;;
    *)
      fail "pull requests may target only dev, main, or an Initiative feature branch; got '${base}'."
      ;;
  esac

  if [[ -n "${base_sha}" || -n "${head_sha}" ]]; then
    [[ -n "${base_sha}" && -n "${head_sha}" ]] || fail 'both base and head SHAs are required for ancestry validation.'
    git cat-file -e "${base_sha}^{commit}" 2> /dev/null || fail "base commit ${base_sha} is unavailable."
    git cat-file -e "${head_sha}^{commit}" 2> /dev/null || fail "head commit ${head_sha} is unavailable."
    if [[ ! ("${base}" == 'dev' && "${head}" == 'main') ]]; then
      git merge-base --is-ancestor "${base_sha}" "${head_sha}" ||
        fail "head '${head}' must originate from current base '${base}'."
    fi
  fi

  if [[ -n "${base_repo}" || -n "${head_repo}" || -n "${dev_sha}" ]]; then
    [[ -n "${base_repo}" && -n "${head_repo}" && -n "${dev_sha}" ]] ||
      fail 'base repository, head repository, and dev SHA are all required for source validation.'
    if [[ "${base}" == 'main' || ("${base}" == 'dev' && "${head}" == 'main') ]]; then
      [[ "${head_repo}" == "${base_repo}" ]] ||
        fail "release, hotfix, and synchronization branches must belong to '${base_repo}'."
    fi
    if [[ "${base}" == 'main' && "${head}" =~ ^hotfix/ ]]; then
      git cat-file -e "${dev_sha}^{commit}" 2> /dev/null || fail "dev commit ${dev_sha} is unavailable."
      shared_base="$(git merge-base "${dev_sha}" "${head_sha}")"
      [[ "${shared_base}" == "${base_sha}" ]] ||
        fail 'a hotfix must branch from current main without inheriting unreleased dev commits.'
    fi
  fi
}

changed_paths() {
  git diff-tree --root --no-commit-id --name-only -r "$1"
}

workflow_admission() {
  local commit=$1
  local -a admissions=()
  mapfile -t admissions < <(
    git show -s --format=%B "${commit}" |
      git interpret-trailers --parse |
      sed -n 's/^Workflow-Admission:[[:space:]]*//p'
  )
  ((${#admissions[@]} == 1)) ||
    fail "direct commit ${commit} must carry exactly one Workflow-Admission trailer."
  printf '%s\n' "${admissions[0]}"
}

validate_handoff_commit() {
  local commit=$1
  local path
  local path_count=0
  while IFS= read -r path; do
    ((path_count += 1))
    case "${path}" in
      docs/handoff/* | docs/STATUS.md | docs/TODO.md) ;;
      *) fail "handoff commit ${commit} changes disallowed path '${path}'." ;;
    esac
  done < <(changed_paths "${commit}")
  ((path_count > 0)) || fail "handoff commit ${commit} must change at least one allowed path."
}

validate_t0_commit() {
  local commit=$1
  local added deleted path
  local file_count=0
  local changed_lines=0

  while IFS=$'\t' read -r added deleted path; do
    [[ "${added}" =~ ^[0-9]+$ && "${deleted}" =~ ^[0-9]+$ ]] ||
      fail "T0 commit ${commit} contains a binary or uncountable change."
    ((file_count += 1))
    ((changed_lines += added + deleted))
    case "${path}" in
      *.md) ;;
      *) fail "T0 commit ${commit} changes non-Markdown path '${path}'." ;;
    esac
    case "${path}" in
      AGENTS.md | CLAUDE.md | .agents/* | .claude/* | .github/* | docs/adr/* | docs/design/* | docs/specs/* | docs/handoff/* | docs/STATUS.md | docs/TODO.md)
        fail "T0 commit ${commit} changes protected surface '${path}'."
        ;;
    esac
  done < <(git diff-tree --root --no-commit-id --numstat -r "${commit}")

  ((file_count > 0 && file_count <= 3)) ||
    fail "T0 commit ${commit} must change between 1 and 3 files."
  ((changed_lines <= 30)) ||
    fail "T0 commit ${commit} exceeds 30 added-plus-deleted lines."
}

validate_direct_commit() {
  local commit=$1
  local subject admission parent_count
  parent_count="$(git rev-list --parents -n 1 "${commit}" | awk '{print NF - 1}')"
  ((parent_count == 1)) || fail "direct admission ${commit} must not be a root or merge commit."
  subject="$(git show -s --format=%s "${commit}")"
  validate_conventional_subject "${subject}"
  admission="$(workflow_admission "${commit}")"

  case "${admission}" in
    Handoff) validate_handoff_commit "${commit}" ;;
    T0) validate_t0_commit "${commit}" ;;
    *) fail "direct commit ${commit} has unsupported Workflow-Admission value '${admission}'." ;;
  esac
}

validate_direct_range() {
  local branch=$1
  local before=$2
  local after=$3
  local commit

  [[ "${branch}" == 'dev' ]] || fail "direct pushes to '${branch}' are prohibited."
  [[ "${before}" != "${zero_sha}" ]] || fail 'the permanent dev branch may not be created through a local push.'
  [[ "${after}" != "${zero_sha}" ]] || fail 'the permanent dev branch may not be deleted.'
  git merge-base --is-ancestor "${before}" "${after}" || fail 'force-pushing dev is prohibited.'

  while IFS= read -r commit; do
    validate_direct_commit "${commit}"
  done < <(git rev-list --reverse "${before}..${after}")
}

validate_version_tag() {
  local ref=$1
  local local_sha=$2
  local remote_sha=$3
  local tag=${ref#refs/tags/}
  local commit

  [[ "${tag}" =~ ${semver_pattern} ]] || fail "release tag '${tag}' is not valid SemVer."
  [[ "${local_sha}" != "${zero_sha}" ]] || fail "release tag '${tag}' may not be deleted."
  [[ "${remote_sha}" == "${zero_sha}" ]] || fail "release tag '${tag}' may not be moved."
  commit="$(git rev-parse "${local_sha}^{commit}")"
  git merge-base --is-ancestor "${commit}" refs/remotes/origin/main ||
    fail "release tag '${tag}' must identify a commit reachable from origin/main."
}

validate_pre_push() {
  local _ local_sha remote_ref remote_sha branch
  while read -r _ local_sha remote_ref remote_sha; do
    case "${remote_ref}" in
      refs/heads/*)
        branch=${remote_ref#refs/heads/}
        validate_branch_name "${branch}"
        if [[ "${branch}" == 'main' || "${branch}" == 'dev' ]]; then
          validate_direct_range "${branch}" "${remote_sha}" "${local_sha}"
        fi
        ;;
      refs/tags/v*) validate_version_tag "${remote_ref}" "${local_sha}" "${remote_sha}" ;;
    esac
  done
}

validate_repository() {
  local branch mode path
  branch="$(git branch --show-current)"
  [[ -z "${branch}" ]] || validate_branch_name "${branch}"

  for path in .githooks/commit-msg .githooks/pre-push scripts/branch-policy.sh scripts/setup-git-hooks.sh scripts/test-branch-policy.sh; do
    mode="$(git ls-files --stage -- "${path}" | awk 'NR == 1 {print $1}')"
    [[ "${mode}" == '100755' ]] || fail "${path} must be tracked and executable."
  done
}

usage() {
  printf 'Usage: %s branch NAME | commit-message FILE | pull-request BASE HEAD TITLE [BASE_SHA HEAD_SHA BASE_REPO HEAD_REPO DEV_SHA] | range BRANCH BEFORE AFTER | pre-push | repository\n' "${0##*/}" >&2
  exit 2
}

case "${1:-}" in
  branch)
    (($# == 2)) || usage
    validate_branch_name "$2"
    ;;
  commit-message)
    (($# == 2)) || usage
    validate_commit_message_file "$2"
    ;;
  pull-request)
    (($# == 4 || $# == 6 || $# == 9)) || usage
    validate_pull_request "$2" "$3" "$4" "${5:-}" "${6:-}" "${7:-}" "${8:-}" "${9:-}"
    ;;
  range)
    (($# == 4)) || usage
    validate_direct_range "$2" "$3" "$4"
    ;;
  pre-push)
    (($# == 1)) || usage
    validate_pre_push
    ;;
  repository)
    (($# == 1)) || usage
    validate_repository
    ;;
  *) usage ;;
esac
