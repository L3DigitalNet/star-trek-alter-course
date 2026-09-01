#!/usr/bin/env bash
# Restore the pinned Linux x86_64 command-line tools used by fix.sh and verify.sh.
# Requirements: Bash, curl, sha256sum, tar, and xz support. Files are installed
# under the user's cache so verification never changes repository content.

set -euo pipefail

readonly cache_root="${XDG_CACHE_HOME:-${HOME}/.cache}/star-trek-alter-course/native-tools"
readonly bin_dir="${cache_root}/bin"

mkdir -p "${bin_dir}"

install_file() (
  local name=$1
  local version=$2
  local url=$3
  local expected_sha=$4
  local destination="${bin_dir}/${name}"

  if [[ -x "${destination}" ]] && "${destination}" --version 2>&1 | grep -Fq "${version}"; then
    return
  fi

  local temp_dir
  temp_dir="$(mktemp -d "${cache_root}/download.XXXXXX")"
  trap 'rm -rf -- "${temp_dir}"' EXIT
  curl --fail --location --silent --show-error "${url}" --output "${temp_dir}/${name}"
  printf '%s  %s\n' "${expected_sha}" "${temp_dir}/${name}" | sha256sum --check --status
  install -m 0755 "${temp_dir}/${name}" "${destination}"
)

install_archive_binary() (
  local name=$1
  local version=$2
  local url=$3
  local expected_sha=$4
  local archive_member=$5
  local destination="${bin_dir}/${name}"

  if [[ -x "${destination}" ]] && "${destination}" --version 2>&1 | grep -Fq "${version}"; then
    return
  fi

  local temp_dir
  temp_dir="$(mktemp -d "${cache_root}/download.XXXXXX")"
  trap 'rm -rf -- "${temp_dir}"' EXIT
  curl --fail --location --silent --show-error "${url}" --output "${temp_dir}/archive"
  printf '%s  %s\n' "${expected_sha}" "${temp_dir}/archive" | sha256sum --check --status
  tar -xf "${temp_dir}/archive" -C "${temp_dir}"
  install -m 0755 "${temp_dir}/${archive_member}" "${destination}"
)

install_file \
  shfmt \
  3.14.0 \
  https://github.com/mvdan/sh/releases/download/v3.14.0/shfmt_v3.14.0_linux_amd64 \
  fe42021c7272ef2d67ea36cbc3031683c625d0badec733ef3a57b567246a0b66

install_archive_binary \
  shellcheck \
  0.11.0 \
  https://github.com/koalaman/shellcheck/releases/download/v0.11.0/shellcheck-v0.11.0.linux.x86_64.tar.xz \
  8c3be12b05d5c177a04c29e3c78ce89ac86f1595681cab149b65b97c4e227198 \
  shellcheck-v0.11.0/shellcheck

install_archive_binary \
  actionlint \
  1.7.12 \
  https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_linux_amd64.tar.gz \
  8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8 \
  actionlint

install_archive_binary \
  gitleaks \
  8.30.1 \
  https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_linux_x64.tar.gz \
  551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb \
  gitleaks

printf '%s\n' "${bin_dir}"
