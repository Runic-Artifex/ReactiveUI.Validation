#!/usr/bin/env bash
set -euo pipefail
usage() { echo 'Usage: open-review-pr.sh --repo Runic-Artifex/ReactiveUI.Validation --branch <branch> --base <sha> --upstream <sha> --month YYYY-MM --evidence-dir <directory>' >&2; }
repo='' branch='' base='' upstream='' month='' evidence=''
while (($#)); do
  case "$1" in
    --repo|--branch|--base|--upstream|--month|--evidence-dir)
      (($# >= 2)) || { usage; exit 2; }
      case "$1" in --repo) repo="$2";; --branch) branch="$2";; --base) base="$2";; --upstream) upstream="$2";; --month) month="$2";; --evidence-dir) evidence="$2";; esac
      shift 2;;
    *) usage; exit 2;;
  esac
done
[[ "$repo" == 'Runic-Artifex/ReactiveUI.Validation' && "$base" =~ ^[0-9a-fA-F]{40}$ && "$upstream" =~ ^[0-9a-fA-F]{40}$ && "$month" =~ ^[0-9]{4}-(0[1-9]|1[0-2])$ ]] || { usage; exit 2; }
[[ "$branch" =~ ^review/upstream/[0-9]{4}-(0[1-9]|1[0-2])-[0-9a-f]{12}-[0-9a-f]{12}$ ]] || { echo 'Unsafe review branch name.' >&2; exit 2; }
mkdir -p "$evidence"; gh_bin="${GH_BIN:-gh}"
existing="$("$gh_bin" pr list --repo "$repo" --state open --head "$branch" --base main --json url --jq '.[0].url')"
if [[ -n "$existing" ]]; then printf 'status=existing\nurl=%s\n' "$existing"; exit 0; fi
body="$evidence/pull-request-body.md"; error="$evidence/pull-request-error.txt"
printf '%s\n\nPins Runic base `%s` and upstream candidate `%s`.\n' "Automated preparation for the ${month} upstream review." "$base" "$upstream" > "$body"
printf '%s\n' 'This PR contains inventory and virtual-merge evidence only. It does not merge upstream, publish packages, or contact upstream.' >> "$body"
set +e; url="$("$gh_bin" pr create --repo "$repo" --base main --head "$branch" --title "docs: prepare ${month} upstream review" --body-file "$body" 2>"$error")"; code=$?; set -e
if ((code == 0)); then rm -f "$error" "$evidence/pr-status.md"; printf 'status=created\nurl=%s\n' "$url"; exit 0; fi
manual="https://github.com/${repo}/compare/main...Runic-Artifex:${branch}?expand=1"
printf '# Pull request creation deferred\n\nOpen manually: %s\n\nThe immutable branch and artifact remain authoritative; a later run retries without changing the branch.\n' "$manual" > "$evidence/pr-status.md"
printf 'status=deferred\nmanual_url=%s\n' "$manual"
