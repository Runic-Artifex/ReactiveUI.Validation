# Runic fork difference register

The fork is based on upstream ReactiveUI.Validation
[`cde3062`](https://github.com/reactiveui/ReactiveUI.Validation/commit/cde3062937752abeb4216a92c15f5b3b37be9a34).
This register lists every intentional difference and the inherited behavior to
protect. Follow the [maintenance policy](maintenance.md) when integrating or
retiring entries.

Keep stable IDs. `active` means intentional fork policy or adaptation;
`retained` means inherited behavior to protect; `retired` means a verified
replacement or removal (keep retired entries). When behavior changes, update the
source links, regressions and retirement condition.

## Build, dependencies and packaging

| ID / status | Difference | Preserve | Retire when |
| --- | --- | --- | --- |
| RUV-001 / active | [SDK pin](../global.json) and [build properties](../src/Directory.Build.props): SDK 10.0.401, .NET 10-only core targets instead of upstream's broader scope. | Both libraries, tests and .NET 10 API baselines; Linux/Windows build with warnings as errors. | An explicit SDK/target policy change with build, API and consumer verification. |
| RUV-002 / active | [Central pins](../src/Directory.Packages.props), [bootstrap](../eng/restore-fork-dependencies.py) and [NuGet sources](../nuget.config): ReactiveUI 26.0.1 and the released Runic.DynamicData 10.0.0-runic.30 pair from a SHA-256-verified local feed. The native gate also restores the immutable `.5` pair, only for its released Validation baseline. | Update both flavor versions and bootstrap hashes together; no sibling project references, upstream DynamicData or inherited workspace versions. | A tested replacement cohort or a dependency-distribution policy change that keeps identity/integrity checks. |
| RUV-003 / active | [Primitives](../src/ReactiveUI.Validation/ReactiveUI.Validation.csproj) and [Reactive](../src/ReactiveUI.Validation.Reactive/ReactiveUI.Validation.Reactive.csproj) projects share source with matching DynamicData flavor imports. Reactive `Validations` exposes `DynamicData.Reactive.IObservableList`. | `REACTIVE_SHIM`, both namespace roots, flavor schedulers and both API baselines. | An explicit flavor architecture decision with consumer migration. |
| RUV-004 / active | [Core solution](../src/ReactiveUI.Validation.slnx) contains the libraries, generator and tests; the [platform solution](../src/ReactiveUI.Validation.Platforms.slnx) keeps AndroidX/samples separately. | Core CI needs no Android/desktop workloads; AndroidX IDs are branded but not released. | A deliberate platform support expansion with its own gates. |
| RUV-005 / active | Runic package IDs/metadata, Nerdbank.GitVersioning policy in [version.json](../version.json), [CI](../.github/workflows/ci-build.yml) and the [release workflow](../.github/workflows/release.yml). | Two branded core GitHub prerelease assets, `v<version>` tags (`9.0.0-runic.N`; older `runic-v8.1.0-runic.0.*` releases are historical), immutable published versions, original MIT license and authorship. | An explicit packaging/distribution policy change. |
| RUV-006 / active | [Property observation](../src/ReactiveUI.Validation/Extensions/ValidationContextExtensions.cs) uses the flavor-compatible `CombineLatest` instead of System.Reactive's. | Primitives consumers have no System.Reactive dependency; keep [DisposalAndOverloadTests](../src/tests/ReactiveUI.Validation.Tests/DisposalAndOverloadTests.cs). An initially empty observation still emits nothing. | A flavor-safe upstream replacement preserves these behaviors. |
| RUV-007 / active | The [package verifier](../eng/verify-packages.py) restores each packed flavor outside the source graph and checks the DynamicData list type, validity updates and helper removal. | Exactly one current package per flavor, exact versions, no upstream DynamicData, no System.Reactive in Primitives; keep the [negative graph checks](../eng/test_verify_packages.py). | Equivalent consumer verification elsewhere. |

## Runtime contracts

| ID / status | Difference | Preserve | Retire when |
| --- | --- | --- | --- |
| RUV-010 / active | [Binding lifetime](../src/ReactiveUI.Validation/ValidationBindings/ValidationBinding.cs): bindings follow the view's current model/helper, detach replaced or null sources and dispose idempotently. | [Lifetime regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationBindingLifetimeTests.cs) for property, whole-model, helper, action and formatter paths. Legacy property callbacks keep their empty prelude. | An upstream replacement passes the same contracts. |
| RUV-011 / active | [Explicit-context rules](../src/ReactiveUI.Validation/Extensions/ValidationRuleContextExtensions.cs) and [selected-context bindings](../src/ReactiveUI.Validation/Extensions/ValidationContextBindingExtensions.cs). Blocking and advisory contexts are independent; cleanup removes a rule from its captured registration context. | API baselines, [context regressions](../src/tests/ReactiveUI.Validation.Tests/MultipleValidationContextTests.cs) and the [example](examples/multiple-contexts.md). Named contexts do not become blocking rules or error-export channels. | A compatible API replacement preserves independence and ownership. |
| RUV-012 / active | [Typed state bindings](../src/ReactiveUI.Validation/Extensions/ValidationStateBindingExtensions.cs): `BindValidationState` projects complete states to typed targets/actions without deriving validity from text. | [Typed regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationStateBindingTests.cs): bool/enum/nullable projections, custom state metadata, initial state, empty/null, replacement and disposal. | An equivalent projection API with migration. |
| RUV-013 / active | [Opt-in benchmark solution](../benchmarks/ReactiveUI.Validation.Benchmarks.slnx) measuring both flavors outside shipping builds. | [Commands and workloads](../benchmarks/README.md), smoke/correctness checks, recorded measurement source. No speed ranking or UI-latency claim. | An equivalent opt-in performance workflow. |
| RUV-014 / active | [Context domain streams](../src/ReactiveUI.Validation/Contexts/ValidationContext.cs): membership, `Valid`, `ValidationStatusChange` and `Validations` deliver synchronously on the owner; `IsValid`/`Text` stay scheduled presentation. Fixes stale same-turn validity and command admission. | [Scheduling regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationContextSchedulingTests.cs), reentrant removal/disposal and the [ordering contract](examples/validation-ordering.md). Consumers dispatch UI presentation explicitly. | A reviewed scheduler redesign preserves current validity and ordering. |
| RUV-015 / active | [Collection recipe](../examples/CollectionValidation/CollectionValidationRecipe.cs) and [ordering probe](../examples/ValidationOrdering/Program.cs): stable keys, initial snapshot, refresh/removal/replacement/disposal and current command admission. | [Collection](../src/tests/ReactiveUI.Validation.Tests/CollectionValidationRecipeTests.cs) and [ordering](../src/tests/ReactiveUI.Validation.Tests/ValidationOrderingTests.cs) regressions; both executable collection examples run in CI. | Equivalent supported examples. |
| RUV-016 / active | [Observable rules](../src/ReactiveUI.Validation/Extensions/ObservableValidationRuleExtensions.cs) and [observable callbacks](../src/ReactiveUI.Validation/Extensions/ObservableValidationBindingExtensions.cs): explicit value/state streams with full property metadata, no discovered observation or assignment. | API baselines, the [runtime recipe](examples/native-validation.md) and [runtime regressions](../src/tests/ReactiveUI.Validation.Tests/ObservableRuntimeApiTests.cs): initial values, null, full paths, strictness, replacement, reentrant disposal and registration rollback. | An equivalent supported runtime surface. |
| RUV-017 / active | Core projects enable AOT/trim analysis; false `RequiresDynamicCode` and blanket `RequiresUnreferencedCode` are removed from generated-OAPH and supplied-stream paths, kept on reflection. | Accurate annotations in both API baselines; zero-warning safe-path publishes in the [native gate](../eng/verify-native-validation.py). | An equivalent producer/package workflow keeps accurate boundaries. |
| RUV-018 / active, unreleased | Generated/Unsafe split ([generator](../src/ReactiveUI.Validation.SourceGenerators/ReactiveUI.Validation.SourceGenerators.csproj), [design](generated-validation-design.md), [capabilities](generated-capabilities.md), [migration](examples/generated-validation.md)): normal rule/binding calls are intercepted or use typed/registered plans; `*Unsafe` methods own reflection. Typed `Func` overloads take priority for inline lambdas; the 29 original signatures are preserved. Both packages embed the analyzer and allowlist props; `IsAotCompatible` is set on .NET 10 targets. Normal property text bindings show actual initial state; Unsafe keeps the legacy prelude. Safe observable APIs use classic extensions because of a C# 14 `CS8714` bug. | Both API baselines and package graphs; generator diagnostics with typed alternatives; no reflection or expression-evaluation fallback; captured contexts, strict paths, custom states, replacement and disposal; the [generated gate](../eng/verify-generated-validation.py) in managed/trimmed/native modes on Linux and Windows x64. | A reviewed replacement preserves compile-time behavior, accurate Unsafe warnings, analyzer delivery and migration. |

## Maintenance operations

| ID / status | Difference | Preserve | Retire when |
| --- | --- | --- | --- |
| RUV-019 / active | Monthly [upstream review](../.github/workflows/upstream-review.yml) with [scripts](../eng/upstream): uploads the upstream inventory and a summary as a workflow artifact and opens or updates one `Upstream review YYYY-MM` tracking issue. | Offline fixtures; read-only contents permission; commits nothing, pushes no branches, executes no upstream code and sends nothing upstream. Real merges stay human decisions. | Equivalent review that keeps the inventory out of the repository. |
| RUV-020 / active | [Release guard](../eng/release/release_assets.py): promote the latest trusted exact-main Build's Linux package pair; `verify_only` creates no remote state. | [Offline fixtures](../eng/release/test_release_assets.py): identity, source, cohort, license and analyzer payload checks; draft download-byte verification before publishing. | An equivalent process preserving tested bytes and immutability. |
| RUV-021 / active | Gate-owned NuGet cache and XML feeds, source/input/restored byte proof, graph checks before execution and current-invocation reports in [verify-packages.py](../eng/verify-packages.py) and [CI](../.github/workflows/ci-build.yml) artifact verification. | Same-version cache, corrupt-byte and early-failure regressions; active `.30` vs. legacy `.5` separation. | Equivalent evidence that the selected bytes were exercised. |

## Inherited contracts to retain

| ID / status | Source and scope | Preserve |
| --- | --- | --- |
| RUV-008 / retained | [Upstream #990](https://github.com/reactiveui/ReactiveUI.Validation/pull/990): split flavors, custom-resolver formatter fallback, PublicApiSharp baselines, TUnit. | [Formatter resolver tests](../src/tests/ReactiveUI.Validation.Tests/ValidationTextFormatterResolverTests.cs), awaited TUnit assertions, explicit review of both API baselines. |
| RUV-009 / retained | [Upstream #660](https://github.com/reactiveui/ReactiveUI.Validation/pull/660) disposable observation ownership; [#879](https://github.com/reactiveui/ReactiveUI.Validation/pull/879) disposal order and `WhenAnyValue` error-state regressions. | [Context](../src/tests/ReactiveUI.Validation.Tests/ValidationContextTests.cs), [internal collection](../src/tests/ReactiveUI.Validation.Tests/InternalCollectionTests.cs), [helper](../src/tests/ReactiveUI.Validation.Tests/ValidationHelperTests.cs) and [error notification](../src/tests/ReactiveUI.Validation.Tests/NotifyDataErrorInfoTests.cs) coverage in both flavors. |

## Upstream items adopted and deferred

Adopted from the [upstream inventory](upstream/README.md): binding lifetime
(#23, #152, #659/#660, #665/#879 → RUV-010), multiple contexts (#511 → RUV-011),
typed state bindings (#463 → RUV-012), collection recipes (#173, #450, #433 →
RUV-015), command/notification scheduling (#19, #31, #34, #70, #92/#95/#97,
#515 → RUV-014), benchmarks (#117 → RUV-013), dependency/package boundaries
(#500, #679, #696, #829, #874, #967, #979, #691, #933, #990 → RUV-007), the
virtual `RaiseErrorsChanged(string)` hook (#356, now protected virtual; overrides
must call base), and the #378 catalog correction (bind a control property, not a
`FindControl` call).

| Source | Decision | Reconsider when |
| --- | --- | --- |
| #474 / #35 context abstraction | `IValidationContext` already covers it; do not import the old namespace moves. | A concrete API need is missing from `IValidationContext`. |
| #41 helper `BindTo` patch | Superseded by read-only OAPH helpers and RUV-010. | A reproduced helper/activation issue. |
| #66 / #67 task-pool scheduler default | Not restored; use explicit serialized model scheduling. | A supported consumer needs a scoped scheduler adapter. |
| #833, .NET 11, #4/#9/#135/#414 and other platform proposals | AndroidX, desktop/mobile samples, UWP/Xamarin and .NET 11 stay outside .NET 10 core CI and releases. | An explicit platform support decision with its own gates. |
| #990 Binding generator selector caveat | Not reproduced with Binding 9.1; no speculative workaround. | An exact selector reproduces the generator issue. |
| Additional NativeAOT hosts | Supported native hosts are Linux x64 and Windows x64. | A new host passes the strict package gates. |
| Abandoned repository proposals and dependency PRs | Not replayed (badges, coverage uploader, version bumps, analyzer cleanup, platform downgrades, update queues). | A current scenario or tested dependency cohort needs one. |
