# Runic fork difference register

Baseline **2026-10-05**: Runic `origin/main` at [`a4cfecf`](https://github.com/Runic-Artifex/ReactiveUI.Validation/commit/a4cfecf7621d92bc22c9fb0cca6df489350a0ab4), containing port [`509dd45`](https://github.com/Runic-Artifex/ReactiveUI.Validation/commit/509dd45b7f8458a22ae4cb758b1b68e72d874217) and upstream [`cde3062`](https://github.com/reactiveui/ReactiveUI.Validation/commit/cde3062937752abeb4216a92c15f5b3b37be9a34). Follow the [maintenance policy](maintenance.md) when integrating or retiring entries.

Keep stable IDs and update the source/adoption commits, dependencies, regression evidence and retirement conditions when behavior changes. `active` means intentional fork policy/adaptation; `retained` means inherited behavior to protect; `retired` means verified replacement/removal. Keep retired entries as history. The dated research catalogs do not update this register automatically.

Integrated/released source **2026-10-05**: `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`
(shipping pin `5d433128e0692af112f43a7e09409a43b7210edc`).
The integration owner adopted the topic commits below into Runic main and
released [`8.1.0-runic.0.790.17`](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17) from this source. The local Release build, 594 shipping tests, eight negative-graph
checks and both ordinary collection examples passed at `d6d634e`, with unchanged
shipping source/workflows. Consumer correction `bd58ee3` resolved generated-helper
CA1050; both expanded `.804` consumers and full graphs passed with clean
`c9fa501`, and all eight negative checks reran. Cross-platform CI attempt 2
passed at the same source. The release workflow repeated its Linux gates and
verified the tag, nuspecs and both published assets. The
[implementation review](upstream/reviews/2026-10-implementation.md#release) records
exact published hashes, distinct from local and CI artifacts.

## Intentional adaptations

Unless noted otherwise, the adoption source for these entries is Runic commit `509dd45`.

| ID / status | Source, reason and scope | Dependencies / verification to preserve | Retirement condition |
| --- | --- | --- | --- |
| RUV-001 / active | [SDK pin](../global.json) and [build properties](../src/Directory.Build.props): SDK 10.0.401, .NET 10-only core targets. Removes upstream's broader/preview build scope. | Both libraries, shared tests and .NET 10 API baselines; Linux/Windows build with warnings as errors. | Explicit Runic SDK/target policy change with replacement build, API and consumer verification. |
| RUV-002 / active | [Central pins](../src/Directory.Packages.props), [bootstrap](../eng/restore-fork-dependencies.py) and [NuGet sources](../nuget.config): ReactiveUI 26.0.1 and released Runic.DynamicData 10.0.0-runic.5 pair, SHA-256 verified local feed. | Update both flavor versions and bootstrap version/hashes together; no sibling project references, upstream DynamicData substitution or enclosing workspace version inheritance. Verify restored consumers. | A tested replacement release cohort or explicit dependency-distribution policy change; preserve identity/integrity checks. |
| RUV-003 / active | [Primitives project](../src/ReactiveUI.Validation/ReactiveUI.Validation.csproj), [Reactive project](../src/ReactiveUI.Validation.Reactive/ReactiveUI.Validation.Reactive.csproj) and conditional shared source: matching DynamicData flavor imports. Reactive `Validations` exposes `DynamicData.Reactive.IObservableList`. | Preserve `REACTIVE_SHIM`, `ReactiveUI.Validation` / `ReactiveUI.Validation.Reactive` namespace roots, flavor schedulers and both public API baselines. Compile external consumers using the matching list type. | Explicit flavor architecture/support decision with consumer migration; an upstream namespace/package change alone is insufficient. |
| RUV-004 / active | [Core solution](../src/ReactiveUI.Validation.slnx) contains two libraries and two test projects; [platform solution](../src/ReactiveUI.Validation.Platforms.slnx) retains AndroidX/native samples separately. | Core CI needs no Android/desktop workloads. Retained AndroidX IDs are branded but not shipped as core release assets. | Deliberate platform support expansion with workload, API, consumer and release verification. |
| RUV-005 / active | Runic package IDs/metadata, MinVer policy in [build properties](../src/Directory.Build.props), [CI](../.github/workflows/ci-build.yml) and [release workflow](../.github/workflows/release.yml). | Two branded core GitHub prerelease assets; isolated `runic-v` version tags; immutable published versions/assets; original MIT license/authorship. Record source/cohort and check both OS jobs. | Explicit packaging/distribution policy change. Published assets and original licensing remain protected. |
| RUV-006 / active | [Property observation](../src/ReactiveUI.Validation/Extensions/ValidationContextExtensions.cs) replaces the explicit System.Reactive `CombineLatest` call with the flavor-compatible extension. | Primitives packaged consumer has no System.Reactive dependency. Preserve dynamic rule addition/removal, state changes and observation disposal regressions in [DisposalAndOverloadTests](../src/tests/ReactiveUI.Validation.Tests/DisposalAndOverloadTests.cs), run in both flavors. Initially empty observation currently emits nothing; do not claim this adaptation fixes initial empty collections. | A verified upstream/flavor replacement preserves these behaviors and the dependency boundary; retain regressions. |
| RUV-007 / active | [Independent package verifier](../eng/verify-packages.py) restores each packed flavor outside the source project graph, checks matching DynamicData list type, validity/error updates and helper removal. Guard hardening adopted at `df7b48de7a2191171c7243484a785a36850e749c`; expanded generated-consumer namespace correction at `bd58ee3e052670a4e320388d61d7b368735a7c04`. | Exactly one current package per flavor; exact resolved versions and matching flavor only; exclude both upstream DynamicData IDs and System.Reactive in the Primitives graph. Preserve [negative graph checks](../eng/test_verify_packages.py). Actual `.804` pair passed both expanded consumers/full graphs at `c9fa501`; both OS CI gates passed in attempt 2. | Equivalent consumer verification in a replacement distribution/test system. |

## Adopted implementation contracts

| ID / status | Source, adopting commit and contract | Dependencies / verification to preserve | Retirement condition |
| --- | --- | --- | --- |
| RUV-010 / active | [Binding lifetime](../src/ReactiveUI.Validation/ValidationBindings/ValidationBinding.cs), adopted at `925e344e0ed1d8b26ff8f128167a2fa476b35067`: follow the view's current model/helper; detach replacement/null sources; dispose subscriptions idempotently. | Preserve [lifetime regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationBindingLifetimeTests.cs) for property/whole-model/helper/action bindings and formatter paths in both flavors. Legacy property callbacks keep their empty prelude. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Equivalent upstream/flavor replacement passes replacement/null/helper/disposal contracts; retain regressions. |
| RUV-011 / active | [Explicit-context rules](../src/ReactiveUI.Validation/Extensions/ValidationRuleContextExtensions.cs) and [selected-context bindings](../src/ReactiveUI.Validation/Extensions/ValidationContextBindingExtensions.cs), API adoption `e116e8bf808ebbf37a7b4a5177729bf3d89b8193`, captured default ownership `64542c88e03951f0158de0e36874549a0be1b80f`, combined at `8dc90961e3b6e5dfd326004749e6b81447c5d093`. Blocking and advisory contexts stay independent; cleanup removes only the rule from its captured registration context. | Preserve both public API baselines, [context regressions](../src/tests/ReactiveUI.Validation.Tests/MultipleValidationContextTests.cs), actual property-state seeding, replacement/null handling and [ownership example](examples/multiple-contexts.md). Named contexts do not automatically become blocking rules or error-export channels. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | A deliberate compatible API replacement preserves independent contexts, captured ownership and consumer migration; retain regressions. |
| RUV-012 / active | [Typed state bindings](../src/ReactiveUI.Validation/Extensions/ValidationStateBindingExtensions.cs), adopted at `acf8d7178b639d68ea3b5eaf474fdf5d521d4321`: `BindValidationState` projects complete helper/property rule states to typed targets/actions without deriving validity from text. | Preserve both public API baselines, [typed regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationStateBindingTests.cs), bool/enum/nullable projections, custom class/struct metadata, actual initial state, empty/null behavior and replacement/disposal. Dynamic-code/trimming limits remain. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Equivalent supported projection API and documented migration preserve state/metadata/lifetime semantics; retain regressions. |
| RUV-013 / active | [Opt-in benchmark solution](../benchmarks/ReactiveUI.Validation.Benchmarks.slnx), adopted at `a23693e7990c9ba2539237db8ca5533ca8d54102`; binding workloads added at `587c1b0bf11c1b02b6adb0df34d85e7b8eeecd48`; CLI/source-pin correction `8fd9847716916d9436ebfab908ec74b9bb922a89`. Identical shared workloads measure both flavors outside shipping builds. | Preserve [commands and workload contracts](../benchmarks/README.md), bounded smoke/correctness checks, explicit measurement source/cohort and [baseline limitations](../benchmarks/results/2026-10-05.md). [Final source-pinned report](../benchmarks/results/2026-10-05-final.md) records clean benchmark build, 18 CLI checks and six selected ShortRun measurements. No full matrix, algorithm improvement or UI-latency result is inferred. | Equivalent opt-in performance workflow preserves both flavor comparisons, correctness checks and reproducible source/cohort evidence. |

## Model scheduling and executable recipes

| ID / status | Source, adopting commit and contract | Dependencies / verification to preserve | Retirement condition |
| --- | --- | --- | --- |
| RUV-014 / active | [Context domain streams](../src/ReactiveUI.Validation/Contexts/ValidationContext.cs), adopted at `0a94cea64958de8221059dbcef03d791d05544fc`: raw membership, `Valid`, `ValidationStatusChange` and public `Validations` delivery are synchronous on the owner; `IsValid`/`Text` OAPH properties remain scheduled presentation. Fixes stale same-turn rule-add/remove validity and command admission. | Preserve [manual queue regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationContextSchedulingTests.cs), reentrant removal/disposal and [ordering contract](examples/validation-ordering.md). Consumers dispatch native/UI presentation explicitly; no concurrent mutation or global scheduler change is promised. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | A reviewed scheduler/domain redesign preserves current validity, status payload consistency, ownership and documented observable delivery; retain regressions and migration guidance. |
| RUV-015 / active | [Shared collection recipe](../examples/CollectionValidation/CollectionValidationRecipe.cs) and [ordering probe](../examples/ValidationOrdering/Program.cs), adopted at `004a17c84c264f50ece8f65400fe5373668bfa4f`: matching flavor imports, stable child keys, initial snapshot, refresh/removal/replacement/disposal and current command admission. | Preserve [collection regressions](../src/tests/ReactiveUI.Validation.Tests/CollectionValidationRecipeTests.cs), [ordering regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationOrderingTests.cs), both ordinary executable flavors and opt-in SDK adapter source probes pinned at `e878a4f361a7c4b9326f54663defa7debe41adb9`. CI adoption `667978c` runs ordinary examples, not SDK probes. No bridge/browser E2E claim. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Equivalent supported examples retain initial emission, stable identity, lifecycle, presentation/domain distinction and both flavor/scheduler evidence. |

## Inherited contracts to retain

| ID / status | Source and scope | Verification / retirement condition |
| --- | --- | --- |
| RUV-008 / retained | [Upstream #990](https://github.com/reactiveui/ReactiveUI.Validation/pull/990), `e68c8a6`: split validation flavors, custom-resolver formatter fallback and PublicApiSharp baselines; TUnit/Microsoft.Testing.Platform already exists upstream. | Preserve [formatter resolver tests](../src/tests/ReactiveUI.Validation.Tests/ValidationTextFormatterResolverTests.cs), awaited TUnit assertions and explicit review of both .NET 10 API baselines. Replacement needs equivalent consumer/DI/API evidence, not another test runner by incidental merge. |
| RUV-009 / retained | [Upstream #660](https://github.com/reactiveui/ReactiveUI.Validation/pull/660), `3834d5e`: disposable collection ownership for context observations; [#879](https://github.com/reactiveui/ReactiveUI.Validation/pull/879), `406c372`: disposal-order and `WhenAnyValue` error-state regressions. | Preserve [context](../src/tests/ReactiveUI.Validation.Tests/ValidationContextTests.cs), [internal collection](../src/tests/ReactiveUI.Validation.Tests/InternalCollectionTests.cs), [helper](../src/tests/ReactiveUI.Validation.Tests/ValidationHelperTests.cs) and [error notification](../src/tests/ReactiveUI.Validation.Tests/NotifyDataErrorInfoTests.cs) coverage in both flavors. Retire an adaptation only after equivalent ownership/order behavior is reproduced. These tests do not prove all ViewModel replacement or side-effect ordering cases. |

## Live implementation status

The [October implementation ledger](upstream/implementation-2026-10.md) and
[implementation review](upstream/reviews/2026-10-implementation.md) record the
released source and its logical adoption SHAs. Binding lifetime, explicit contexts,
typed state projections, collection/scheduler correction, opt-in benchmarks and
graph hardening are implemented, integrated and released at `c9fa501`, with
local, Linux/Windows CI and release gates passed.

The original [investigation](upstream/README.md#binding-lifetime) reproduced
old-model updates and binding double-disposal failures at `509dd45` in both
flavors. Its [diagnostics](upstream/evidence/README.md) remain historical evidence;
RUV-010 now points to permanent regressions for the adopted correction.

RUV-014 records the newly reproduced same-turn membership correction, including
its deliberate observable delivery change. RUV-015 records the compiled shared
collection examples and source-level SDK adapter probes. The
[ledger](upstream/implementation-2026-10.md#implemented-behavior-and-scheduling-correction)
records the shipping pin, API parity approval and completed verification. No broad
Native AOT support or ReactiveUI 25 compatibility follows from this release.
