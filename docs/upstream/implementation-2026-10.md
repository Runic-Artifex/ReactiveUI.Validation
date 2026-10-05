# Validation implementation ledger: 2026-10

Started **2026-10-05** from Runic base `48a7bde8e4fdbb34f85d700597c87d60c24aff69`.
This is the live decision and adoption record for the worthwhile work identified
by the [dated assessment](README.md). The [implementation review](reviews/2026-10-implementation.md)
records integrated inputs and verification. The [original October review](reviews/2026-10.md)
and [diagnostic evidence](evidence/README.md) remain historical records.

Status is **implementation in progress**. A topic is adopted only when the
integration owner supplies its exact adopting SHA and verification results.
Independent topic commits and historical test counts do not establish final
integration or release. The [difference register](../fork-differences.md) remains
the source of stable fork contracts.

## Worthwhile implementation topics

| Topic / sources | Decision and contract | Status / adoption | Required evidence |
| --- | --- | --- | --- |
| Binding lifetime: #23, #152, #659/#660, #665/#879, reproduced diagnostics | Switch to the current ViewModel/helper/context; detach replaced and null sources; make binding disposal idempotent. Preserve property, aggregate, helper and callback behavior in both flavors. | In progress; adoption pending. | Replacement/null/helper cases, stale-source detachment, double disposal and post-disposal updates; both flavor suites. |
| Multiple contexts: #511; existing #35/#474 abstraction | Add explicit-context rule registration and context-selected binding while retaining the default context APIs. The selected registration context owns helper removal. Advice stays independent of blocking validity. | In progress; adoption pending. | Blocking/advisory independence, clearing/disposal, context replacement/null, both API baselines and both flavors. |
| Typed state binding: #463 | Add distinct `BindValidationState` projections for typed targets and actions. Define empty and multi-rule state explicitly; preserve text formatter overloads. | In progress; adoption pending. | Bool/enum projection, mixed rules, empty state, replacement/null/disposal and both API baselines. |
| Collection recipes: #173, #450, #433 | Compile fully imported examples for both flavors with an explicit initial empty snapshot, stable identity, child refresh and source replacement. Separate domain validity from touched/submitted presentation. | In progress; adoption pending. | Empty/add/edit/remove/clear/replacement scenarios, current validity and property/entity errors. |
| Command and notification scheduling: #19, #31, #34, #70, #92/#95/#97, #515/#879 | Document and test serialized model mutation, validation and command admission using an explicit flavor scheduler. Preserve the distinction between synchronous current validity and deferred notifications. | In progress; adoption pending. | Same-turn invalidation, queued work, cross-field/membership changes, asynchronous completion and notification-order regressions. |
| Benchmarks: #117; inherited #878 baseline | Add an opt-in .NET 10 BenchmarkDotNet solution for both flavors outside shipping solution/release gates. Measure representative construction, change, collection and replacement/disposal workloads before optimizing. | In progress; adoption pending. | Both projects compile; bounded smoke execution and workload coverage. Measurements are scenario evidence, not input-to-paint or blanket performance claims. |
| Dependency and package boundaries: #500/#679/#696/#829/#874/#967/#979, #691/#933/#990 | Retain the released matching DynamicData pair and formatter fallback. Strengthen independent consumer checks for both upstream DynamicData IDs, opposite flavor, exact resolved versions and Primitives' System.Reactive exclusion. | In progress; adoption pending. | Package verification plus deliberate negative graphs; independent packed consumers on the final integrated SHA. |
| Catalog correction: #378 | The reported selector calls `view.FindControl<TextBlock>("UsernameError").Text`; move control lookup behind a property and bind `view.UsernameError.Text`. The reporter confirmed this fix. | Documented in this ledger's initial topic commit; integration pending. | [Suggested property-only selector](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-957330539) and [reporter confirmation](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-960270058); issue table and JSON agree. |

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

Exact integrated SHAs, focused/full results, API review, packing, independent
consumer graphs and Linux/Windows verification are pending the integration owner.
The historical **402 passing tests** at the earlier unchanged baseline are not
verification of this implementation. Benchmarks, retained platforms, source
generators and Native AOT have their separately stated limits.

No new release is recorded. Published tags and assets remain immutable. No
upstream message or contribution is part of this work.
