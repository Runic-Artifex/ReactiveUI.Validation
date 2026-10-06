# ReactiveUI.Validation upstream assessment for Runic

Upstream issues and pull requests were assessed on **2026-10-05** against upstream
[`cde3062`](https://github.com/reactiveui/ReactiveUI.Validation/commit/cde3062937752abeb4216a92c15f5b3b37be9a34).
The recommended work below has since been adopted; the
[fork difference register](../fork-differences.md#upstream-items-adopted-and-deferred)
maps each item to its contract and lists the deferrals. Later upstream activity is
summarized each month by the [upstream review workflow](../maintenance.md#monthly-upstream-review)
in an `Upstream review YYYY-MM` issue; its raw inventory is a workflow artifact and
is not committed.

The inventory contained **53 issues and 936 pull requests**; five issues and no
pull requests were open.

| Document | Contents |
| --- | --- |
| [Issue catalog](issues.md) | All 53 issues, with links, summaries and a Runic disposition. |
| [Pull request catalog](pull-requests.md) | All 936 PRs, grouped by topic, with links, state, summaries and a disposition. |

## Recommended work

Priorities here are our recommendations, not upstream labels. P1 means useful next work; P2 means a follow-up after correctness and a consumer scenario; P3 means defer. Effort describes scope, not a delivery estimate.

| Priority | Work | Why useful to Runic | Implementation scope |
| --- | --- | --- | --- |
| P1 | [Binding lifetime](#binding-lifetime) | Replaced models can still change a view; double disposal throws. Both failures reproduced. | Small internal fixes plus targeted tests in both flavors. |
| P1 | [Multiple contexts](#multiple-contexts), issue #511 | Blocking command checks and advisory messages need separate ownership and display. | Medium public API work; browser projection is a separate SDK integration. |
| P1 | [Collection recipes](#collection-recipes), issues #173 and #450 | Our DynamicData fork is a first-class dependency; empty and changing collections need reliable initial validation. | Small example and regressions, then a Runic bridge example. |
| P2 | [Typed state bindings](#typed-state-bindings), issue #463 | Native views need validity-to-color/visibility conversion; browser views need typed state. | Small to medium API work after binding lifetime fixes. |
| P2 | [Benchmarks](#benchmarks), issue #117 | Aggregate scans and message allocation matter for large forms and collections. | Separate benchmark project; optimize only against measured results. |
| P2 | [Notification order](#notification-order), issues #515, #92 and #95 | Validation gates commands and drives bridge snapshots on the model scheduler. | Focused integration tests and a documented ordering contract. |
| P3 | [Virtual error hook](#virtual-error-hook), PR #356 | Potential customization for a real notification adapter. | Small API change, contingent on a concrete use case. |
| P3 | MAUI/AndroidX samples and .NET 11 | Useful only with a corresponding Runic support decision. | Platform workload, CI and release expansion; currently deferred. |

For Runic, the existing bridge exports `INotifyDataErrorInfo` errors, while arbitrary ReactiveUI validation observables are not automatically exported. Native `BindValidation` does not bind DOM controls. Domain validation can live with the model; touched/submitted state and visual presentation should be owned by each presentation. These distinctions affect which upstream ideas belong in this library and which belong in SDK examples or adapters. See the [SDK ReactiveUI contract](https://github.com/Runic-Artifex/runic-sdk/blob/main/docs/guides/application/reference/reactiveui.md) and [bridge validation implementation](https://github.com/Runic-Artifex/runic-sdk/blob/main/packages/dotnet/Runic.Application.Views/BridgeValidation.cs).

## Binding lifetime

**Related upstream history:** [issue #659](https://github.com/reactiveui/ReactiveUI.Validation/issues/659), [PR #660](https://github.com/reactiveui/ReactiveUI.Validation/pull/660), [issue #665](https://github.com/reactiveui/ReactiveUI.Validation/issues/665), [PR #879](https://github.com/reactiveui/ReactiveUI.Validation/pull/879), and [PR #152](https://github.com/reactiveui/ReactiveUI.Validation/pull/152). The two findings below are additional cases discovered in this review, not claims that those upstream fixes were never applied.

**Confirmed replacement failure.** Bind a view to model A, replace its ViewModel with valid model B, then make A invalid. The view receives A's error again. `ReplacingViewModelDetachesOldValidation` fails in both flavors: expected an empty message, received `First error`.

The property and whole-model binding paths observe `ViewModel`, filter out null, and use `SelectMany` to subscribe to each model's validation stream. That combines old and new streams instead of replacing the old subscription. Helper-based binding already uses switching and offers a useful local pattern. See [`ValidationBinding.cs`](../../src/ReactiveUI.Validation/ValidationBindings/ValidationBinding.cs), particularly `ForProperty`, `ForViewModel` and `ObserveHelperState`.

**Proposed fix:** map each ViewModel to its current validation stream and use `SwitchTo`. Handle a null ViewModel inside the mapping so it detaches the old stream. Decide explicitly whether null clears the rendered value or merely stops updates; the current null filter silently keeps the prior stream alive. Apply the policy consistently to property, whole-model and action overloads. Test A→B→null→A, changes to detached models, late assignment, binding disposal and errors/completion. Instrument observer counts or use weak-reference tests before claiming a memory-retention fix; the probe demonstrates stale updates, not a measured leak.

**Confirmed disposal failure.** `BindingDisposalIsIdempotent` throws `NullReferenceException` on its second call in both flavors. `ValidationBinding.Dispose(bool)` disposes `_disposable` and sets it to null, then dereferences it again on a later call.

**Proposed fix:** make the private subscription nullable and take it once before disposing it, for example `Interlocked.Exchange(ref _disposable, null)?.Dispose()`. This also protects reentrant disposal of the subscription field, without making every operation on the binding thread-safe. Test two calls, reentrant callbacks, disposal after ViewModel replacement and disposal through multiple owning scopes. This is internal work and should not require changing the public API baseline.


## Multiple contexts

**Upstream request:** [issue #511](https://github.com/reactiveui/ReactiveUI.Validation/issues/511) distinguishes validations that block an external process from validations that only display advice. Its author published a [prototype branch](https://github.com/brignolff/Mdified.ReactiveUI.Vaidation/tree/multiple_validation_contexts), inspected here at `952c05fe7ffeac36e9adb78c9631460d69ba3e84`. It adds explicit concrete-context arguments for rules and context-selector arguments for bindings. It is a prototype on an older API generation, not an open upstream PR ready to merge.

**Runic value, inferred:** command admission can depend on a blocking context while each presentation displays advisory messages separately. This avoids making warnings accidentally disable Save or start another process. It also fits multiple presentations over one model, provided shared domain rules remain distinct from presentation-specific touched state.

**Proposed implementation:** add explicit `IValidationContext` arguments to selected `ValidationRule` overloads and context selection to binding entry points. Keep current overloads and forward them to `viewModel.ValidationContext`; preserve existing source and binary signatures. Start with property predicates, observable booleans and `IValidationState` streams, then expand only with concrete usage. Refactor registration into one internal helper taking the target context and component. Capture the chosen context at registration so disposing a helper removes its rule from that exact context rather than re-evaluating a selector.

Keep `ReactiveValidationObject.ValidationContext` as the default blocking context. Give advisory contexts explicit ownership and disposal. Avoid automatically adding all advisory contexts to the default aggregate, which would erase the blocking/advisory distinction. `IValidationContext` is itself a component, so deliberate child-context aggregation is possible, but property error-path projection and ownership still need tests.

For the browser, export an explicit supported DTO or primitive properties for advice. Default `INotifyDataErrorInfo` describes the default context; named contexts do not become bridge channels merely by adding properties to the ViewModel. An SDK adapter/example should define how contexts map to message groups and command gates. Additional severity fields on a custom state also need an observable/equality policy; separation by context is the smaller initial feature.

**Completion checks:** invalid advice leaves Save enabled; invalid blocking rules disable it; clearing one context leaves the other unchanged; helper disposal removes only its own rule; context replacement detaches old subscriptions; both flavor APIs and baselines match their DynamicData namespace; all owned contexts are disposed. This is feasible public API work, but the old prototype should be ported conceptually rather than cherry-picked wholesale.

## Typed state bindings

**Upstream request:** [issue #463](https://github.com/reactiveui/ReactiveUI.Validation/issues/463) wants validity mapped to a non-string property such as a control color. Existing generic `TViewProperty` methods still produce strings through `IValidationTextFormatter<string>` and `BindToView`. There are lower-level generic action/formatter overloads, but their formatter receives message text, not the whole validity state.

**Proposed implementation:** add distinct state-converter overloads rather than pretending a text formatter is a validity converter. A helper binding can take `Func<IValidationState, TTarget>`; a property with multiple rules can take `Func<IList<IValidationState>, TTarget>`, consistent with the current observation result, or an explicitly defined aggregate state. Define empty-rule behavior and aggregation so a valid state is never inferred from an empty message. Reuse the corrected switching pipeline. Generalize the private string-only assignment path to a typed observable, keeping the existing expression/dynamic-code annotations and a clear overload-resolution story.

For a Runic browser UI, the simpler first step is to export `isValid`, messages and presentation state, then map them to CSS/ARIA/frontend components. That avoids exporting platform color objects or adding DOM binding to this library. An explicit callback-based projection is also useful where reflection-based property assignment is unsuitable.

**Completion checks:** bool and enum targets; custom color conversion in a native sample if needed; multiple rules with mixed validity; an empty context; helper/model replacement and null; disposal; compatibility with existing formatter overloads; generated API baselines for both flavors. Preserve Native AOT limits rather than claiming typed converters make reflection-based binding AOT-safe.

## Benchmarks

**Upstream request:** [issue #117](https://github.com/reactiveui/ReactiveUI.Validation/issues/117) proposes BenchmarkDotNet in its own project/solution. This remains open. [PR #878](https://github.com/reactiveui/ReactiveUI.Validation/pull/878) already removes comparer string allocation and improves text/collection paths; those changes are inherited, so they are a baseline, not new optimization work.

**Proposed implementation:** add an opt-in .NET 10 benchmark solution outside normal test/release builds. Measure both flavors with identical workloads: one rule; 10/100/1,000 active rules; multiple rules on one property; validity flips; message changes while remaining invalid; dynamic add/remove/clear; empty and changing child collections; and repeated binding replacement/disposal. Measure construction, per-change time and bytes allocated separately. Include context observation, INotifyDataErrorInfo and explicit projected state as distinct consumers.

Current aggregate validity queries inspect the active rule set, and text construction traverses it again. Performance hypotheses therefore include repeated scans and per-change immutable text construction. Measure before replacing immutable values, adding pooled public results or rewriting aggregate counters. Any incremental aggregate must preserve updates when messages change but overall validity does not, rule removal, reentrancy and disposal.

A microbenchmark does not measure input-to-paint latency. A later SDK scenario should include model-scheduler turns, bridge serialization/delivery and actual frontend rendering. Keep that workload separate, bound rule counts and concurrency, and own rules by model/entity lifetime rather than transient DOM row lifetime.

## Collection recipes

**Sources:** [issue #173](https://github.com/reactiveui/ReactiveUI.Validation/issues/173) asks about validating child collections; [issue #450](https://github.com/reactiveui/ReactiveUI.Validation/issues/450) asks about initially empty collections, current validity and deferring visible errors until submission. The closing comment on #450 says its initial-value behavior was verified in the upstream 8.0 generation. Current rule overloads also warn that observable inputs must emit an initial value. Do not infer an initial collection bug in our fork solely from the old question.

**Proposed implementation:** write a fully imported example for each flavor using the matching `SourceList`/`SourceCache`, an initial snapshot or count emission, `AutoRefresh` for child properties, and a validation observable registered through the existing rule API. Use stable identity keys; a child name that changes is unsuitable as a cache key. Seed an empty source explicitly where the selected operator suppresses empty change sets. Validate initial empty, add, edit, remove, clear and replacement behavior without mutating the collection merely to trigger validation.

Prefer `ValidationContext.GetIsValid()` for a deliberate synchronous check and `Valid`/`IsValid()` for ongoing observations, with explicit scheduler assumptions. Keep delayed presentation separate: show errors when touched or submitted while domain validity can already be false. Add a Runic example that exports property/entity errors through the existing bridge and verifies that command execution sees the current blocking state.

Our earlier fork work already covers dynamic rule membership and observation disposal. The review's entity-rule probe also passes in both flavors: `HasErrors` is true and `GetErrors(null)` contains the entity-level message. That supports the existing bridge route and avoids inventing a missing entity-error API.

## Notification order

**Sources:** [issue #515](https://github.com/reactiveui/ReactiveUI.Validation/issues/515), [PR #879](https://github.com/reactiveui/ReactiveUI.Validation/pull/879), [issue #92](https://github.com/reactiveui/ReactiveUI.Validation/issues/92), [issue #95](https://github.com/reactiveui/ReactiveUI.Validation/issues/95) and [PR #97](https://github.com/reactiveui/ReactiveUI.Validation/pull/97).

PR #879 adds tests reading `HasErrors` inside `WhenAnyValue`; the original #515 report described `WhenAnyPropertyChanged`. The named legacy all-property API did not resolve in the current test environment. A diagnostic using the ordinary `INotifyPropertyChanged.PropertyChanged` event passes in both flavors. Neither the inherited regression nor that passing probe proves every subscription order, scheduler or asynchronous rule is safe. This is a coverage boundary, not a reproduced stale-HasErrors bug in the current fork.

**Proposed integration work:** specify serialized model updates → validation updates → command admission and bridge snapshot publication. Pass the appropriate Runic model-backed `ISequencer`/`IScheduler` explicitly to contexts instead of changing ReactiveUI's process-global scheduler. Test an invalidating property update followed by a command in the same model turn, deferred scheduling, cross-field changes, rule membership changes and asynchronous completion. Decide whether command admission must call a synchronous current-value check or wait for asynchronous validation; do not assume a scheduled display property has already caught up.

Keep `HasErrors` updated before `ErrorsChanged`, and preserve property-path notifications for nested rules. Do not globally reorder ReactiveUI notifications to satisfy an arbitrary side-effect subscription. Document which observable or event is the supported validation boundary.

## Compatibility

**Sources:** [#500](https://github.com/reactiveui/ReactiveUI.Validation/issues/500), [#679](https://github.com/reactiveui/ReactiveUI.Validation/issues/679), [#696](https://github.com/reactiveui/ReactiveUI.Validation/issues/696), [#829](https://github.com/reactiveui/ReactiveUI.Validation/issues/829), [#874](https://github.com/reactiveui/ReactiveUI.Validation/issues/874), [#967](https://github.com/reactiveui/ReactiveUI.Validation/issues/967), [#979](https://github.com/reactiveui/ReactiveUI.Validation/issues/979), [PR #691](https://github.com/reactiveui/ReactiveUI.Validation/pull/691), [PR #933](https://github.com/reactiveui/ReactiveUI.Validation/pull/933) and [PR #990](https://github.com/reactiveui/ReactiveUI.Validation/pull/990).

These reports repeatedly show runtime method mismatches after package-generation changes. The useful response is already part of our fork: build each flavor against its matching DynamicData package and exercise independent packaged consumers, rather than replacing NuGet IDs on an old binary. Keep graph checks excluding upstream DynamicData, maintain the Primitives consumer without System.Reactive, and retain the custom-resolver fallback and its tests. The upstream migration and formatter fixes are inherited; no historical compatibility patch needs another import.

PR #990 also records a source-generator caveat involving anonymous/private selector result types in ReactiveUI.Binding.SourceGenerators. That belongs to the binding generator, not to a validation PR to finish here. It was **not independently reproduced against the currently resolved 9.1 generation** in this review. Before adding a workaround, reproduce the exact selector and inspect generator diagnostics/output. Public named result types and simple selectors are a reasonable interim example convention, not proof of a general fix.

Native AOT is a separate validation target: rule and binding APIs currently carry dynamic-code or trimming annotations. A .NET 10 target and an AOT-friendly bridge serializer do not remove those limits. If Runic needs those APIs in a native AOT binary, add a focused publish/run smoke using the actual generated rule path, then address generator gaps with evidence. Do not advertise blanket AOT compatibility from this review. The fork has since added explicit observable APIs and a generated API for this; see [NativeAOT and generators](../aot-and-generators.md).

## Virtual error hook

**Candidate:** closed unmerged [PR #356](https://github.com/reactiveui/ReactiveUI.Validation/pull/356). Its patch simply makes `RaiseErrorsChanged` virtual. Maintainers requested a concrete overriding use case before closing it. The current fork still has protected, nonvirtual overloads.

**Decision:** feasible but optional. If a real adapter needs to customize delivery, make the string overload virtual and keep the parameterless overload forwarding to it. Document whether overrides must call base so normal INotifyDataErrorInfo subscribers still receive notifications. Alternatively add a narrower hook for scheduling or presentation projection without overriding event delivery. Test derived dispatch, base invocation, null/empty names, nested paths and disposal; update both public API baselines. For the normal Runic bridge, subscribing to existing `ErrorsChanged` is sufficient, so there is no present justification to ship this solely because the patch is small.

## Context abstraction

**Candidate:** closed unmerged [PR #474](https://github.com/reactiveui/ReactiveUI.Validation/pull/474), related to [issue #35](https://github.com/reactiveui/ReactiveUI.Validation/issues/35). Its patch introduces an interface, adjusts exposed rule collections, and relocates some text/context namespaces.

**Decision:** the useful capability is already present as `Contexts.IValidationContext`, including observable rules and add/remove/current-validity operations. Current `IValidatableViewModel` exposes that interface. Do not import the old namespace moves and test edits. Extend the current abstraction only where multiple contexts or a concrete integration requires it. The relevant code is [IValidationContext](../../src/ReactiveUI.Validation/Contexts/IValidationContext.cs) and [IValidatableViewModel](../../src/ReactiveUI.Validation/Abstractions/IValidatableViewModel.cs).

## Build overhaul

**Candidate:** closed unfinished [PR #833](https://github.com/reactiveui/ReactiveUI.Validation/pull/833). It proposes broad, host-aware platform target selection and workload setup. Later upstream build work supersedes much of the surrounding configuration.

**Decision:** do not finish that whole matrix for the current fork. We already have a .NET 10 core solution, pinned SDK, separate platform solution, Linux/Windows CI and branded package release workflow. If AndroidX becomes necessary, scope a new job to one supported workload/SDK generation, add consumer and platform tests, reconcile its API baselines, then include the branded assets deliberately. [Issue #414](https://github.com/reactiveui/ReactiveUI.Validation/issues/414) can supply the shared-model sample goal. .NET 11 in [PR #992](https://github.com/reactiveui/ReactiveUI.Validation/pull/992) likewise needs a support decision rather than a historical cherry-pick.

## Legacy helper binding

**Candidate:** closed unmerged [PR #41](https://github.com/reactiveui/ReactiveUI.Validation/pull/41). It replaces ValidationHelper `ToProperty` calls with `BindTo` while trying to fix old view-update behavior.

**Decision:** do not replay it. Current ValidationHelper uses read-only OAPH-backed properties and has a different disposal/activation implementation. Binding directly into those properties would conflict with that contract. A new failing case should be isolated in activation, scheduling or view binding and fixed there. Keep the current helper/property tests; the replacement-stream bug identified above has a specific modern implementation path.

## Legacy scheduler default

**Candidates:** closed [PR #66](https://github.com/reactiveui/ReactiveUI.Validation/pull/66) and merged [PR #67](https://github.com/reactiveui/ReactiveUI.Validation/pull/67) proposed task-pool scheduling for historical WPF/WinForms initialization failures. [PR #82](https://github.com/reactiveui/ReactiveUI.Validation/pull/82) later addressed related path and scheduling behavior.

**Decision:** do not restore a process-global or platform-conditioned task-pool default. The current constructor defaults to current-thread scheduling, accepts an explicit flavor-appropriate scheduler, and Runic requires serialized model turns. Specify and test that boundary. Native UI assignment may separately require a dispatcher adapter; model mutation and UI rendering should not be silently moved to arbitrary worker threads.

## Remaining unmerged proposals

There are 212 closed, unmerged PRs: 200 dependency updates and 12 other proposals. Five of the latter have detailed assessments above: #41, #66, #356, #474 and #833. The remaining seven do not offer missing core validation behavior:

| Proposal | Runic usefulness and completion path |
| --- | --- |
| [#4: Xamarin.Forms sample](https://github.com/reactiveui/ReactiveUI.Validation/pull/4) | An unfinished sample for a retired platform. Reuse its demonstration goal in a fresh Runic collection or command-validation example; completing the old Xamarin project is outside current support. |
| [#7: badges](https://github.com/reactiveui/ReactiveUI.Validation/pull/7) | Repository presentation, subsequently covered by merged badge/documentation changes. Any badge cleanup should point to this fork's workflows and packages. |
| [#11: warning cleanup](https://github.com/reactiveui/ReactiveUI.Validation/pull/11) | The current fork already enforces warnings as errors. Address a current analyzer failure directly rather than porting obsolete warning edits. |
| [#98: release version](https://github.com/reactiveui/ReactiveUI.Validation/pull/98) | A historical release bump for fixes now inherited. Current versioning and the Runic release workflow supersede it. |
| [#155: coverage action](https://github.com/reactiveui/ReactiveUI.Validation/pull/155) | A former coverage-uploader proposal. If coverage publishing is needed, integrate the current TUnit coverage format with a supported uploader and the fork's credential/permission policy. |
| [#400: Android Material downgrade](https://github.com/reactiveui/ReactiveUI.Validation/pull/400) | A version-specific platform repair. Reproduce against the chosen Android workload and supported dependency cohort before considering a downgrade. |
| [#628: Renovate onboarding](https://github.com/reactiveui/ReactiveUI.Validation/pull/628) | Upstream already has Renovate configuration. For this fork, configure intentional ReactiveUI/DynamicData dependency cohorts and Runic release sources before enabling automated proposals. |

The 200 unmerged dependency PRs are historical alternatives, not a queue of changes to finish. Their linked summaries preserve the requested versions. Current compatibility, release-source availability and consumer checks determine whether a new update is useful.

## Implementation order

1. Convert the two failing diagnostic expectations into permanent regressions and fix switching and idempotent disposal. Add null/replacement/action-overload cases in both flavors.
2. Add an initial-empty/changing-collection example and a command-admission regression on the Runic model scheduler. Keep property/entity bridge errors and presentation-specific touched state explicit.
3. Design the minimal explicit-context API for blocking and advisory validation; update both public API baselines and consumer examples. Avoid importing the old prototype wholesale.
4. Add typed state-converter convenience only for a demonstrated native consumer or explicit browser state projection.
5. Establish the separate benchmark project, then measure aggregate scans and allocations before changing algorithms.

Dependencies, closed status and upstream merge labels are evidence about history, not an implementation mandate. The catalogs preserve every record so deferred platform work and historical proposals remain discoverable without obscuring the useful next steps.
