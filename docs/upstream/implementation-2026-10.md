# Validation implementation ledger: 2026-10

Started **2026-10-05** from Runic base `48a7bde8e4fdbb34f85d700597c87d60c24aff69`.
This is the live decision and adoption record for the worthwhile work identified
by the [dated assessment](README.md). The [implementation review](reviews/2026-10-implementation.md)
records integrated inputs and verification. The [original October review](reviews/2026-10.md)
and [diagnostic evidence](evidence/README.md) remain historical records.

Status is **implemented candidate; final gates pending** at source candidate
`e9ab48a3e7d46f9a20eb0c0f5e1777887599dbe8`. The integration owner supplied the
logical adopting commits below. Collection/scheduling work remains in progress.
This candidate has not completed final integration verification or release.
Historical test counts do not verify the candidate. The [difference register](../fork-differences.md) remains
the source of stable fork contracts.

## Worthwhile implementation topics

| Topic / sources | Decision and contract | Status / adoption | Required evidence |
| --- | --- | --- | --- |
| Binding lifetime: #23, #152, #659/#660, #665/#879, reproduced diagnostics | Switch to the current ViewModel/helper/context; detach replaced and null sources; make binding disposal idempotent. Preserve property, aggregate, helper and callback behavior in both flavors. | Implemented candidate: `925e344e0ed1d8b26ff8f128167a2fa476b35067`; final gates pending. | Replacement/null/helper cases, stale-source detachment, double disposal and post-disposal updates; both flavor suites. |
| Multiple contexts: #511; existing #35/#474 abstraction | Add explicit-context rule registration and context-selected binding while retaining the default context APIs. The selected registration context owns helper removal. Advice stays independent of blocking validity. | Implemented candidate: API `e116e8bf808ebbf37a7b4a5177729bf3d89b8193`, default ownership `64542c88e03951f0158de0e36874549a0be1b80f`; combined at `8dc90961e3b6e5dfd326004749e6b81447c5d093`. Final gates pending. | Blocking/advisory independence, clearing/disposal, context replacement/null, both API baselines and both flavors. |
| Typed state binding: #463 | Add distinct `BindValidationState` projections for typed targets and actions. Define empty and multi-rule state explicitly; preserve text formatter overloads. | Implemented candidate: `acf8d7178b639d68ea3b5eaf474fdf5d521d4321`; final gates pending. | Bool/enum projection, mixed rules, empty state, replacement/null/disposal and both API baselines. |
| Collection recipes: #173, #450, #433 | Compile fully imported examples for both flavors with an explicit initial empty snapshot, stable identity, child refresh and source replacement. Separate domain validity from touched/submitted presentation. | In progress; adoption pending. | Empty/add/edit/remove/clear/replacement scenarios, current validity and property/entity errors. |
| Command and notification scheduling: #19, #31, #34, #70, #92/#95/#97, #515/#879 | Document and test serialized model mutation, validation and command admission using an explicit flavor scheduler. Preserve the distinction between synchronous current validity and deferred notifications. | In progress; adoption pending. | Same-turn invalidation, queued work, cross-field/membership changes, asynchronous completion and notification-order regressions. |
| Benchmarks: #117; inherited #878 baseline | Add an opt-in .NET 10 BenchmarkDotNet solution for both flavors outside shipping solution/release gates. Measure representative construction, change, collection and replacement/disposal workloads before optimizing. | Implemented candidate: initial `a23693e7990c9ba2539237db8ca5533ca8d54102`, binding workloads `587c1b0bf11c1b02b6adb0df34d85e7b8eeecd48`; candidate smoke/final gates pending. | Both projects compile; bounded smoke execution and workload coverage. Measurements are scenario evidence, not input-to-paint or blanket performance claims. |
| Dependency and package boundaries: #500/#679/#696/#829/#874/#967/#979, #691/#933/#990 | Retain the released matching DynamicData pair and formatter fallback. Strengthen independent consumer checks for both upstream DynamicData IDs, opposite flavor, exact resolved versions and Primitives' System.Reactive exclusion. | Implemented candidate: `df7b48de7a2191171c7243484a785a36850e749c`; final packed-consumer gates pending. | Package verification plus deliberate negative graphs; independent packed consumers on the final integrated SHA. |
| Catalog correction: #378 | The reported selector calls `view.FindControl<TextBlock>("UsernameError").Text`; move control lookup behind a property and bind `view.UsernameError.Text`. The reporter confirmed this fix. | Adopted documentation: `ac6e6ed`; included in candidate `e9ab48a`. | [Suggested property-only selector](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-957330539) and [reporter confirmation](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-960270058); issue table and JSON agree. |

## Implemented behavior and remaining scheduling fix

The candidate includes permanent [binding lifetime regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationBindingLifetimeTests.cs),
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

The collection agent reproduced a separate **current scheduling bug**: adding an
invalid rule during an explicit SDK model turn leaves `GetIsValid()`,
`HasErrors` and command admission stale until queued membership publication.
A correction is in progress to update raw context membership/status synchronously
while preserving scheduled presentation OAPH updates. It is **not included** in
candidate `e9ab48a`; its final source SHA and regression results remain pending.
This adds evidence beyond the original passing property-notification probe.

The [benchmark report](../../benchmarks/results/2026-10-05.md) distinguishes the
initial warmed baseline at `48a7bde` from the later binding workload smoke at
`73bedbb`. Those measurements are not relabeled as final-candidate timings.

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

## Verification and release status

Candidate/logical adopting SHAs are recorded above. Exact final integrated SHA,
focused/full results, API review, packing, independent consumer graphs and
Linux/Windows verification remain pending the integration owner.
The historical **402 passing tests** at the earlier unchanged baseline are not
verification of this implementation. Benchmarks, retained platforms, source
generators and Native AOT have their separately stated limits.

No new release is recorded. Published tags and assets remain immutable. No
upstream message or contribution is part of this work.
