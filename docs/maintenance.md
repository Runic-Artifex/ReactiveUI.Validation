# Maintaining the Runic ReactiveUI.Validation fork

Applied **2026-10-05**, following the [Runic DynamicData maintenance policy at 5887123](https://github.com/Runic-Artifex/DynamicData/blob/5887123c83c831262bbb9ef383ca4ad399d0abc9/docs/maintenance.md), adapted to this repository's dependencies and build scope. This document governs maintenance. The [upstream investigation](upstream/README.md) is dated research, not live completion state. The [fork difference register](fork-differences.md) identifies adaptations and regressions to preserve.

## Repository and integration ownership

Runic `main` on `origin` is the maintained integration branch. It already contains the port in [`509dd45`](https://github.com/Runic-Artifex/ReactiveUI.Validation/commit/509dd45b7f8458a22ae4cb758b1b68e72d874217), based on upstream [`cde3062`](https://github.com/reactiveui/ReactiveUI.Validation/commit/cde3062937752abeb4216a92c15f5b3b37be9a34). A local branch still named `main` may be behind; fetch and use `origin/main` as the starting point. Topic branches carry small logical changes and their regressions.

One integration owner pins inputs, orders dependent topics, reviews conflicts and verification, and integrates/releases the result. Keep existing Runic and upstream Git history. Monthly syncs use real merges preserving both parent histories; do not squash syncs, rebase away upstream ancestry, reconstruct the fork as a patch stack, or replace it with a submodule wrapper. Patch exports may be derived artifacts. Preserve MIT licensing and original authorship. This workflow does not include upstream issues, PRs, comments or contributions.

## Monthly upstream review

Review upstream monthly, and sooner for an urgent correctness or security fix. This is a manual operating cadence; no recurring job or automatic merge is installed.

1. Fetch `origin` and `upstream`. Start from the maintained Runic `origin/main` in a clean checkout or isolated worktree. Preserve unrelated work. Record the Runic base SHA, previous integrated upstream SHA and review date.
2. Inspect upstream commits since that integrated point, including changes already ported selectively. Select one exact reviewed release/main commit and record its full SHA. A fresh issue catalog helps triage but does not establish that a change was integrated.
3. Create `sync/upstream/YYYY-MM` from the Runic base. Merge the pinned upstream commit with a real Git merge. Review cleanly merged changes as well as conflicts: an upstream build or dependency change can silently restore unwanted targets, package IDs, namespaces or workflows. Adapt the result to the difference register.
4. Keep follow-up behavioral adaptations in logical topic commits with focused regressions. Update the register when a difference is added, changed or retired. Preserve both flavors and both .NET 10 public API baselines.
5. Validate the resulting revision using the gates below. Record exact inputs, accepted/deferred changes, conflict decisions, retained differences and verification in `docs/upstream/reviews/YYYY-MM.md`, using the [template](upstream/reviews/TEMPLATE.md). A no-change review still records its pinned input and reason. An attempted merge that is deferred does not advance the integrated upstream point.
6. The integration owner integrates the tested branch into Runic `main` without squashing. If `main` moved, merge that new head into the sync branch, review the result and rerun affected checks. A fast-forward from the unchanged Runic base preserves the upstream merge commit. Remove task-owned temporary branches/worktrees after integration and evidence retention.

Example preparation commands, from a clean checkout:

```sh
git fetch origin main
git fetch upstream
runic_base=$(git rev-parse origin/main)
upstream_commit=$(git rev-parse upstream/main)
git switch -c sync/upstream/2026-10 "$runic_base"
git merge --no-ff --no-commit "$upstream_commit"
# Review and adapt the tree before recording the merge and follow-up commits.
```

Replace the month and choose the upstream SHA after review. If it is already an ancestor of the Runic base, record a no-change review rather than manufacture a merge. Check retained ancestry with `git merge-base --is-ancestor <pinned-upstream-sha> <tested-sync-sha>`. A selective port is not a full upstream sync.

## Coupled dependency updates

Validation consumes **released packages**, not the adjacent DynamicData checkout. The current cohort is ReactiveUI/ReactiveUI.Reactive 26.0.1 with Runic.DynamicData/Runic.DynamicData.Reactive 10.0.0-runic.5, on .NET 10. Pins live in [src/Directory.Packages.props](../src/Directory.Packages.props).

When DynamicData changes, complete and publish its tested pair first. Record its release tag, source SHA, versions and both asset hashes. Then update both Validation package pins and the allowed version/checksums in [eng/restore-fork-dependencies.py](../eng/restore-fork-dependencies.py) together. Validate the restored cohort and packaged consumers before adoption. Do not replace hashes merely to silence a checksum failure; verify the intended immutable release assets.

Keep ReactiveUI flavors on a compatible generation and preserve their corresponding DynamicData namespaces and scheduler types. No upstream `DynamicData` package may re-enter either restored consumer graph; the Primitives consumer must remain free of System.Reactive. Review inherited Renovate proposals as inputs to this process: NuGet-only automation does not update the GitHub-release bootstrap or prove cohort compatibility.

DynamicData separately tests Primitives 8.4.0 and 9.0.0. That does not imply Validation supports ReactiveUI 25 or needs the same matrix: this fork's supported cohort is ReactiveUI 26. Add an older cohort only with explicit consumer need, compatible package assets and its own verification. SDK, runtime, platform and major ReactiveUI upgrades are support-policy decisions; an upstream merge does not authorize .NET 11 or renewed legacy platform support by itself.

## Urgent fixes and conflicts

An urgent fix may be ported ahead of the monthly merge. Pin the source and prerequisites; use `git cherry-pick -x` when applicable, or cite the source commit/PR in an adapted commit. Include a meaningful regression and register entry. At the next upstream merge, compare implementations and remove redundant adaptation only after equivalent behavior is verified. Keep useful regressions after replacement.

Separate mechanical edits, public contracts and behavioral fixes where practical. Translate upstream tests to awaited TUnit assertions. Pin initial emissions, message-only changes, rule membership, property paths, replacement/null behavior, scheduling and disposal when touching those areas. Performance changes need relevant measurements and correctness replay.

Use recorded conflict resolutions as an aid, with automatic staging disabled:

```sh
git config --local rerere.enabled true
git config --local rerere.autoupdate false
git rerere diff
```

Review reused resolutions and surrounding code, then stage deliberately. Forget stale resolutions with `git rerere forget <path>` and retest affected behavior in both flavors. Record significant choices in the monthly review; a clean merge or remembered resolution is not proof of correctness.

## Validation and development environment

Follow [CONTRIBUTING](../CONTRIBUTING.md), [global.json](../global.json), project files and current CI. On this NixOS desktop, read `../runic-sdk/.envrc` and `flake.nix`, inspect `direnv status`, and reuse the locked environment. Use Git-aware flakes, resolve SDK pin mismatches, and do not accept a last-working-shell fallback as validation of changed Nix inputs.

From this repository root:

```sh
direnv exec ../runic-sdk python3 eng/restore-fork-dependencies.py
cd src
direnv exec ../../runic-sdk dotnet build ReactiveUI.Validation.slnx -c Release -warnaserror
direnv exec ../../runic-sdk dotnet test --solution ReactiveUI.Validation.slnx -c Release -- --maximum-parallel-tests 4 --report-trx
direnv exec ../../runic-sdk dotnet pack ReactiveUI.Validation.slnx -c Release --no-build -o ../artifacts/packages
cd ..
direnv exec ../runic-sdk python3 eng/verify-packages.py
```

For other systems, run the same commands directly in the correctly pinned environment. Build current sources before tests; do not use `--no-build` for tests. Packing with `--no-build` is valid only after building the same source/configuration. Review both [Primitives](../src/ReactiveUI.Validation/PublicAPI/net10.0/PublicAPI.txt) and [Reactive](../src/ReactiveUI.Validation.Reactive/PublicAPI/net10.0/PublicAPI.txt) baselines for intentional API changes. The build enforces PublicApiSharp baselines; do not bypass it or blindly regenerate baselines to accept drift.

The package verifier expects exactly one current package per core flavor in `artifacts/packages`. Before packing a different version, move useful prior outputs to a separate retained location or remove only disposable task-owned outputs. Never mix old/new packages as evidence of the current build. Preserve the verified dependency feed and useful caches. Exercise representative external consumers for overload, namespace, scheduler or dependency changes.

Require successful [Linux and Windows CI](../.github/workflows/ci-build.yml) at the tested code/workflow revision before integration or release, including strict safe-subset NativeAOT consumers (with trimming enabled) on matching Linux x64 and Windows x64 hosts using the same Linux shipping package artifact. The [native gate](../eng/verify-native-validation.py) must retain warning-as-error analysis, exact package graphs and source identity, and execute the emitted binaries. The dated diagnostic investigations do not satisfy that gate. The workflow's push triggers cover `main` and `runic/**`, not `sync/**`. For a sync branch, push it to this fork and dispatch the existing workflow explicitly:

```sh
git push -u origin sync/upstream/2026-10
gh workflow run ci-build.yml --repo Runic-Artifex/ReactiveUI.Validation --ref sync/upstream/2026-10
```

Verify the completed run's exact head SHA and both OS jobs; a prior green main run is not validation of new implementation. Documentation-only updates need document/link/inventory checks and may reuse unchanged code's recorded results; record that distinction.

AndroidX and native samples remain in [the platform solution](../src/ReactiveUI.Validation.Platforms.slnx), outside core CI/releases. Expanding support requires workloads, platform/API verification, branded consumer tests and deliberate release inclusion. Check project/temp storage before large runs, use conservative concurrency and avoid overlapping matrices. On storage failure, stop and diagnose; remove only task-owned disposable artifacts and preserve shared caches, useful logs and Nix store paths.

## Releases and retirement

Release a tested source commit with a unique version/tag. MinVer settings in [src/Directory.Build.props](../src/Directory.Build.props) use the `runic-v` tag prefix, 8.1 minimum and `runic.0` prerelease identifiers; inspect the produced version rather than infer it from an upstream tag. The [manual release workflow](../.github/workflows/release.yml) builds/tests/packs/verifies both core packages and publishes GitHub prerelease assets. It does not replace the prior Windows gate or publish to NuGet.org.

Preserve `Runic.ReactiveUI.Validation` and `Runic.ReactiveUI.Validation.Reactive` IDs, their current namespace roots, licensing and attribution. Record the source SHA, dependency cohort/release hashes, verification and known limitations in release notes. Published versions, tags and assets are immutable: do not retag or replace assets with different code under an existing version. Corrections receive a new version.

Retire a register entry only when its stated condition is met. Record the replacement/upstream SHA or policy decision, removal commit and verification; retain its ID with `retired` status. Source ancestry alone does not prove behavior. Known failing diagnostic expectations remain open work until an actual implementation and permanent regression establish the fix.
