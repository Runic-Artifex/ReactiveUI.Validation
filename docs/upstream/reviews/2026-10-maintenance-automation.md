# Validation maintenance automation: 2026-10-06

**Adopted at `eb72dcd0d1ef0962b1ebb520e40b774c8e641ed7`.** Main Build
[37396739461](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37396739461),
monthly preparation
[37396767234](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37396767234)
and release `verify_only`
[37397684566](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37397684566)
succeeded. No package was published. See
[final adoption and release verification](#final-adoption-and-release-verification).
The earlier audit and pending observations below are dated history.

This record covers maintenance preparation, package evidence and guarded release
promotion. It does not adopt upstream implementation code, publish a Validation
package or change the Generated API's **UNRELEASED** status. Earlier October
research, release receipts and RUV-001–018 remain historical evidence.

## Initial audit inputs and decisions

The audit and maintenance topics started from Runic main
`e9d853935403d5c1ec14660722a225e50e7ee3ea`, containing the released DynamicData
`.30` adoption. Concurrent authorized generator interoperability work later
advanced main to `90a44fcbcb627f6496767181f9adbbb5db74aa5a`; its files and
worktree are separate from this maintenance scope. Integration must preserve it
and verify the resulting revision.

The comparator was DynamicData main
`27346e4a9f5ec77343f856c10760ea04165c5ae5`, including reviewed monthly preparation,
release asset guarding and packaged acceptance. Validation already has stronger
matching-host NativeAOT and Generated application coverage; that coverage is
retained rather than duplicated.

| Gap at the audited Validation base | Maintenance decision |
| --- | --- |
| Monthly cadence is manual, with no preparation workflow. | Prepare pinned review inputs and immutable same-fork review branches/PRs. Do not execute upstream code, integrate it or contribute upstream. |
| Ordinary consumers can resolve shared global-cache bytes without proving they match the selected packages or current source. | Use gate-owned caches, isolated XML feeds and input/restored hash/source checks before execution. Preserve ordinary consumer behavior. |
| Native/Generated reruns can leave an older successful report after an early failure. | Invalidate success before prerequisite checks, retain partial/failure evidence and require explicit current completion. |
| Release dispatch can select another ref and directly publish without verifying new tag/draft asset bytes. | Promote the accepted pair from the latest trusted exact-main Build, validate reports/metadata, refuse existing release state, verify draft downloads and new tag before publication. Explicit `verify_only` creates no release state. |
| Artifact transfer and ordinary failure retention are weaker than the comparator. | Verify uploaded/downloaded contents and retain logs, graphs and current reports, including failures. |

The stale-report finding was reproduced independently without a build: seed a
temporary output with a previous success, invoke each strict verifier with a
mocked incorrect SDK, and observe rejection while the old `results.json` remains
unchanged. Fresh hosted jobs and the existing release workflow's required
verification dependency already reject failed jobs; the finding concerns rerun
and evidence reliability, not a demonstrated publication bypass.

## Contracts to retain

Preparation records inherited upstream
`cde3062937752abeb4216a92c15f5b3b37be9a34` separately from a later proven real
merge. It records absent/ambiguous ancestry honestly. The fresh inventory is
unassessed for that review and never copies historical completion decisions.
Month/base/upstream identify immutable snapshots and branches; retries recover
matching state. For the inherited linear baseline, the manifest reports
`inherited-shared-merge-base` and explicitly says no real upstream merge was
found. Later imported merge parents provide the separate real-merge evidence.
Snapshots live under `eng/upstream/reviews/YYYY-MM-base12-upstream12`; a retry
retains the first branch snapshot and archives the selected snapshot with its
branch status. Collection failures retain artifacts. If repository token
settings block a PR, retain the branch and exact manual compare URL. Human review,
a real merge and implementation verification remain subsequent decisions.

Release verification consumes an existing exact-main Build rather than deriving
a new package version through another build. Both branded assets must have the
same version, correct source/dependencies, MIT attribution and Generated payloads.
The latest eligible trusted run and current core/native/generated jobs must have
succeeded. A verification-only dispatch does not create a tag, draft or uploaded
release assets; set `verify_only: true` explicitly because the workflow default
is publication. Publication requires no existing tag/release,
verified draft bytes and a new tag resolving to the accepted source. No new
Validation release is part of this task.

The supported cohort remains .NET 10, SDK `10.0.401`, ReactiveUI/ReactiveUI.Reactive
`26.0.1` and the SHA-verified released Runic DynamicData `.30` pair. The immutable
`.5` pair remains solely the released `.790.17` expected-failure baseline.
Primitives remains free of System.Reactive and neither flavor may restore
upstream DynamicData or the opposite flavor.

Core CI retains Linux/Windows builds and tests plus ordinary/collection examples.
Both native host jobs consume the same accepted Linux shipping package pair.
Package artifacts include a source-bound manifest of both SHA-256 hashes and
sizes. Core jobs verify uploaded/downloaded contents; native jobs verify the
downloaded manifest and pair. Preserve the existing eight artifact names and
always-upload diagnostic reports/logs/graphs, with missing-file failures explicit.
Ordinary evidence lives under `artifacts/verification/package-smoke/`, with
`results.json` and flavor restore/runtime logs and graphs. Native and Generated
retain their respective `artifacts/verification/native-gates/` and
`artifacts/verification/generated-gates/` evidence directories. All reports
begin incomplete and retain partial checks; only normal successful completion
sets `completed: true`.
Hosted runtime Native mode produces four checks: one positive native executable
and one legacy expected-failure check per flavor, with five application cases in
each positive executable. Generated all-mode produces eighteen checks: six
managed/full-trim/native positives plus twelve expected diagnostic checks, with
seven application cases per positive executable and the intercepted overload
inventory preserved. These gates do not establish blanket package AOT support,
Android/native samples, actual Terra UI execution or ReactiveUI 25 compatibility.

## Initial evidence and adoption status

Observation **2026-10-06**: the integration owner reports audited-base Build
[37392395506](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37392395506)
successful across its four jobs. That result identifies `e9d8539`, not the later
maintenance implementation or concurrent main revision. Detailed topic commits,
guard results and integrated hosted evidence will be recorded after independent
review. No new workflow run, package publication or server-side immutable-release
setting is claimed by this record.

The integration owner must record the final source and exact successful hosted
run, package/report hashes and any remaining limits before treating these
operations as adopted. Preserve failures and distinguish local/offline proofs
from actual Windows/native execution. RUV-019–021 identify the contracts; they
do not advance upstream ancestry or replace RUV-018's release boundary.

## Integrated preparation and PR evidence

Observation **2026-10-06**: [PR #2](https://github.com/Runic-Artifex/ReactiveUI.Validation/pull/2)
was merged without changing its tested tree, producing maintained-main source
`eb72dcd0d1ef0962b1ebb520e40b774c8e641ed7`. The following reviewed topics are
included; their local proofs do not substitute for integrated hosted execution.
Compared with concurrent main `90a44fc`, maintenance changes are confined to
workflows, `eng` and documentation: `src`, examples, investigations, dependency
pins and `global.json` are unchanged.

| Topic | Reviewed commit |
| --- | --- |
| Monthly preparation and portable fixtures | `3255daa44b094fe7ec2482b143b763d735e8ffa1`, `5cf9c578b6d8f94950056fa01c22611ca834de50` |
| Package byte/cache/report evidence | `8832c17ff7df59034deaba757cf6b73132f4c965`, `f4a824885fbb60cbd3565109118b97f128b6de2a` |
| CI artifact and failure evidence | `e4275a2a3e882e7bef8d5ebc0e784e9cd657a943` |
| Release promotion safeguards | `3ac38699ebb5669f796feca5172d5b4d43d6fc99` |
| Windows checkout long paths | `05854381407d1577ff9a2098e0e15e1405b00cc2` |

The first PR run,
[37395508226](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37395508226),
hit Windows' filename-length limit while checking out retained interoperability
evidence and was deliberately cancelled after being superseded. The reviewed
fix enables Windows long paths before checkout; it changes only workflows and
does not remove the retained evidence or alter library behavior.

Corrected PR Build
[37395749702](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37395749702)
passed all four jobs. Its API head was
`d9e36470bf5fd204d92889a39f2030e7820db7c0`; actual checkout/package source was
the synthetic merge `dcd0af33d8b4070d1264c39e1764f419965bf0ef`, tree
`3ddd0bf682d04903a348bccfea4aa8f4b9333229`. It passed 1,366 core tests with
zero non-pass results, four ordinary executions, sixteen Generated executions
(112 application cases) and 48 expected negative compilations, plus four strict
native executions (20 cases) and four expected legacy IL2026/IL3050 failures.
All eight artifact ZIP digests matched API/upload/download evidence. One
optional release-fixture replay was skipped because historical local evidence is
absent on a fresh runner; nine hermetic release guards passed and the new eight
artifacts were subsequently validated read-only. Application checks had no skips.
The PR fixture explicitly remained ineligible for main release promotion.

First hosted monthly preparation
[37396767234](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37396767234)
succeeded at `eb72dcd0d1ef0962b1ebb520e40b774c8e641ed7`. Upstream main was
`cde3062937752abeb4216a92c15f5b3b37be9a34`: inherited shared ancestry,
zero candidate commits, a clean virtual tree matching `3ddd0bf`, and no upstream
code execution. The inventory contains 989 records: 53 issues, 936 PRs and five
open items, all 989 unassessed. Independent paginated REST counts matched.

The immutable branch
`review/upstream/2026-10-eb72dcd0d1ef-cde306293775` points to
`d5ff2bf5b8ac67ff3cfbba1d1030ec9f535d1ae0`, whose sole parent is the pinned
base. Its three snapshot files are byte-identical to artifact `11383219705`,
whose ZIP SHA-256 is
`58a9d6838348dff7ca022e570f14b74440d91a2e1a569749508491b7a5b20707`.
GitHub Actions' PR-creation restriction deferred the PR; no PR was created for
that branch. Its artifact retains the
[manual compare URL](https://github.com/Runic-Artifex/ReactiveUI.Validation/compare/main...Runic-Artifex:review/upstream/2026-10-eb72dcd0d1ef-cde306293775?expand=1).
This is a preparation result, not a new upstream merge or item assessment.

Reports are retained under
`artifacts/verification/maintenance-automation-2026-10-06/`: the superseded
failure, `hosted-37395749702/verification-summary.json` and full report,
`monthly-37396767234/verification.md`, inventory validation and branch/artifact
proofs. Main Build `37396739461` and explicit release `verify_only` evidence are
still pending in this observation. No new Validation package has been published.

## Maintained-main Build proof

Later observation **2026-10-06**: main Build
[37396739461](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37396739461)
completed successfully at exact checkout/package source
`eb72dcd0d1ef0962b1ebb520e40b774c8e641ed7`, tree
`3ddd0bf682d04903a348bccfea4aa8f4b9333229`. All four jobs and eight downloaded
artifact ZIP digests were independently verified against API and upload
evidence. Reports are current, complete and bound to their package manifests,
input/restored bytes and the active/legacy dependency cohorts.

The physical main results match the separately recorded PR counts: 1,366 core
tests passed with zero non-pass, four ordinary executions, sixteen Generated
executions/112 cases and 48 expected negative compilations, four strict native
executions/20 cases and four legacy expected failures. Both native hosts exercised
the exact Ubuntu candidate pair. The optional historical-fixture replay remained
the sole skip; application checks had none. The artifact action emitted an
internal DEP0005 deprecation warning; strict digest and byte comparisons passed.

The unpublished candidate version is `8.1.0-runic.0.790.17.15.15`.

| Host / package | SHA-256 |
| --- | --- |
| Ubuntu / `Runic.ReactiveUI.Validation` | `fbbc2ecf2b4677372c7f3a3bab99dcf66b78f3fcbc96eb3d1ba5baaa940fbccf` |
| Ubuntu / `Runic.ReactiveUI.Validation.Reactive` | `c25bccd322b96bbe05828c8c534954073633cf89f14e059070aa484282e573c4` |
| Windows / `Runic.ReactiveUI.Validation` | `bb1ea1236be734bb6656f3a90d54a3ecec431e9391bc18807c5838215b194553` |
| Windows / `Runic.ReactiveUI.Validation.Reactive` | `db311cbb89f4eeabfd275100c67d06ca10526654d00e60e7d97e4657e54bc572` |

These are this main run's bytes. The earlier PR candidates carry the same version
but a different repository commit and hashes; no artifact is substituted between
runs. Canonical reports are
`artifacts/verification/maintenance-automation-2026-10-06/main-37396739461/verification-report.json`
and `verification-summary.json`, with all eight archives and physical report/graph
proofs retained alongside them. No candidate has been published.

The integration owner dispatched release run `37397684566` exactly once with
`verify_only: true`; its hosted verification and unchanged remote tag/release
identity checks remain pending before final adoption is recorded.

## Final adoption and release verification

Final observation **2026-10-06: RUV-019–021 adopted at
`eb72dcd0d1ef0962b1ebb520e40b774c8e641ed7`**. Hosted release verification
[37397684566](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37397684566)
succeeded at that exact main source with explicit `verify_only: true`.
Resolve, strict artifact download, evidence verification and verification-only
completion passed; publication was skipped. It selected the latest eligible
main Build `37396739461`, whose four job identities and eight artifact identities
and digests match the independent main report. The selected Ubuntu shipping
pair matches the `fbbc2ecf`/`c25bccd3` hashes above.

The guard artifact ZIP SHA-256 is
`471eb5fb93f3a565305c1e3a7237c125ac9e966484b4385500759cb059969ea4`,
matching the API digest. Its `source.json`, `download-metadata.json` and
`verified.json` are retained in
`artifacts/verification/maintenance-automation-2026-10-06/release-verify-only/hosted-37397684566/archives/release-guard-evidence-37397684566.zip`.
The before/after comparison in the same canonical `release-verify-only/`
directory, `immutable-state-comparison.json`, confirms all three releases and
40 tag identities unchanged, including release/source/publication state and
asset IDs, names, sizes and digests. Download counters are intentionally excluded.

No new release, tag or uploaded release asset was created. Actual publication,
draft byte comparison and tag creation remain covered by offline failure
fixtures, not a new hosted publication. The Generated API stays **UNRELEASED**,
and all existing releases and the active `.30`/legacy `.5` boundary are preserved.
The monthly review branch remains a preparation artifact with its PR deferred;
its inventory is still unassessed and does not advance upstream integration.

This final receipt changes documentation only and reuses the recorded unchanged
implementation's gates; it does not relabel `eb72dcd` packages as a later docs
commit. Once the `[skip ci]` receipt advances main, a future promotion needs a
new successful exact-current-main Build, as specified in the
[release policy](../../maintenance.md#releases-and-retirement). The initial pending
observations above remain dated history, superseded by this completed evidence.
