#!/usr/bin/env bash
set -euo pipefail
usage() { echo 'Usage: publish-review-branch.sh --repo <repo> --base <sha> --upstream <sha> --branch <review/upstream/YYYY-MM-base-upstream> --snapshot <directory>' >&2; }
repo='' base='' upstream='' branch='' snapshot=''
while (($#)); do
  case "$1" in
    --repo|--base|--upstream|--branch|--snapshot)
      (($# >= 2)) || { usage; exit 2; }
      case "$1" in --repo) repo="$2";; --base) base="$2";; --upstream) upstream="$2";; --branch) branch="$2";; --snapshot) snapshot="$2";; esac
      shift 2;;
    *) usage; exit 2;;
  esac
done
[[ -n "$repo" && -n "$base" && -n "$upstream" && -n "$branch" && -n "$snapshot" ]] || { usage; exit 2; }
[[ "$base" =~ ^[0-9a-fA-F]{40}$ && "$upstream" =~ ^[0-9a-fA-F]{40}$ ]] || { usage; exit 2; }
[[ "$branch" =~ ^review/upstream/[0-9]{4}-(0[1-9]|1[0-2])-[0-9a-f]{12}-[0-9a-f]{12}$ ]] || { echo 'Unsafe review branch name.' >&2; exit 2; }
[[ "${base,,}" == "${branch:24:12}"* && "${upstream,,}" == "${branch:37:12}"* ]] || { echo 'Branch pins do not match publication arguments.' >&2; exit 2; }
[[ -f "$snapshot/manifest.json" && -f "$snapshot/report.md" && -f "$snapshot/inventory.json" ]] || { echo 'Snapshot is incomplete.' >&2; exit 2; }
repo="$(cd "$repo" && pwd)"; snapshot="$(cd "$snapshot" && pwd)"; git_bin="${GIT_BIN:-git}"
origin="$("$git_bin" -C "$repo" remote get-url origin)"
[[ "$origin" =~ ^(https://github\.com/|git@github\.com:)Runic-Artifex/ReactiveUI\.Validation(\.git)?$ ]] || { echo "Origin is not Runic-Artifex/ReactiveUI.Validation: $origin" >&2; exit 1; }
mapfile -t push_urls < <("$git_bin" -C "$repo" remote get-url --push --all origin)
(( ${#push_urls[@]} == 1 )) || { echo 'Origin must have exactly one push URL.' >&2; exit 1; }
[[ "${push_urls[0]}" =~ ^(https://github\.com/|git@github\.com:)Runic-Artifex/ReactiveUI\.Validation(\.git)?$ ]] || { echo 'Origin push URL is not Runic-Artifex/ReactiveUI.Validation.' >&2; exit 1; }
[[ -z "$("$git_bin" -C "$repo" status --porcelain)" ]] || { echo 'Refusing to publish from a dirty worktree.' >&2; exit 1; }
validate() { node - "$base" "$upstream" "$1" <<'NODE'
const { readFileSync } = require('node:fs');
const [base, upstream, file] = process.argv.slice(2); const manifest = JSON.parse(readFileSync(file, 'utf8'));
if (manifest.base !== base.toLowerCase() || manifest.upstream !== upstream.toLowerCase()) throw new Error('Snapshot pins do not match publication arguments.');
NODE
}
validate "$snapshot/manifest.json"
target="eng/upstream/reviews/${branch#review/upstream/}"
set +e; "$git_bin" -C "$repo" ls-remote --exit-code --heads origin "refs/heads/$branch" >/dev/null 2>&1; remote_status=$?; set -e
if ((remote_status == 0)); then
  "$git_bin" -C "$repo" fetch --no-tags origin "refs/heads/$branch:refs/remotes/origin/$branch" >&2
  existing="$(mktemp)"; trap 'rm -f "$existing"' EXIT
  "$git_bin" -C "$repo" show "refs/remotes/origin/$branch:$target/manifest.json" > "$existing"; validate "$existing"
  selected="$snapshot/selected-branch-snapshot"; mkdir -p "$selected"
  for file in manifest.json inventory.json report.md; do
    "$git_bin" -C "$repo" show "refs/remotes/origin/$branch:$target/$file" > "$selected/$file"
  done
  printf '# Existing immutable review branch selected\n\nBranch: %s\nSnapshot path: %s\nThe generated candidate snapshot remains evidence of this run; `selected-branch-snapshot/` is the retained authoritative branch snapshot.\n' "$branch" "$target" > "$snapshot/branch-status.md"
  printf 'status=existing\nbranch=%s\ntarget=%s\nsnapshot=reused\n' "$branch" "$target"; exit 0
fi
((remote_status == 2)) || { echo "Unable to determine branch state (git ls-remote exit $remote_status)." >&2; exit "$remote_status"; }
[[ "$("$git_bin" -C "$repo" rev-parse HEAD)" == "$base" ]] || { echo 'Checkout no longer matches base.' >&2; exit 1; }
"$git_bin" -C "$repo" switch --create "$branch" "$base" >&2
mkdir -p "$repo/$(dirname "$target")"; [[ ! -e "$repo/$target" ]] || { echo 'Snapshot destination exists.' >&2; exit 1; }
cp -a "$snapshot" "$repo/$target"; "$git_bin" -C "$repo" add -- "$target"
"$git_bin" -C "$repo" -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' commit -m "docs: prepare ${branch#review/upstream/} upstream review" >&2
"$git_bin" -C "$repo" push --force-with-lease="refs/heads/$branch:" origin "HEAD:refs/heads/$branch" >&2
printf 'status=created\nbranch=%s\ntarget=%s\nsnapshot=created\n' "$branch" "$target"
