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

The canonical ignored `artifacts/packages` feed now contains exactly the two
published `.790.17` assets with the verified release hashes above. The local
`.804` verification pair moved to
`artifacts/retained-packages/8.1.0-runic.0.804`; its integration summary preserves
both historical pack-time paths and current retained paths. The previous `.790`
pair remains retained. `artifacts/verification/canonical-published-feed.json`
records the canonical copy. The release description was updated from the reviewed
notes without changing the tag or assets.


## Native runtime and examples follow-up

Started **2026-10-05** from `e653e52eb6abba95d3a5787771df01c4e570e309`.
Status: **released; tag and both assets independently verified**, separate from the immutable `.790.17`
release above. That released pass selected runtime APIs and realistic examples
first and deferred a Validation generator. The [current design](../../generated-validation-design.md)
and unreleased follow-up below supersede that deferral.
No generator package, generator-only runtime surface, dependency upgrade,
platform UI support or SDK/DynamicData source change is part of this pass.

| Topic | Exact source and contract | Evidence and status |
| --- | --- | --- |
| Producer analysis and annotations | `4cdf89ab1d2de08dc3b85c48db2aee7418aa85c1`: enables AOT/trim analyzers for both shipped core producers; removes overly broad RDC and RUC declarations without changing method signatures. | Both producers build with warnings as errors and zero warnings/errors. 48 focused tests per flavor pass. Independently reviewed and adopted in released `f22d2bb`; the full final CI/release gates pass. |
| Explicit observable runtime | `67fdd937cc1aef85bb6b42ca9cd3a31d0e06f195`: six methods (`AddObservableRule` in four forms and two typed observable bindings); [runtime contract](../../examples/native-validation.md). | Independently reviewed and adopted in released `f22d2bb`; +21 baseline lines per flavor. Producer-analysis warnings-as-errors build passes with zero warnings/errors. 12 new [runtime regressions](../../../src/tests/ReactiveUI.Validation.Tests/ObservableRuntimeApiTests.cs) per flavor pass; final integrated core/native gates remain separate. |
| Realistic application examples | Baseline `4c55026`, safe counterpart `85c63a8`; [package corpus](../../../examples/NativeValidation/README.md): generic fields, nullable nested editor/rich typed target, blocking/advisory cross-field rules, row-owned asynchronous state and existing observable foundation. | Both baseline flavors record three managed case passes and the expected required-null-policy gap, plus strict IL2026/IL3050 with Validation call provenance. The final safe fixtures, including adapter correction `07ab9ca`, pass five cases in all three local modes at exact clean `f22d2bb`; final CI and both actual native RIDs pass; published tag/source/assets are independently verified. |
| Native package gates | `ee7d0db`, `ba30d02` and strengthening `c026ec0`, integrated at `eae4984`; [strict runner](../../../eng/verify-native-validation.py): producer/package analysis, exact graphs, trimmed and native publish/run, and CI artifact/source identity. | Final local actual-package gate at exact clean `f22d2bb` passes all three modes with warning-as-error analysis and actual Linux native execution. Final CI and both actual native RIDs pass; published tag/source/assets are independently verified. |
| Future generator | `b2043da`, `ebb726d`, `eba235a`, handoff follow-up `9fb933f` / `ef69fa0`; [current design, retaining historical proof](../../generated-validation-design.md). | Reviewed design and managed compiler proof only. The synthetic annotated call still produces IL2026/IL3050 despite interception. No native or shipping generator claim. |

The [structured evidence](../evidence/native-runtime-implementation.json) records
the audited annotation counts and preserves historical release-probe identity.
Source declarations change from **45 RDC to zero** and **62 RUC to 41**. Each
flavor's existing public API loses **41 RDC and 19 RUC annotations**, preserving
its method signatures. The 41 remaining source RUC declarations protect actual
reflection observation and target assignment. Generated context/helper OAPH
source and current DLL calls were inspected; this narrows those constructor
contracts without replacing their runtime behavior.

The audit first reproduced three constructor-chain IL3050 locations per flavor
from the old internal RDC annotation. Temporarily reopening annotated bodies
exposed **16 distinct IL2026 locations per flavor and zero IL3050 locations**.
Correct boundaries were then restored. Final producer analysis reports zero
warnings/errors; it does not advertise blanket `IsAotCompatible` metadata.

Dated `.790.17` base probes still record **60 successful scenario executions**
across managed/trimmed/native modes and two flavors, with **49 native warning
occurrences per flavor**. The generated consumer still records **ten native
warning occurrences per flavor**. Their diagnostic severity was lowered to
capture behavior and emitted warnings. Neither runner satisfies the new strict
zero-warning gate. Only explicitly recorded safe paths and actual native host
executions establish the new support claim; Linux/Windows managed core gates do
not substitute for native execution on either platform.


### Preliminary NativeAOT candidate verification

Clean source **`eae498422aaf55ef9fea765eaff5027fb9d1a289`** passed the final
local Release warnings-as-errors build with zero warnings/errors, **618 core
tests (309 per flavor), zero failed/skipped**, both ordinary collection examples
and **14 Python guard tests: six new native checks plus eight existing package
checks**. The actual strict package gate passed five safe cases per flavor in
managed, fully trimmed and Linux x64 NativeAOT modes: **30 successful scenario
executions**, zero positive-path warnings/errors. Native executables enforce
`RuntimeFeature.IsDynamicCodeSupported == false`; both retained ILC response
files contain `--parallelism:1` and `--warnaserror`, with no suppression.

| Local candidate package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.790.17.12` / `b5dbbbd465ef73fe212668ed1a47487f91631ad3aecc6611527a7bf16f906a53` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.790.17.12` / `5afe0c7ec174fa5a2bfefe688fcf1d2453263ee23d5566123c7b87a095b207f5` |

Both candidate graphs verify exact package IDs, versions, source SHA and restored
bytes, matching released DynamicData `.5`, no opposite flavor/upstream DynamicData
and no System.Reactive in Primitives. Both baseline strict builds verify the
immutable `.790.17` hashes and required IL2026/IL3050 Validation provenance;
no new baseline native execution is claimed. Independent corpus and package-gate
reviews approved the final implementation.

Ignored root evidence remains under `artifacts/verification/native-gates/` and
`artifacts/verification/native-support/local/`. The tracked structured record
contains the native result snapshot and SHA-256 log/response-file manifest.
These local candidate package hashes are **not published release hashes**.
[CI run 37373253674](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37373253674)
passed all four jobs at this preliminary source, including actual Linux x64 and
Windows x64 native execution against the same CI Linux package artifact. The
later application-adapter fix supersedes this candidate. These results do not
verify the final fresh package pair or its release. The prior `.790.17` published
source, tag and assets remain immutable.


### Final candidate after application adapter correction

The final frozen source is **`f22d2bb42c30d66df19a59333ed4fa633b241940`**.
The library and library-test trees are byte-identical to preliminary `eae4984`;
application-adapter cleanup/handoff corrections and their assertions are new.
The [adapter source/evidence](../../../examples/NativeValidation/evidence/adapter-lifecycle-results.json)
keeps the earlier focused managed before/after proof separate from final package
execution. New [adapter checks](../../../examples/NativeValidation/ApplicationAdapterChecks.cs)
run within `generic-field` under every final consumer mode: initial parent
replacement/null, old/latest/outer detachment, owner disposal rejecting returning
handles, disposed snapshot listeners, and initial getter/callback original-error
cleanup. Equivalent generated-adapter acceptance remains deferred. The release
workflow also targets this fork explicitly; dependency pins remain unchanged.

Fresh local strict verification at clean `f22d2bb` passes all five safe cases in
each flavor under managed, fully trimmed and actual Linux x64 NativeAOT:
**30 scenario executions**, zero positive-path warnings/errors. The native
false-dynamic-code guard and single-threaded warning-as-error ILC response-file
checks pass again. Both immutable baseline strict failures retain the required
Validation diagnostic provenance; no new baseline native execution is claimed.

| Final local candidate package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.790.17.15` / `78c7b65b9f60d5e316cad708f9ebc5545764ed0a3ea98f4917ac412d474c4f1f` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.790.17.15` / `b6f2530fb5b56c256ff29568f7fe8374a5c6d0ce68d9d359c4394f79adbc5a58` |

These final local hashes are separate from preliminary, CI and published bytes.
Final same-head cross-platform CI passes; actual native Windows and Linux
execution is established. Published tag/source/assets are independently verified. The later documentation-only review stamp may reuse verified
unchanged code/workflow evidence under the maintenance policy; its own source
commit will not be presented as the shipping package source.


### Final cross platform verification

[Build run 37375282317](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37375282317)
passes all four jobs in attempt 1 at exact
`f22d2bb42c30d66df19a59333ed4fa633b241940`. Each core OS job passes **618 tests
(309 per flavor), zero failed/skipped**: 1,236 test executions across two hosts,
not 1,236 distinct tests. Both native jobs publish with trimming enabled and
execute five cases per flavor: **20 actual native case checks** across Linux x64
and Windows x64, zero candidate warnings/errors. Each platform pipeline retains
eight existing package guard tests and six native guard tests. Both ordinary
collection examples and independent package consumers pass.

The two native RID reports verify the exact same Ubuntu shipping package bytes:

| CI Ubuntu package | Version / SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation` | `8.1.0-runic.0.790.17.15` / `f0c0a60577c3636d07946a5c69fb00092e553275fc9b026e2fd2a8c6c1b7847a` |
| `Runic.ReactiveUI.Validation.Reactive` | `8.1.0-runic.0.790.17.15` / `ce1ff84059e7167a1e786f323efc9329ff4bbab307a63afb1bfa4ded409d3462` |

These CI hashes differ from the local `.15` pair above and are not published
release hashes. The retained root summary is
`artifacts/verification/native-support/final/ci/37375282317/summary.json`;
the structured record preserves source/package identity, actual RID reports and
artifact digests. Standalone full-trim managed execution is local Linux evidence;
the Windows job establishes actual native execution with trimming enabled.
[Release run 37375833531](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37375833531)
passes the complete matrix plus the publishing job at `f22d2bb`. Published tag,
nuspec source and independently downloaded assets are verified below.


### Native follow up release and documentation stamp

[Release run 37375833531](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37375833531)
passes all five jobs in attempt 1 at exact
`f22d2bb42c30d66df19a59333ed4fa633b241940`. The reused matrix again passes
**618 tests per OS (309 per flavor), zero failed/skipped**, both ordinary
examples and independent consumers, eight core package guards plus six native
guards per platform pipeline, and **20 actual native case checks** across Linux
x64/Windows x64, both flavors, zero positive warnings/errors. The publishing job
uses the same Ubuntu artifact consumed by both successful native RID jobs.

Published [8.1.0-runic.0.790.17.15](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17.15)
uses tag `runic-v8.1.0-runic.0.790.17.15`, resolving to exact `f22d2bb`. Both
nuspecs record that source, .NET 10, ReactiveUI 26.0.1 and matching released
Runic.DynamicData `10.0.0-runic.5`. Release API hashes match an independent
download, and both published assets match the release matrix's Ubuntu artifact
and native RID reports:

| Published asset | Bytes / independently verified SHA-256 |
| --- | --- |
| `Runic.ReactiveUI.Validation.8.1.0-runic.0.790.17.15.nupkg` | 184,532 / `17be4c3b5f46e9ff237b5f89bf97c4ff1f47b5dfef9abd250516cc454c57e95f` |
| `Runic.ReactiveUI.Validation.Reactive.8.1.0-runic.0.790.17.15.nupkg` | 185,104 / `08d6087dbd86e0ae92f1230275351240d8b2e32b7765e5b6c2b31e8ae47b4fd2` |

Ignored independent evidence is retained at
`artifacts/verification/native-published-independent/verification.json`; the
release matrix summary is
`artifacts/verification/native-support/final/release/37375833531/summary.json`.
At that historical release verification stamp, the canonical `artifacts/packages`
feed contained exactly those published bytes;
the useful local `.15` pair is retained under
`artifacts/retained-packages/8.1.0-runic.0.790.17.15/local-f22d2bb`.
Local, final CI and published hashes remain distinct in this review and the
structured evidence. Earlier `.790`/`.790.17` tags and assets are unchanged.
Versions, tags and assets are immutable under fork policy; the GitHub release is
not marked as an enforced immutable release.

The final review stamp changes documentation and evidence metadata only. It
reuses exact released code/workflow verification under the maintenance policy;
its own commit is not the shipping package source. `RUV-016` and `RUV-017` are
active released contracts. That release claims no Validation generator,
whole-library `IsAotCompatible`, retained UI-platform, other RID or bridge/browser
end-to-end support.

## Unreleased generated API follow-up

**2026-10-06: implementation and verification complete; UNRELEASED until
publication.** Tested source is `2550376b230ccfb78beb8d3b4ced8866b65d809e`.
This later documentation/evidence record reuses unchanged implementation and CI
results; its own commit is not the tested/package source. No new release/tag or
published package assets are claimed.

The supported inline-lambda normal API generates direct Validation operations;
explicit Unsafe calls retain reflection and trimming warnings. Normal bodies
without an interceptor throw, including precompiled calls, so consumers must
recompile with embedded analyzer/props or explicitly migrate. Generated normal
property text bindings expose actual initial rule text without the synthetic
empty-list prelude; Unsafe retains its legacy sequence. Both branded package IDs,
namespace flavors, API baselines and released matching DynamicData `.5` cohort
are preserved. The [design](../../generated-validation-design.md),
[recipe](../../examples/generated-validation.md) and
[RUV-018](../../fork-differences.md#unreleased-generated-api-contract) define the
supported selectors/diagnostics, contexts, full paths/strictness, null/default,
complete custom states, target replay and ownership/disposal.

| Topic | Adopting source / retained evidence |
| --- | --- |
| Generated/Unsafe API split and static observation support | `822b873bc687763cc9137b9334a967c0d27159c4`; 18 normal overloads and explicit Unsafe counterparts, safe observable ABI/source correction and both API baselines. |
| Generator and final-compilation dispatch analyzer | `16b717822fb4806d99066c6717960c508e8e87b3`; bundled Roslyn 5.9 analyzer/props, direct notification/getter/setter output, seven error diagnostic IDs and indirect-call checks. |
| Realistic actual-package corpus/gates | `a674a805672676a671aca9392d82aad0cd2a0cac` and assertion correction `b1f7a14`; seven application cases, all 18 overloads, six isolated negative configurations per flavor and exact graphs/source/package identities. |
| Default-rule context interface dispatch | `33e1f28ba93f1539ce7f43921a7d1b8af90a873e`: read `IValidatableViewModel.ValidationContext`, including explicit default implementations and private shadows; both-flavor compiler regressions. |
| Selected-context state interface dispatch | `cf2cd2d3812706141a3157bf9a230027e7404b22`: observe through `IValidationContext` and dispatch `IValidationComponent.ValidationStatusChange` through its interface, including concrete subtype shadows/inaccessible or misleading streams; both-flavor compiler regressions. |
| Binding view reference ownership | Final `2550376b230ccfb78beb8d3b4ced8866b65d809e`: require a reference-type static view contract with INPC, reject concrete struct views with RUVG006, support one stable box accessed through a notifying view interface; both-flavor negative/positive compiler regressions. |

Generated bindings require the view's static type to be a reference type
implementing `INotifyPropertyChanged`. A concrete struct receiver would box event
owners and mutate captured setter copies, so it is rejected with `RUVG006`.
A stable boxed view accessed through a supported notifying interface remains
valid. The final compiler fixture count includes both-flavor negative struct and
positive interface-boxed cases; the six isolated corpus negative configurations
remain unchanged.

The locked SDK 10.0.401 compiler is Roslyn `5.9.0-1.26423.113`; analyzer/driver
references and `analyzers/dotnet/roslyn5.9/cs` match it. The six safe observable
methods use traditional `this` extensions to avoid its reproduced nullable-generic
C#14 synthesized static-bridge CS8714 error. CLR signatures/inferred calls remain
compatible; explicit observable binding calls use `<TSource, TOut>`, while
`AddObservableRule<TValue>` keeps its arity. The narrow SST1703 exception is
justified by that measured bug; no IL suppression or tuple-based signatures are
introduced. The same direct-static nullable callers produce eight CS8714 errors
before correction and zero warnings/errors afterward; inferred receiver callers
already passed. Exact proof inputs/logs are retained under
`artifacts/verification/generator-nullability`, including `results.json`.

### Final verification at 2550376

| Gate | Result / scope |
| --- | --- |
| Clean local Release build and tests | Zero warnings/errors; **683 tests**: 323 per library flavor and 37 compiler-fixture tests. The 14 focused generated-runtime infrastructure tests per flavor are included in the library count. |
| Local guards/examples/package consumers | 20 Python guards (eight package, six runtime-native, six generated); two collection examples and both independent actual-package consumers pass. |
| Local generated actual-package gate | `linux-x64`, all mode: six flavor/stage runs, **42 behavioral case executions**, 12 isolated negative builds, all 18 typed overload shapes in every emitted stage, six retained generated-source files; zero positive warnings/errors. |
| Local existing runtime actual-package gate | `linux-x64`, all mode: six flavor/stage runs, **30 behavioral case executions**, two immutable-release diagnostic baseline checks; zero positive warnings/errors. |
| CI Linux/Windows core jobs | [Run 37386897109](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37386897109), exact `2550376`: both OS jobs pass **683 tests each**, zero warnings/errors, 20 guards, both collection examples and both package consumers. |
| CI generated consumers, each RID | The same run's `linux-x64` and `win-x64` jobs each execute managed, standalone full-trim managed and actual NativeAOT in both flavors: six stage runs, **42 behavioral executions**, 12 negative builds, all 18 overloads per stage and six retained emitted-source files; zero positive warnings/errors. |
| CI existing runtime consumers, each RID | Both native jobs independently execute two flavor-native runs: **10 behavioral executions**, plus two historical baseline checks; zero positive warnings/errors. Native publishing includes trimming; standalone full-trim managed runtime evidence remains local Linux. |
| Artifact/source audit | `artifacts/verification/generated-support/2550376/ci-audit.json`: all four jobs pass, exact source/version/analyzer/props/nuspec and branded/flavor graphs audited; both RIDs' reports match the same verified Ubuntu package pair. Emitted-source and shipping-package hashes independently verified. |

Local and CI package pairs share version **8.1.0-runic.0.790.17.15.10** and tested
source `2550376`, with distinct bytes. CI native/generated jobs consume only the
Ubuntu shipping artifact; Windows core package bytes are not substituted for it.

| Verification artifact / flavor | SHA-256 |
| --- | --- |
| Local Primitives | `ffea7c200bde9893b535dcf3dc9d8fe81ddaba747aad3f95d8b24315218b01c5` |
| Local Reactive | `1f1cb6b0b8404d0914d935306575f2c399b7686e302846b2627f513112ca1a46` |
| CI Ubuntu Primitives | `b68cfe33d5f1bf4aeee612bd2baaffdb25f1a0a918d6594db65e592a844ac800` |
| CI Ubuntu Reactive | `c1c921c16ee79018fcaa4b881c810f850d3018fde70400064b73a0308bd028e6` |

The identical analyzer bundled in both CI packages has SHA-256
`f42985b5b64d0400a5280dc42c799be428331c6d29e1aba78a5d62edaf8bd79d`.
The [structured evidence](../evidence/generated-api-implementation.json) records
local reports, CI RID reports/audit, package hashes, source and the distinct
historical release. Retained local results are
`artifacts/verification/generated-support/2550376/generated-gates/results.json`
and `artifacts/verification/generated-support/2550376/native-gates/results.json`;
CI evidence is retained under that directory's `ci/` child.

The preliminary `b1f7a14` local core/guard/package-consumer gate is superseded by
the context fixes; cancelled CI `37384112590` establishes no final/native result.
Final evidence uses exact `2550376`. Earlier published `.790`, `.790.17` and
`.790.17.15` tags/assets/source and all warning-bearing proofs remain unchanged.
No new published asset hash or release identity is inferred from this verification
version. Released DynamicData 10.0.0-runic.5 and ReactiveUI 26.0.1 remain pinned;
no sibling source adoption, blanket `IsAotCompatible`, all-reflection-removed,
retained UI-platform or bridge/browser support follows.

### Prior verified candidate

Earlier tested source `cf2cd2d3812706141a3157bf9a230027e7404b22` remains a
valid historical candidate record: **679 tests per OS** (323 per flavor plus
33 compiler fixtures), verification version **8.1.0-runic.0.790.17.15.8**, and
all four jobs in [CI 37384820581](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37384820581)
passed. Its local generated/runtime counts were 42/30; CI generated/runtime
counts were 42/10 per RID, with 12 negative builds/two baseline checks. Its local
and CI package hashes remain distinct and are retained in the active evidence's
`priorVerifiedCandidates` array. The exact
[prior full structured record](https://github.com/Runic-Artifex/ReactiveUI.Validation/blob/a6a084670143f1bd7f60565e56ec38f40d921047/docs/upstream/evidence/generated-api-implementation.json)
is preserved at documentation commit `a6a084670143f1bd7f60565e56ec38f40d921047`.
Final `2550376` supersedes that candidate with the reference-view guard and fresh
gates. Neither candidate is a published release; prior bytes/results are not
relabeled as final source evidence.
