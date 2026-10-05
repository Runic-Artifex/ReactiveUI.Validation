# Validation implementation ledger: 2026-10

Started **2026-10-05** from Runic base `48a7bde8e4fdbb34f85d700597c87d60c24aff69`.
This is the live decision and adoption record for the worthwhile work identified
by the [dated assessment](README.md). The [implementation review](reviews/2026-10-implementation.md)
records integrated inputs and verification. The [original October review](reviews/2026-10.md)
and [diagnostic evidence](evidence/README.md) remain historical records.

Status is **implemented, integrated and released; tag and both assets verified** at released source
`c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`. Shipping code is pinned at
`5d433128e0692af112f43a7e09409a43b7210edc`; workflow follow-up `667978c` adds
negative-graph checks and ordinary collection example smoke. The integration
owner supplied the logical adopting commits below. All shipping topics are
implemented in the released source. The local build, 594 tests and both ordinary
examples passed. Both expanded packaged consumers and their full graphs also
passed at `c9fa501`. Cross-platform CI passed in attempt 2 at the same source;
release identity and assets are verified.
Historical test counts are kept separate from current verification. The [difference register](../fork-differences.md) remains
the source of stable fork contracts.

## Worthwhile implementation topics

| Topic / sources | Decision and contract | Status / adoption | Required evidence |
| --- | --- | --- | --- |
| Binding lifetime: #23, #152, #659/#660, #665/#879, reproduced diagnostics | Switch to the current ViewModel/helper/context; detach replaced and null sources; make binding disposal idempotent. Preserve property, aggregate, helper and callback behavior in both flavors. | Adopted: `925e344e0ed1d8b26ff8f128167a2fa476b35067`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Replacement/null/helper cases, stale-source detachment, double disposal and post-disposal updates; both flavor suites. |
| Multiple contexts: #511; existing #35/#474 abstraction | Add explicit-context rule registration and context-selected binding while retaining the default context APIs. The selected registration context owns helper removal. Advice stays independent of blocking validity. | Adopted: API `e116e8bf808ebbf37a7b4a5177729bf3d89b8193`, default ownership `64542c88e03951f0158de0e36874549a0be1b80f`; combined at `8dc90961e3b6e5dfd326004749e6b81447c5d093`. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Blocking/advisory independence, clearing/disposal, context replacement/null, both API baselines and both flavors. |
| Typed state binding: #463 | Add distinct `BindValidationState` projections for typed targets and actions. Define empty and multi-rule state explicitly; preserve text formatter overloads. | Adopted: `acf8d7178b639d68ea3b5eaf474fdf5d521d4321`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Bool/enum projection, mixed rules, empty state, replacement/null/disposal and both API baselines. |
| Collection recipes: #173, #450, #433 | Compile fully imported examples for both flavors with an explicit initial empty snapshot, stable identity, child refresh and same-key child replacement. Separate domain validity from touched/submitted presentation. | Adopted: `004a17c84c264f50ece8f65400fe5373668bfa4f`; local and both OS example/suite gates passed; released in `8.1.0-runic.0.790.17`. | Empty/add/edit/remove/clear/replacement scenarios, current validity and property/entity errors. |
| Command and notification scheduling: #19, #31, #34, #70, #92/#95/#97, #515/#879 | Document and test serialized model mutation, validation and command admission using an explicit flavor scheduler. Preserve the distinction between synchronous current validity and deferred notifications. | Adopted: correction `0a94cea64958de8221059dbcef03d791d05544fc`, ordering example/regressions `004a17c84c264f50ece8f65400fe5373668bfa4f`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Same-turn invalidation, queued work, cross-field/membership changes, asynchronous completion and notification-order regressions. |
| Benchmarks: #117; inherited #878 baseline | Add an opt-in .NET 10 BenchmarkDotNet solution for both flavors outside shipping solution/release gates. Measure representative construction, change, collection and replacement/disposal workloads before optimizing. | Adopted: initial `a23693e7990c9ba2539237db8ca5533ca8d54102`, binding workloads `587c1b0bf11c1b02b6adb0df34d85e7b8eeecd48`; CLI/source-pin fix `8fd9847716916d9436ebfab908ec74b9bb922a89`, report topic `40b3a45`. Selected final measurements/CLI checks passed; no full timing matrix. | Both projects compile; bounded smoke execution and workload coverage. Measurements are scenario evidence, not input-to-paint or blanket performance claims. |
| Dependency and package boundaries: #500/#679/#696/#829/#874/#967/#979, #691/#933/#990 | Retain the released matching DynamicData pair and formatter fallback. Strengthen independent consumer checks for both upstream DynamicData IDs, opposite flavor, exact resolved versions and Primitives' System.Reactive exclusion. | Adopted: `df7b48de7a2191171c7243484a785a36850e749c`, generated-consumer correction `bd58ee3e052670a4e320388d61d7b368735a7c04`; actual .804 pair passed both integrated consumers/full graphs at `c9fa501`; both OS CI gates passed. | Package verification plus deliberate negative graphs; independent packed consumers on the final integrated SHA. |
| Catalog correction: #378 | The reported selector calls `view.FindControl<TextBlock>("UsernameError").Text`; move control lookup behind a property and bind `view.UsernameError.Text`. The reporter confirmed this fix. | Adopted documentation: `ac6e6ed`; included in candidate `e9ab48a`. | [Suggested property-only selector](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-957330539) and [reporter confirmation](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-960270058); issue table and JSON agree. |

## Implemented behavior and scheduling correction

The released source includes permanent [binding lifetime regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationBindingLifetimeTests.cs),
[typed state regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationStateBindingTests.cs)
and [context regressions](../../src/tests/ReactiveUI.Validation.Tests/MultipleValidationContextTests.cs).
These cover model/helper/context replacement, null selection, cleanup ownership
and idempotent disposal in shared source for both flavors. The
[context example](../examples/multiple-contexts.md) and
[typed binding example](../examples/typed-state-bindings.md) describe the adopted APIs.
Typed helper/property converters preserve custom class and struct rule metadata;
ordinary aggregate context states retain their existing aggregation contract.

Legacy `BindValidation` property callbacks retain an empty-state-list prelude
on selection. A consumer using `All(IsValid)` may interpret that empty list as
valid. New typed and selected-context property bindings seed actual membership,
do not invent valid states for existing invalid rules, and wait for each active
rule to supply an initial state. Both surfaces are presentation observations;
command admission requires its own current-validity/pending policy.

The explicit SDK model-turn probe reproduced a separate scheduling bug: adding
an invalid rule left `GetIsValid()`, `HasErrors` and command admission stale
until queued membership publication. Correction `0a94cea` now updates raw
context membership, `Valid` and `ValidationStatusChange` synchronously on
the owning model context; `IsValid` and `Text` OAPH presentation properties
remain scheduled. **Public `Validations` observable delivery changes to the
model owner**. Native/UI consumers must dispatch at the presentation boundary
when they need another scheduler. This does not change global RxApp defaults or
promise arbitrary concurrent mutation. Preserve the
[scheduling regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationContextSchedulingTests.cs)
and [ordering regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationOrderingTests.cs).

The [collection recipe](../examples/collection-validation.md) compiles shared
source for both flavors and covers initial empty/add/edit/remove/clear, same-key
child replacement, old-child detachment and disposal. Its stable cache itself is
not replaced; entire-source replacement requires a deliberate inner switch and
ownership policy. Touched/submitted flags affect presentation, not validity.
The [ordering recipe](../examples/validation-ordering.md) uses actual SDK model
context/adapter source pinned at `e878a4f361a7c4b9326f54663defa7debe41adb9`
through opt-in `RunicSdkRoot` projects. This is source-level SDK scheduling
coverage, **not bridge/browser end-to-end verification**. CI runs Python negative
graph checks and both ordinary collection examples; it does not run these
SDK-source probes.

The API parity review at shipping pin `5d433128e0692af112f43a7e09409a43b7210edc`
confirmed preservation of all original public API lines, with **14 new methods**
in each flavor and matching namespace/annotation contracts. All runtime gates passed; release identity and assets are verified.

The [benchmark report](../../benchmarks/results/2026-10-05.md) distinguishes the
initial warmed baseline at `48a7bde` from the later binding workload smoke at
`73bedbb`. Those measurements are not relabeled as final-candidate timings. The separate
[final report](../../benchmarks/results/2026-10-05-final.md) records committed
workload source `8fd9847716916d9436ebfab908ec74b9bb922a89`, whose runtime tree
matches shipping `5d433128e0692af112f43a7e09409a43b7210edc`: six selected
ShortRun measurements at ten rules, 18 CLI checks and a clean Release
warnings-as-errors benchmark build. These shared-host observations establish
no speed ranking, historical improvement or UI-latency claim.

## Coverage and retained behavior

The catalog contains all **53 issues and 936 pull requests** captured on
2026-10-05. Its categories include **43 Runtime and API PRs**, **9 Testing and
analysis PRs**, and **803 dependency-maintenance PRs**. The integration owner confirms that the scope review covers all 53 issues,
the relevant runtime requests and all 12 nondependency closed-unmerged PRs.
The 43 runtime/API category count is the current refined classification; an
earlier count of 47 is not a separate implementation set. No record disappears
because its proposal was closed, superseded or deferred.

Existing custom-state, immutable-text, null-text, full-path matching,
INotifyDataErrorInfo, multi-rule, rule membership, formatter resolver and
observation-ownership behavior is retained. Inherited implementations require
regression preservation, rather than a second historical patch import. The
53 issue dispositions and 936 PR records remain discoverable through the
[issue catalog](issues.md), [PR catalog](pull-requests.md) and [JSON catalog](catalog.json).

## Explicit deferrals and superseded proposals

| Source | Decision / reason | Reconsider when |
| --- | --- | --- |
| #356 virtual `RaiseErrorsChanged` hook | Deferred: no concrete Runic SDK adapter requires overriding event delivery. Existing `ErrorsChanged` subscription covers the stated bridge use. | A concrete adapter needs the hook and defines base-call/ownership semantics. |
| #474 / #35 context abstraction | Useful interface already exists; do not import old namespace moves or obsolete tests. | A concrete API requirement is missing from current `IValidationContext`. |
| #41 helper `BindTo` patch | Superseded by current read-only OAPH/helper contracts; modern binding lifetime receives its own fix. | A new reproduced helper/activation issue requires an implementation change. |
| #66 / #67 legacy scheduler default | Do not restore platform-conditioned task-pool defaults; use explicit serialized model scheduling. | A supported consumer demonstrates a scheduler contract that needs a scoped adapter. |
| #833, #992 / .NET 11, #4/#9/#135/#414 and other native platform proposals | AndroidX, desktop/mobile samples, obsolete UWP/Xamarin and .NET 11 remain outside current .NET 10 core CI/release support. | Explicit platform support decision, workload/cohort and consumer/API/release gates. |
| #990 binding source-generator caveat | Anonymous/private selector issue is unreproduced against the resolved 9.1 generator generation; no speculative workaround. | Exact selectors reproduce the generator diagnostic/output issue. |
| Native AOT | Dynamic-code/trimming annotations remain. Typed state callbacks do not prove blanket AOT compatibility. | A concrete generated rule/binding publish-and-run scenario establishes support requirements. |
| Remaining abandoned repository/platform proposals and routine updates | Preserve dated disposition; no historical badge, coverage uploader, version bump, analyzer cleanup, platform downgrade or dependency-update queue is replayed. | Current supported scenario or intentional tested dependency cohort requires it. |

## Dependency decision

The implementation retains SDK **10.0.401**, **.NET 10**, ReactiveUI and
ReactiveUI.Reactive **26.0.1**, and the released Runic.DynamicData pair
**10.0.0-runic.5**. Release source is
`edd2d175794afc86e964a06cb55329f44793009f`; asset hashes remain in the
[historical cohort record](reviews/2026-10.md#dependency-cohort-and-existing-verification)
and [bootstrap](../../eng/restore-fork-dependencies.py). Ordinary new validation
features do not require a DynamicData cohort change or sibling checkout.

Unreleased sibling DynamicData improvements are future-cohort candidates.
DynamicData list `Switch` changesets are distinct from ReactiveUI `SwitchTo`
used in validation binding lifetimes. The finite/erroring list
`QueryWhenChanged` change `bd22ab8` is not a dependency of these ordinary
features; reconsider it through a released cohort if terminal parity becomes a
supported promise. Do not substitute unreleased sibling source for packages.
Publication-time read-only watch found the latest released pair still at `.5`;
the sibling checkout at `dab721ed0f54025d1cd6e65b77847f2220a5281c` on
`integration/upstream-2026-10` remains an unreleased future cohort. This release
consumes none of those later sibling changes; no SDK/DynamicData source was edited
as part of Validation implementation.

## Verification and release status

Candidate/logical adopting SHAs and API parity review are recorded above. The
local gate at `d6d634e43d109b848a06b960a29704aede2b100f` passed the Release
warnings-as-errors build, **594 tests (297 per flavor; zero failed/skipped)**,
eight Python negative graph checks and both ordinary collection examples.
Expanded consumers initially hit CA1050 in generated global helper types.
Verifier correction `bd58ee3e052670a4e320388d61d7b368735a7c04` compiled the
helpers inside a named namespace; its owner and integration runner verified both expanded consumers and full
exact-version/flavor graphs with the actual local `8.1.0-runic.0.804` package
pair at clean `c9fa501`. Eight negative graph tests reran successfully. Retained
local evidence and both package hashes appear in the
[implementation review](reviews/2026-10-implementation.md#verification).
[Linux/Windows CI run 37363825486](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37363825486)
passed at exact `c9fa501` in attempt 2. Windows attempt 1 and the Linux retry
passed all gates: **594 tests per OS (297 per flavor), zero failed/skipped**;
eight negative checks each, clean build, examples, packing and consumers. Windows produced
verification version
`8.1.0-runic.0.790.17`, as did Linux. It differs from local `.804` because CI had the published
version tag metadata. The review records each identity/hash separately; no byte
equality is claimed. Attempt 1 failed overall when Ubuntu never acquired a
hosted runner and executed no work. Only Linux was rerun at the same source;
attempt 2 passed, carrying the unchanged Windows success. This recovered
infrastructure acquisition failure did not indicate a shipping-code test
failure. Runic main is integrated; release identity and assets are verified.

The reviewed SDK source input remains `e878a4f361a7c4b9326f54663defa7debe41adb9`
for earlier focused probe evidence. During the local gate its checkout moved to
clean `e6de9ca3c6073aae33dc5fadbe6ebd5ec3de58df`; the unpinned Primitives
probe pass is excluded from final evidence and Reactive was intentionally not
run. There is no final pinned-SDK or bridge/browser E2E result.

The historical **402 passing tests** at the earlier unchanged baseline are not
verification of this implementation. Benchmarks, retained platforms, source
generators and Native AOT have their separately stated limits.

Runic main fast-forwarded to `c9fa501`, preserving upstream/topic histories.
[Release workflow 37365832750](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37365832750)
passed at that exact source and published
[`8.1.0-runic.0.790.17`](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17) under tag `runic-v8.1.0-runic.0.790.17` on **2026-10-05 at 19:50:16 UTC**.
The lightweight tag targets exact `c9fa501`; both published asset SHA-256 values
match downloaded bytes. Both nuspecs declare the correct version, .NET 10,
ReactiveUI 26.0.1, matching released DynamicData `.5` and repository commit `c9fa501`.
The [implementation review](reviews/2026-10-implementation.md#release) records
release hashes separately from local/CI verification packages. The release
workflow also passed 594 tests, eight guard checks, clean build, both examples
and both expanded consumers. The prior `.790` tag still targets `509dd45`.
The subsequent documentation-only update reuses unchanged code/CI evidence;
its own commit is not the released shipping source. Published tags and assets
remain immutable. No upstream message or contribution is part of this work.
