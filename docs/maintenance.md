# Maintaining the Runic ReactiveUI.Validation fork

This policy follows the [Runic DynamicData maintenance policy](https://github.com/Runic-Artifex/DynamicData/blob/main/docs/maintenance.md),
adapted to this repository. The [fork difference register](fork-differences.md)
lists the adaptations and regressions to preserve; the
[upstream inventory](upstream/README.md) catalogs upstream issues and PRs.

## Repository and integration ownership

Runic `main` on `origin` is the maintained integration branch, based on upstream
[`cde3062`](https://github.com/reactiveui/ReactiveUI.Validation/commit/cde3062937752abeb4216a92c15f5b3b37be9a34).
Start work from fetched `origin/main`; a local `main` may be behind. Topic
branches carry small logical changes with their regressions.

One integration owner pins inputs, orders dependent topics, reviews conflicts and
verification, and integrates and releases the result. Keep existing Runic and
upstream history. Monthly syncs use real merges preserving both parents; do not
squash syncs, rebase away upstream ancestry or replace the fork with a submodule
wrapper. Preserve MIT licensing and original authorship. This workflow sends no
issues, PRs, comments or contributions upstream.

## Monthly upstream review

Review upstream monthly, sooner for urgent correctness or security fixes. The
[preparation workflow](../.github/workflows/upstream-review.yml) collects inputs;
selection, implementation and integration remain human decisions.

Preparation pins Runic `main` and one fetched upstream commit, collects a fresh
**unassessed** issue/PR inventory and computes a virtual merge without checking
out or executing upstream code. The inherited baseline `cde3062` is not evidence
that a later merge happened; ambiguous ancestry stays explicit. Review branches
are immutable and identified by month and both pins; a repeated run recovers the
matching branch rather than rewriting it. If the restricted `GITHUB_TOKEN`
cannot create a PR, the run keeps the branch and prints a manual compare URL.
Token-created PRs do not trigger CI; dispatch verification explicitly.
Preparation never merges into `main`, publishes packages or contacts upstream.

1. Fetch `origin` and `upstream`. Start from `origin/main` in a clean checkout or
   worktree. Record the Runic base, the previous integrated upstream SHA and the date.
2. Inspect upstream commits since that point, including changes already ported
   selectively. Pick one reviewed upstream commit and record its full SHA.
3. Create `sync/upstream/YYYY-MM` from the Runic base and merge the pinned commit
   with a real merge. Review clean merges as well as conflicts: an upstream build
   change can silently restore targets, package IDs, namespaces or workflows.
4. Put behavioral adaptations in logical topic commits with focused regressions.
   Update the register when a difference is added, changed or retired. Preserve
   both flavors and both .NET 10 API baselines.
5. Run the gates below. Record the inputs, accepted/deferred changes, conflict
   decisions and verification in the sync PR description. A no-change review
   still records its pinned input. A deferred merge does not advance the
   integrated upstream point.
6. Integrate the tested branch into `main` without squashing. If `main` moved,
   merge it into the sync branch and rerun affected checks. Remove task-owned
   branches and worktrees afterwards.

```sh
git fetch origin main
git fetch upstream
runic_base=$(git rev-parse origin/main)
upstream_commit=$(git rev-parse upstream/main)
git switch -c sync/upstream/2026-10 "$runic_base"
git merge --no-ff --no-commit "$upstream_commit"
# Review and adapt the tree before recording the merge and follow-up commits.
```

If the chosen upstream SHA is already an ancestor of the Runic base, record a
no-change review instead of manufacturing a merge. Check ancestry with
`git merge-base --is-ancestor <upstream-sha> <sync-sha>`. A selective port is
not a full sync.

## Coupled dependency updates

Validation consumes **released packages**, not a DynamicData checkout. The cohort
is ReactiveUI/ReactiveUI.Reactive 26.0.1 with Runic.DynamicData/Runic.DynamicData.Reactive
[10.0.0-runic.30](https://github.com/Runic-Artifex/DynamicData/releases/tag/v10.0.0-runic.30)
on .NET 10, pinned in [src/Directory.Packages.props](../src/Directory.Packages.props).

When DynamicData changes, publish its tested pair first, then update both
Validation pins and the versions/checksums in
[eng/restore-fork-dependencies.py](../eng/restore-fork-dependencies.py) together
and verify packaged consumers. Never replace hashes just to silence a checksum
failure. The native gate separately restores the immutable `.5` pair for its
released Validation baseline.

Keep ReactiveUI flavors on a compatible generation with their DynamicData
namespaces and schedulers. No upstream `DynamicData` package may enter either
consumer graph, and Primitives consumers must stay free of System.Reactive.
Renovate proposals are inputs only: NuGet automation does not update the
release-feed bootstrap or prove cohort compatibility. SDK, runtime, platform and
major ReactiveUI upgrades (including ReactiveUI 25 or .NET 11 support) are
support-policy decisions, not side effects of a merge.

## Urgent fixes and conflicts

An urgent fix may be ported ahead of the monthly merge: pin the source, use
`git cherry-pick -x` where possible or cite the source, and add a regression and
register entry. At the next merge, remove redundant adaptation only after
equivalent behavior is verified; keep the regression.

Separate mechanical edits, public contracts and behavioral fixes. Translate
upstream tests to awaited TUnit assertions. Pin initial emissions, membership,
paths, replacement/null behavior, scheduling and disposal when touching those
areas. Use rerere with automatic staging disabled and review reused resolutions:

```sh
git config --local rerere.enabled true
git config --local rerere.autoupdate false
git rerere diff
```

## Validation and development environment

Follow [CONTRIBUTING](../CONTRIBUTING.md), [global.json](../global.json) and CI.
On the Runic NixOS desktop, read `../runic-sdk/.envrc` and `flake.nix`, check
`direnv status`, and reuse the locked shell. From the repository root:

```sh
direnv exec ../runic-sdk python3 eng/restore-fork-dependencies.py
cd src
direnv exec ../../runic-sdk dotnet build ReactiveUI.Validation.slnx -c Release -warnaserror
direnv exec ../../runic-sdk dotnet test --solution ReactiveUI.Validation.slnx -c Release -- --maximum-parallel-tests 4 --report-trx
direnv exec ../../runic-sdk dotnet pack ReactiveUI.Validation.slnx -c Release --no-build -o ../artifacts/packages
cd ..
direnv exec ../runic-sdk python3 eng/verify-packages.py
direnv exec ../runic-sdk python3 -B -m unittest discover -s eng -p 'test_verify_*.py'
```

Build before testing; never use `--no-build` for tests. Packing with `--no-build`
is only valid after building the same source. Review the
[Primitives](../src/ReactiveUI.Validation/PublicAPI/net10.0/PublicAPI.txt) and
[Reactive](../src/ReactiveUI.Validation.Reactive/PublicAPI/net10.0/PublicAPI.txt)
baselines for intentional changes; do not regenerate them to accept drift.

Focused test runs must check discovery and a minimum executed count, not just
the exit code; zero discovered or executed tests is never a pass. TUnit class
segments are namespace-qualified, so filter with a leading wildcard such as
`--treenode-filter '/*/*/*CapabilityCompilerHostTests/*'` (see
[CONTRIBUTING](../CONTRIBUTING.md)).

The generator test host keeps producer and fixture assemblies loaded for the
process lifetime in noncollectible load contexts, caching only assemblies from
exact verified package/path/content cohorts. Each test still creates fresh
generators and drivers, and each executed fixture gets its own context so edited
compilations run their current bytes. This mirrors the
[Roslyn analyzer loader's lifetime](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/DiagnosticAnalyzer/AnalyzerAssemblyLoader.Core.cs#L179)
and avoids collectible unloading under the locked CLR, which contains a
[generic dispatch cache defect](https://github.com/dotnet/runtime/pull/132859).

`verify-packages.py` expects exactly one current package per flavor in
`artifacts/packages`; move or remove older outputs before packing a new version.
All package gates use an isolated NuGet configuration and gate-owned cache,
verify restored graphs and bytes before running consumers, invalidate earlier
success before their own checks, and keep failure logs. A cached package with the
same ID and version is not proof that the selected bytes were exercised.

[CI](../.github/workflows/ci-build.yml) builds, tests, runs the examples, packs
and verifies consumers on Linux and Windows. The
[native workflow](../.github/workflows/native-validation.yml) runs the
[native gate](../eng/verify-native-validation.py) and the
[generated gate](../eng/verify-generated-validation.py) on Linux x64 and Windows
x64 against the same Linux package artifact (see
[NativeAOT and generators](aot-and-generators.md)). Require green CI at the
tested revision before integration or release. Push triggers cover `main` and
`runic/**`, not `sync/**`; dispatch sync branches explicitly and confirm the
run's head SHA:

```sh
git push -u origin sync/upstream/2026-10
gh workflow run ci-build.yml --repo Runic-Artifex/ReactiveUI.Validation --ref sync/upstream/2026-10
```

AndroidX and samples stay in [the platform solution](../src/ReactiveUI.Validation.Platforms.slnx)
outside core CI and releases. Check storage before large matrices, use
conservative concurrency and remove only task-owned disposable artifacts.

## Generated and Unsafe API boundary

Preserve [RUV-018](fork-differences.md#runtime-contracts) in merges: supported
normal calls use generated direct operations or typed/registered plans, and the
`*Unsafe` methods own reflection with accurate `RequiresUnreferencedCode`.
Unintercepted, unregistered normal calls must keep failing actionably; never
restore a hidden reflection fallback. Recognize `ValidationRuntimeDispatchAttribute`
without weakening the final ordinary-call checks.

Both core packages must carry the same analyzer DLL and their flavor-named
`buildTransitive` props. For generator changes, keep both-flavor compiler and
diagnostic tests, inspect emitted getters, notification sources, paths and
setters, and check that generated output never calls expression APIs,
`WhenAnyValueUnsafe` or Unsafe methods, or relies on Binding to rewrite emitted
calls. The generated gate must pass managed, trimmed and native on each claimed
host. The [capability reference](generated-capabilities.md) maps each capability
to its tests and gate cases; update it with the behavior.

The safe observable rule/binding files use classic `this` extensions because the
locked C# 14 compiler's synthesized static bridge reports `CS8714`; their narrow
SST1703 exception records that. Do not restore extension blocks without
reproducing a fixed compiler. This boundary does not authorize dependency updates.

## Releases and retirement

Release a tested source commit under a unique version and tag. MinVer settings in
[src/Directory.Build.props](../src/Directory.Build.props) use the `runic-v` tag
prefix, an 8.1 minimum and `runic.0` prerelease identifiers; inspect the produced
version. The [release workflow](../.github/workflows/release.yml) promotes the
exact Linux package pair from the latest successful trusted Build of the current
`main` SHA, after checking its completed core, native and generated reports,
source, cohort and hashes. `verify_only: true` performs read-only verification;
the default publishes. Nothing is pushed to NuGet.org.

Keep the `Runic.ReactiveUI.Validation` and `Runic.ReactiveUI.Validation.Reactive`
IDs, namespaces, licensing and attribution. Release notes record the source SHA,
dependency cohort, verification and known limitations. Published versions, tags
and assets are immutable: never retag or replace assets; corrections get a new
version. Publication refuses an existing tag/release, uploads to a draft,
re-downloads and compares both assets with the accepted bytes, then creates the
tag at the tested SHA. A failed verification leaves the draft for deliberate
recovery.

A documentation-only commit still moves `main` to a new SHA, and the release
guard requires a successful Build for that exact SHA; dispatch it with
`gh workflow run ci-build.yml --repo Runic-Artifex/ReactiveUI.Validation --ref main`.

Retire a register entry only when its condition is met: record the replacement
or policy decision and verification, and keep the ID with `retired` status.
