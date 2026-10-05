# Implementation review: 2026-10

Started **2026-10-05**. Outcome: **implemented, integrated and released; all local/cross-platform gates
passed, tag and both assets verified**. This record is separate from the
[historical no-change review](2026-10.md). The
[implementation ledger](../implementation-2026-10.md) records full scope,
contracts, decisions and deferrals.

## Inputs and integration

| Field | Value |
| --- | --- |
| Runic topic base SHA | `48a7bde8e4fdbb34f85d700597c87d60c24aff69` |
| Previously integrated upstream SHA | `cde3062937752abeb4216a92c15f5b3b37be9a34` |
| New upstream merge | None proposed by this implementation; integrated upstream point is unchanged. |
| Exact adopting topic commits | Lifetime `925e344`; typed `acf8d71`; contexts `e116e8b` plus ownership `64542c8`; graph guards `df7b48d` plus generated-consumer correction `bd58ee3`; benchmarks `a23693e` plus binding `587c1b0`; scheduler correction `0a94cea`; collection/ordering examples `004a17c`. Full SHAs in the ledger. |
| Released source SHA | `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`; shipping pin `5d433128e0692af112f43a7e09409a43b7210edc`; workflow example smoke follow-up `667978c`. Local core gate ran at `d6d634e`; shipping source/workflows are unchanged in `c9fa501`. Affected consumer gate and both OS CI passed at `c9fa501`, with the successful Linux-only retry in attempt 2. |
| Integration SHA / maintained branch | Runic `main` fast-forwarded from `48a7bde8e4fdbb34f85d700597c87d60c24aff69` to `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`; topic/upstream histories preserved. |

This work implements the authorized worthwhile assessment topics as local Runic
adaptations. It does not advance upstream ancestry merely by adding APIs or
adopting a catalog disposition.

## Accepted and deferred work

Accepted work comprises binding lifetime/disposal, explicit contexts, typed
state bindings, compiled collection examples, command/scheduler regressions,
opt-in benchmarks and package graph hardening. The supplied logical topic SHAs
are adopted into the released source, including the new synchronous raw
membership/current-validity correction and compiled collection/ordering recipes.
All local and cross-platform gates passed, and Runic main is integrated at
`c9fa501`. Release identity and both assets are verified.
The ledger records existing implementations and explicit deferrals for #356,
#474, #41, #66, #833/.NET 11/platforms, source generators, Native AOT and routine
historical dependency/repository changes. #378's selector account is corrected
consistently in the issue table and JSON with the confirming upstream comments.

## Difference register and conflicts

Existing **RUV-001 through RUV-009** remain protected. **RUV-007** now records
exact/flavor/upstream-ID guard hardening. New **RUV-010 through RUV-015** record
binding lifetime, explicit contexts, typed projections, opt-in benchmarks,
synchronous raw domain streams and executable recipes
with adopting commits and permanent regression/source links in the
[difference register](../../fork-differences.md). All cross-platform runtime gates passed.
Combined context adaptation is recorded at `8dc9096`; final conflict/reused-rerere
review evidence remains with the integration owner. Historical diagnostics
at `509dd45` retain their eight executions/four failing expectations.

## Dependency cohort

| Item | Pin / evidence |
| --- | --- |
| SDK / target | 10.0.401 / .NET 10 |
| ReactiveUI / ReactiveUI.Reactive | 26.0.1 / 26.0.1 |
| Runic.DynamicData / Reactive pair | Released 10.0.0-runic.5 / 10.0.0-runic.5 |
| DynamicData source | `edd2d175794afc86e964a06cb55329f44793009f` |
| Asset integrity | [Recorded bootstrap hashes](2026-10.md#dependency-cohort-and-existing-verification); unchanged. |
| Unreleased sibling improvements | Future cohort only; no sibling project/source substitution. |

## Verification

| Check | Exact source / evidence | Result |
| --- | --- | --- |
| Documentation consistency / local links | Initial documentation topic; 83 local links/anchors, JSON counts 53/936 and consistent #378 correction | Passed; documentation only. |
| Scope reconciliation | Integration-owner confirmation: all 53 issues, relevant runtime requests and all 12 nondependency closed-unmerged PRs | Scope covered; worthwhile topics adopted in the released source. |
| Bootstrap / exact dependency graphs | Bootstrap at `d6d634e`; full restored consumer graphs at `c9fa501` | Released package hashes verified; exact flavor/version graphs passed. |
| API parity review | Shipping pin `5d433128e0692af112f43a7e09409a43b7210edc` | Approved: +54/-0 API lines and 14 new methods per flavor, preserving every original line with matching contracts. |
| Release warnings-as-errors build / both API baselines | `d6d634e43d109b848a06b960a29704aede2b100f` | Passed: zero warnings/errors. |
| Focused regressions / both flavors | Permanent lifetime/context/typed/scheduler/collection classes, compiled in full suite at `d6d634e` | Included in passing full shipping suite. |
| Full shipping tests / both flavors | `d6d634e43d109b848a06b960a29704aede2b100f` | 594 passed: 297 per flavor; zero failures/skips. |
| Ordinary collection examples | Both projects at `d6d634e` | Passed in both flavors. |
| Python negative graph checks | `d6d634e`; `eng/test_verify_packages.py` | 8 passed. |
| Benchmarks / CLI | [Final report](../../../benchmarks/results/2026-10-05-final.md), tested source `8fd9847716916d9436ebfab908ec74b9bb922a89`, runtime tree equal to shipping `5d433128e0692af112f43a7e09409a43b7210edc` | Release warnings-as-errors build passed; 11 workloads discovered per flavor; 18 CLI checks and 6 selected ShortRun measurements passed. |
| Pack / expanded independent consumers | Actual `.804` pair from clean `d6d634e`; corrected verifier at clean `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a` | Both consumers compiled/executed all assertions and passed full exact-version/flavor graphs. Eight negative graph tests reran and passed. |
| Linux / Windows core gates | [CI run 37363825486](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37363825486), exact `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a` | Passed attempt 2: 594 tests per OS (297 per flavor), zero failed/skipped; 8 guard tests per OS, zero build warnings/errors, both examples and expanded consumers passed. Linux retry and carried Windows success use the same SHA. |
| Preserved upstream ancestry | `git merge-base --is-ancestor cde3062937752abeb4216a92c15f5b3b37be9a34 c9fa501c4d2e9442d85693dae77bb6f727e9eb7a` | Passed; integrated main and the verified release tag both target `c9fa501`. |

The prior baseline's 402 passing tests and earlier CI run are historical
evidence. They do not verify later implementation. Platform workloads, Native
AOT, source-generator behavior and input-to-paint performance are not established
by core or benchmark compilation.

Local evidence is retained under `artifacts/verification/integration/`:
`summary.json`, `affected-gate-scope.json`, both TRX reports,
`11-package-guards-fixed.log` and `12-packed-consumers-fixed.log`.
These paths are ignored local evidence, not portable repository links.
`artifacts/verification/topics/inventory.json` retains 190 focused topic/SDK-pin
and benchmark evidence files (6,022,098 bytes), with original/retained source
paths and SHA-256 checksums. Worktrees were preserved at capture. Portable
behavior and measurement references above link to tracked source and reports.
The source diff confirms no `src`, SDK/cohort or workflow changes between
`d6d634e` and `c9fa501`; the corrected verifier reused the exact built
package pair and reran affected guard/consumer gates. The first MTP invocation
with `-m:2` exited with zero discovered tests; removing that argument let the
corrected invocation run the actual suite once. That failed invocation and
the initial generated-helper CA1050 logs are retained. Neither is a shipping
test failure hidden from the 594-test result.

| Local verification package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.804` / `db6d9d6c881dc1e56ed5d9ebae2fec776f75b6be90ce025c2c947be685820e10` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.804` / `4fad392005179f8a9fa188b52d0ad9befbc178bb1371b52e3940d6b6414335f4` |

Windows CI at `c9fa501` produced **`8.1.0-runic.0.790.17`**, rather than
the local `.804` pair. CI had the published `runic-v8.1.0-runic.0.790`
tag metadata; that tag was fetched locally after the local verification pack.
These are distinct package identities and bytes, even though shipping source
and dependency cohort match. Neither verification version establishes the final
release version. Ignored local CI evidence is retained at
`artifacts/verification/ci/37363825486/windows-summary.json`.

| Windows CI verification package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.790.17` / `f1a7e2c59de853f47f5818bcb186cb47c4f4ff1062f9335fb0ed8dc1a5d5c15f` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.790.17` / `b6f5f1eb22773e07b478f6b1d789607b6ce21c487e56db57cf6dda973f1b5543` |

Workflow attempt 1 failed overall because Ubuntu never acquired a hosted runner
and executed no job steps. Its annotation was: "The job was not acquired by
Runner of type hosted even after multiple attempts". Windows passed its entire
job, including all 594 tests, examples, packing and consumers. The integration
owner reran only Linux job `111944298107` with `gh run rerun --job` at the
same `c9fa501` source; attempt 2 passed all Linux gates and carried Windows
success, completing the cross-platform workflow. No source change or Windows
repeat is required by this infrastructure retry. This is not a shipping-code
test failure. Preserve Windows attempt-1 evidence alongside the successful Linux
attempt-2 result.

The Windows workflow artifact digest is
`680be097ac58fde2178160e43a1b2c2c818e730f7850e493719f4625fd0053ce`.
This digest identifies the workflow artifact, rather than either package hash.

Linux attempt 2 also produced verification version `8.1.0-runic.0.790.17`.
Both OS jobs passed **594 tests each (297 per flavor)**, with zero failed/skipped,
eight negative graph tests each, zero build warnings/errors, both ordinary
examples and both expanded consumers. These are the same 594 tests exercised
on two operating systems. Retained local evidence:
`artifacts/verification/ci/37363825486/final-summary.json`.

| Linux CI verification package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.790.17` / `0b24a7e153e81a1ce947ab00e36cf111b826ee480611b4d6014867f1c17d25b1` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.790.17` / `a920a37149fb24d178c6669642df21229225089310abfa8e12213c5e1377e2b0` |

Linux artifact `11368590458` has digest
`31b965419848cfc0a12ab7f9d9bf5d88ef2a5790686221df393c5a9c9f5f8371`.
CI verification hashes remain distinct from the published release assets below.

Package verifier correction `bd58ee3` has independent review approval:
namespace/`Program.Main` wrapping preserves assertion behavior, all eight
negative tests pass, and no analyzer suppression was added. Runtime/API source
`5d433128e0692af112f43a7e09409a43b7210edc` and benchmark topic `40b3a45`
have their separate independent review approvals.

## Scheduling correction and verification boundaries

Correction `0a94cea64958de8221059dbcef03d791d05544fc` addresses the newly
reproduced SDK model-turn stale `GetIsValid()`, `HasErrors` and command
admission after adding an invalid rule. Raw membership, `Valid` and
`ValidationStatusChange` now update synchronously on the owner. Public
`Validations` delivery therefore moves to that owner; native/UI consumers
must dispatch at their presentation boundary. `IsValid` and `Text` OAPH
properties remain scheduled. This is separate from the original passing
property-notification probe and is an intentional observable behavior change.

Shared collection/ordering examples and regressions are adopted at
`004a17c84c264f50ece8f65400fe5373668bfa4f`. Optional SDK scheduler probes
compile actual SDK source at `e878a4f361a7c4b9326f54663defa7debe41adb9`;
they do not verify bridge routing or browser rendering end to end. Preserve the
earlier focused source-probe evidence at the exact reviewed SDK input. During
the local final gate the SDK checkout moved to clean
`e6de9ca3c6073aae33dc5fadbe6ebd5ec3de58df`. A Primitives probe run against
that unpinned input is excluded from final evidence; the Reactive SDK probe was
intentionally not run. No final pinned-SDK probe result is claimed. Workflow
follow-up `667978c` adds Python negative graph checks and both ordinary
collection examples, without running SDK-source probes.

Legacy text-property callback bindings retain an empty prelude; typed and
selected-context property bindings seed actual rule states. Custom class and
struct rule metadata reaches typed converters, and helper replacement has
permanent coverage. See the ledger and adopted examples for exact absence,
initial-emission, ownership and scheduler contracts.

## Release

Runic main is integrated at `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`.
[Release workflow 37365832750](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37365832750)
passed its own Linux build, tests, ordinary examples, pack and consumers, then
published [`8.1.0-runic.0.790.17`](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17) under tag `runic-v8.1.0-runic.0.790.17` on **2026-10-05 at 19:50:16 UTC**.
The lightweight tag targets exact `c9fa501`. Release job `111950642718` passed
594 tests (297 per flavor), zero failed/skipped, eight negative guard checks,
zero build warnings/errors, both ordinary examples and both expanded consumers
with their full dependency graphs. Published asset API SHA-256 values match
downloaded bytes; both nuspecs verify the package version, .NET 10,
ReactiveUI 26.0.1, matching DynamicData `.5` and repository commit `c9fa501`.

| Published release asset | Verified SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation.8.1.0-runic.0.790.17.nupkg` | `5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df` |
| `Runic.ReactiveUI.Validation.Reactive.8.1.0-runic.0.790.17.nupkg` | `e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b` |

Ignored local evidence is retained at
`artifacts/verification/release/8.1.0-runic.0.790.17/summary.json`, with the
release logs and two downloaded assets. This is a GitHub prerelease; no NuGet.org
publication occurred. The prior `runic-v8.1.0-runic.0.790` tag remains at
`509dd45b7f8458a22ae4cb758b1b68e72d874217`; its pointer was checked read-only.
The local `.804` pair and CI packages are separately recorded verification
artifacts; release hashes must be read from the actual published assets.
The redundant queued main-push Build was cancelled after the exact same source
passed cross-platform CI. This final documentation-only update reuses that
unchanged-source evidence and does not alter the published source/tag/assets.
