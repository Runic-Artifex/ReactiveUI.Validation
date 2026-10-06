# NativeAOT and ReactiveUI generators

The **UNRELEASED generated/Unsafe API split** now has source-landed typed access,
observation, target/storage and finite registered compatibility routes; see the
[contract](generated-validation-design.md), [migration recipe](examples/generated-validation.md)
and [43-row ledger](generated-capabilities-progress.md). Normal calls without
interception or a matching supplied/registered plan fail actionably. Explicit
Unsafe owns reflected operations; there is no hidden reflection fallback.
Safe observable APIs remain available. The .NET 10 AOT flag is source-landed;
complete surface/dependency audit and strict actual flagged-package managed,
full-trim and NativeAOT gates remain pending. No expanded row is complete.

The initial restricted split's evidence below is historical and unchanged. It
does not establish final acceptance of the new typed/producer/catalog surface
or new UI host support.

At tested source `2550376b230ccfb78beb8d3b4ced8866b65d809e`, the local strict
Release gate passes **683 tests** (323 per library flavor and 37 compiler-fixture
tests), 20 Python guards, two collection examples and both independent package
consumers, with zero warnings/errors. Local actual-package generated consumers
pass managed, full trimming and actual Linux x64 NativeAOT: **42 behavioral case
executions and 12 negative builds**, all 18 normal overloads in every emitted
stage. The existing runtime corpus separately passes **30 behavioral case
executions and two historical baseline checks** in local all-mode execution.

All four jobs in [CI 37386897109](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37386897109)
pass at the same source: 683 tests per OS, generated managed/full-trim/native
execution on both Linux x64 and Windows x64 (42 cases and 12 negative builds per
RID), and the runtime safe subset in native mode (10 cases and two historical
checks per RID). Positive paths have zero warnings/errors. Both native hosts
consume the same verified Ubuntu package pair, version
`8.1.0-runic.0.790.17.15.10`; its hashes are distinct from the local pair.
The [review](upstream/reviews/2026-10-implementation.md#unreleased-generated-api-follow-up)
and [structured evidence](upstream/evidence/generated-api-implementation.json)
retain exact package/source/host identities and emitted-source evidence.

The implementation remains **UNRELEASED** until publication. This later
documentation record is not the tested/package source. Earlier release gates
below retain their historical identities. At `2550376` no `IsAotCompatible`
flag or whole-library reflection-free contract was declared; the expanded
source flag and its pending final acceptance are described above.

## Released runtime evidence (before the generator split)

The released [**8.1.0-runic.0.790.17.15**](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17.15) is exact source
`f22d2bb42c30d66df19a59333ed4fa633b241940`, based on `e653e52`. Both flavors
passed all five safe cases, including application-adapter handoff and failure
cleanup checks, in managed, fully trimmed and actual Linux x64 NativeAOT
execution: **30 scenario executions**, with zero positive-path warnings/errors.
[Four-job CI 37375282317](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37375282317)
passes at this source: **618 core tests per OS** and **20 actual native case
checks** across Linux x64/Windows x64, both flavors. Candidate publish paths have
zero warnings/errors. The [release workflow 37375833531](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37375833531)
repeats the complete matrix and publishes its own exact Ubuntu artifact verified
by that run's two native RID jobs. The tag, nuspec source and both downloaded asset hashes
are independently verified.
The preliminary `eae4984` candidate also passed its four-job CI, including
Windows native; those earlier package bytes are retained as historical evidence.
The existing release `8.1.0-runic.0.790.17` identifies immutable source
`c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`; it does not contain the additive
observable APIs. The [implementation review](upstream/reviews/2026-10-implementation.md#native-runtime-and-examples-follow-up)
keeps preliminary, local, CI and published evidence separate.

## Runtime and examples first implementation

[`AddObservableRule`](../src/ReactiveUI.Validation/Extensions/ObservableValidationRuleExtensions.cs)
registers complete state streams or projects caller-created values into complete
states. Explicit full property paths supply error and matching metadata without
reflecting over properties. The returned helper captures its destination context
and removes only its own rule when disposed.

[`BindObservableValidationState` and `BindObservablePropertyValidationState`](../src/ReactiveUI.Validation/Extensions/ObservableValidationBindingExtensions.cs)
follow caller-created model, helper or context selection streams. Typed delegates
select validation state and assign the result; the library does not discover a
view-model getter or target setter on this route. The [runtime recipe](examples/native-validation.md)
defines initial-state, null, matching, replacement and disposal behavior for both
flavors. Consumers own property observation, asynchronous cancellation and UI
dispatch.

The producer audit enables `EnableAotAnalyzer` and `EnableTrimAnalyzer` for both
shipped core projects. It removes annotations only where generated OAPH calls,
metadata-only expressions or explicit streams avoid the claimed operation.
Reflection-based legacy observation and target assignment retain
`RequiresUnreferencedCode`. No blanket `IsAotCompatible` declaration is made.
The final CI core gate passes **618 tests (309 per flavor) per OS**, zero
failed/skipped. Guard coverage comprises **14 Python checks: six native and
eight existing package checks**.
A successful safe-path publish and run establishes that tested path and host;
it does not certify all overloads or all reachable dependency APIs.

The [realistic package corpus](../examples/NativeValidation/README.md) preserves
released-package expected failures and explicit safe counterparts. Its nullable
address case requires missing data to be invalid; the baseline managed run
retains the last valid nested value. The migration explicitly supplies null
notifications and combines two single-field rules into one multi-property rule.
These are application-policy and metadata adaptations, not blanket legacy fixes.
The final fixture installs pending subscription slots before initial callbacks;
it checks synchronous parent replacement/null, disposal, and initial getter or
callback failure cleanup. These are application-observation contracts, separate
from the already audited library APIs and the new generated-code acceptance.

The [strict package gate](../eng/verify-native-validation.py) must consume actual
packed assets, retain
warning-as-error diagnostics, check exact package graphs and run the emitted
trimmed and native executables. The older opt-in investigative runner below
lowers IL2026/IL3050 severity to capture warnings and behavior. It is not the
strict gate and cannot satisfy the new zero-warning requirement.

After bootstrapping the released DynamicData feed, run from the repository root
in the locked SDK/native environment:

```sh
direnv exec "$RUNIC_SDK" python3 eng/verify-native-validation.py --rid linux-x64 --mode all
```

The default gate freshly packs current source into an isolated feed.
`--package-feed FEED` instead verifies a prepacked pair against clean HEAD and
its nuspec source; CI native jobs consume the exact Linux shipping package
artifact. `--output OUTPUT` selects retained evidence (default
`artifacts/verification/native-gates`). Managed, trimmed and native phases can
be selected independently with `--mode`. `win-x64` is another accepted RID;
its safe-path native execution is verified by the final Windows job. Standalone
fully trimmed managed execution is established locally on Linux x64; the Windows
native job publishes with trimming enabled. The
release workflow requires the reused complete
core/native matrix and publishes the same verified package artifact.

## Dated released cohort investigation

Investigation dated **2026-10-05** for the released Runic ReactiveUI.Validation
.NET 10 cohort. All ten base scenarios passed managed, fully trimmed and native
execution in both flavors: **60 scenario executions**. Each base native publish
retained **49 warning occurrences per flavor**. The separate generated consumer
passed native execution with **ten warning occurrences per flavor**. These
results cover the exact tested Linux x64 consumers, including expression rules
and reflected scalar view setters; they do not establish warning-free or
whole-package NativeAOT support.

Keep the existing dependency cohort. Binding runtime and interceptors are
already part of that graph; view-model SourceGenerators are optional consumer
tooling. Neither generator rewrites `ValidationRule` or `BindValidationState`
inside the compiled Validation package. The historical results below describe
the immutable `.790.17` assets, not the released native implementation.

## Investigated release cohort and support status

| Input | Investigated pin |
| --- | --- |
| Validation release | `Runic.ReactiveUI.Validation` and `Runic.ReactiveUI.Validation.Reactive` `8.1.0-runic.0.790.17` |
| Shipping Validation source | `c9fa501c4d2e9442d85693dae77bb6f727e9eb7a` |
| Repository investigation base | `2ba57c537172d841cdc545ef9cd255f1e41a4af8` |
| SDK and target | .NET SDK `10.0.401`, `net10.0` |
| ReactiveUI pair | `ReactiveUI` / `ReactiveUI.Reactive` `26.0.1` |
| DynamicData pair | `Runic.DynamicData` / `Runic.DynamicData.Reactive` `10.0.0-runic.5` |
| Binding runtime and interceptors | `9.1.0`, source [`05c45cec845835d670960fac733bb302bc1c1071`](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/tree/05c45cec845835d670960fac733bb302bc1c1071) |
| View-model SourceGenerators | `4.2.0`, source [`c8a8c38dd7192073540e187b8e8abc8d53e16b1a`](https://github.com/reactiveui/ReactiveUI.SourceGenerators/tree/c8a8c38dd7192073540e187b8e8abc8d53e16b1a) |

The [release review](upstream/reviews/2026-10-implementation.md#release) records
the immutable published assets and source. The [central pins](../src/Directory.Packages.props)
and [bootstrap](../eng/restore-fork-dependencies.py) keep the released DynamicData
pair coupled. A sibling checkout or later unreleased DynamicData changes are not
inputs to this investigation. The Primitives graph must remain free of
System.Reactive, and both graphs must remain free of upstream `DynamicData`
packages. These are package and namespace contracts, independently of NativeAOT.

An API annotation, a compiler warning, and a native runtime result answer
different questions. `RequiresDynamicCode` warns that runtime-generated code
may be needed; `RequiresUnreferencedCode` warns that members may be removed by
trimming. A successful publish or one passing execution does not remove those
contracts for other inputs. NativeAOT requires trimming, and expression trees
use interpretation. See the [NativeAOT overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/),
[IL3050 guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/il3050)
and [trim-warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings).

## The three ReactiveUI integration points

`ReactiveUI.Binding` is runtime binding infrastructure already used by the
ReactiveUI dependency graph and Validation source. Its Reactive counterpart is
`ReactiveUI.Binding.Reactive`. It is not a proposed new optional Validation
feature: the two library projects already import their corresponding expression
namespaces and call observation helpers. See the
[Primitives project](../src/ReactiveUI.Validation/ReactiveUI.Validation.csproj)
and [Reactive project](../src/ReactiveUI.Validation.Reactive/ReactiveUI.Validation.Reactive.csproj).

`ReactiveUI.Binding.SourceGenerators` is the binding generator component,
bundled as analyzer assets in `ReactiveUI.Binding` / `.Reactive` for this cohort.
Its interceptors
can replace recognized expression observation and binding calls in the consuming
compilation with generated code. They cannot rewrite calls inside the released
Validation DLL. In particular, an application-level generated `WhenAnyValue`
does not turn the package's internal `WhenAnyValueUnsafe` into generated code.
ReactiveUI `26.0.1` already brings the Binding `9.1.0` analyzer assets into the
consumer graph. A direct reference can make the application pin explicit; it is
not by itself a new integration or a fix for Validation.

`ReactiveUI.SourceGenerators` generates view-model boilerplate such as reactive
properties, commands and notification members. In the current 4.x split,
`[ObservableAsProperty]` and view generation belong to Binding. OAPH generation
and view-model property generation are distinct operations. Neither implements
Validation rule selection, rule registration, validation binding lifetimes or
validation-property assignment. Generated properties can supply values to a
native path, but the consumer still chooses how to observe those values and
connect them to Validation. In this exact resolved consumer graph its analyzer
also reaches the compiler transitively: rebuilding without a direct
SourceGenerators reference still produced the generated properties. A direct
`PrivateAssets="all"` reference can make the application's pin and intent
explicit; it is optional here. Inspect the actual compiler analyzer inputs when
changing the graph rather than inferring availability from one nuspec entry.

The [official generator documentation](https://www.reactiveui.net/documentation/binding/source-generators/)
describes how Binding recognizes members written by SourceGenerators. Generators
run separately: arbitrary code produced by one generator is not intercepted by
the other. The new Validation generator must emit direct runtime operations,
for example the appropriate `ObservedProperty` API, rather than emit fresh
`WhenAnyValue` calls and assume another generator will rewrite them.

## Released API and warning boundaries

The following source boundaries apply to both released flavors. Primitives uses
`ReactiveUI.Validation.*` and `DynamicData`; Reactive uses
`ReactiveUI.Validation.Reactive.*`, `DynamicData.Reactive` and System.Reactive
scheduler types. A callback target changes the assignment mechanism but does not
replace expression observation at the source.

| API family | Caller contract | Reachable operation | Native support boundary |
| --- | --- | --- | --- |
| `ValidationState`, `ValidationText`, `ObservableValidation<TViewModel,TValue>` with a supplied observable | No RDC/RUC on these entry points | Delegate evaluation, immutable text and explicit state stream | Useful native foundation; does not exercise property observation or view binding. |
| `ValidationContext`, `ValidationHelper`, `ReactiveValidationObject` constructors | RUC | Constant-name `ToProperty` for OAPH presentation; context uses delegate-based DynamicData refresh | Released context/helper IL calls generated `ToProperty` interceptors in both flavors; caller annotations remain. |
| Predicate-based property `ValidationRule` | RDC + RUC | Internal `WhenAnyValueUnsafe` on the supplied expression | Reflection observation is a separate boundary from generated application observation. |
| Supplied-observable `ValidationRule`, including explicit context | RDC + RUC | Observable component plus helper/registration; property variants also read expression metadata | Public warnings remain even where a particular overload avoids property observation. |
| `BindValidation`, selected-context bindings, `BindValidationState` with an action | RDC + RUC | Internal observation of the view model, helper/context and rule state; direct callback | Avoids reflected target assignment, but retains reflected source selection. |
| The same bindings with a target expression | RDC + RUC | Source selection plus reflected target setter; nested targets also observe their parent chain | Needs both source and target reachability; typed projection does not remove reflection. |
| `ObserveFor`, `ContainsProperty`, property `ClearValidationRules` | No RDC/RUC on these entry points | Reads expression member names to match rule metadata | An expression used as metadata is distinct from reflected property access. |

RDC means `RequiresDynamicCode`; RUC means `RequiresUnreferencedCode`.
These annotations move diagnostics to callers and limit analysis of the
annotated body. Expected caller warnings can therefore conceal dependency risks;
they do not mean the body or all transitive libraries have been proven clean.
The [property component](../src/ReactiveUI.Validation/Components/BasePropertyValidation%7BTViewModel,TViewModelProperty%7D.cs),
[rule extensions](../src/ReactiveUI.Validation/Extensions/ValidatableViewModelExtensions.cs),
[typed binding extensions](../src/ReactiveUI.Validation/Extensions/ValidationStateBindingExtensions.cs)
and [target assignment](../src/ReactiveUI.Validation/ValidationBindings/ValidationStateBinding.cs)
show these distinct paths. The source audit found no direct `Expression.Compile`,
`DynamicMethod`, `MakeGenericMethod` or `MakeGenericType` in Validation itself.
That finding does not establish the behavior of every transitive dependency.

Read-only IL inspection of the DLLs extracted from both released `.790.17`
packages confirms that `ValidationContext` and `ValidationHelper` route their
OAPH `ToProperty` calls through `__ReactiveUIGeneratedBindings` interceptors.
They do not call the runtime `ToPropertyUnsafe` fallback. This is package-byte
evidence, independent of generated output from a new local library build.
The current library build already relies on that generator dispatch; removing
the analyzer assets without replacing those calls would leave unsuffixed runtime
stubs. Adding a new production package reference is unnecessary for this cohort.

Binding's [Unsafe runtime guidance](https://reactiveui.net/documentation/binding/unsafe/)
identifies reflected runtime observation. The pinned
[reflection implementation](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.Shared/Expression/Reflection.cs)
uses reflected member access and assignment. Preserve annotations until the
specific path, generated code and dependencies have a supported native contract;
their current generic messages should not be read as proof that every expression
requires runtime code generation.

## Dependency boundaries

The released DynamicData pair comes from
[`edd2d175794afc86e964a06cb55329f44793009f`](https://github.com/Runic-Artifex/DynamicData/tree/edd2d175794afc86e964a06cb55329f44793009f).
Its library build enables AOT/trim analysis on supported targets. That metadata
does not substitute for a Validation consumer executing the actual list and
refresh pipeline. Validation's context uses `SourceList.Connect`,
`AsObservableList`, `AutoRefreshOnObservable` with a delegate and
`QueryWhenChanged`; it does not select DynamicData's property-expression refresh
or grouping APIs. The existing release cohort is therefore the relevant first
test, with no dependency upgrade assumed.

ReactiveUI `26.0.1` package metadata pins source
[`88312688d0281fd260977dfd78bb12e1c85ffaaa`](https://github.com/reactiveui/ReactiveUI/tree/88312688d0281fd260977dfd78bb12e1c85ffaaa).
Binding's runtime provider initialization, ReactiveUI property notifications and
System.Reactive's matching flavor still participate in native reachability.
Warnings from an unrelated rooted dependency API should be attributed to that
API; they should not be described as a failing Validation rule. Conversely,
trimming away an unused problematic API does not certify the whole dependency.

Validation's default formatter lookup asks the resolver for the closed generic
`IValidationTextFormatter<string>` service and falls back to
`SingleLineFormatter.Default`. Supplying an explicit formatter avoids that
lookup. An application's custom dependency injection registrations or assembly
scanning have their own native requirements; the Validation formatter resolver
does not make those registrations safe.

## Dated generated consumer results

The [Binding generator investigation](../investigations/BindingGenerators/README.md)
uses the same released package pair and inspects emitted files as well as
executing behavior. Its final rebuilds reference only the corresponding
Validation and Runic.DynamicData packages. Compiler inputs contain Binding
`9.1.0` and SourceGenerators `4.2.0` analyzers, with C# `14.0` and interceptor
namespace `ReactiveUI.Binding.Generated.Interceptors` supplied by the package's
build assets. No separate application flavor switch was required:
SourceGenerators inferred the matching ReactiveUI integration from the
compilation.

| Shape | Observed result | Boundary |
| --- | --- | --- |
| Generated reactive `Name` property and inline `WhenAnyValue` | Managed pass in both flavors; generated direct getters and interception locations | Application observation is generated. |
| Supplied-observable property rule and generated `BindTo` bool target | Managed pass in both flavors; generated direct setter | Rule registration stays in Validation; generated assignment bypasses its reflected target-binding path. |
| `BindValidationState` callback with replaced/null model | Managed pass in both flavors | No Validation interceptor is emitted; the package retains internal Unsafe source observation. |
| Private nested partial types, nested-chain replacement, closed and open generics | Managed pass in both flavors; hosted partial generated code inspected | Covers these tested notifying types and caller shapes. |
| Selector stored in an expression variable | Primitives negative build: RXUIBIND001 and RXUIBIND021 | An unsuffixed call has no generated binding; use an inline supported selector or deliberately choose the Unsafe fallback. |
| Plain anonymous object with no notification mechanism | Primitives negative build: RXUIBIND002 and RXUIBIND015 | Inaccessible-type support does not imply all anonymous/non-notifying sources are valid. |

The generated consumer also passed actual Linux x64 NativeAOT execution in both
flavors with `--require-aot`, reporting dynamic code unsupported. This combines
the generated property/observation/direct-setter path with the supplied-observable
Validation rule and the package's existing typed callback lifetime. Each publish
retained ten warning occurrences: compiler three IL2026 plus two IL3050, repeated
as three trim IL2026 plus two native IL3050. All originate at annotated
Validation calls in the consumer. Generated application operations therefore
worked; the existing Validation warning boundaries remained.

The [retained generated output](../investigations/BindingGenerators/evidence/Primitives/WhenAnyValueDispatch.g.cs.txt)
and [target dispatch](../investigations/BindingGenerators/evidence/Primitives/BindToDispatch.g.cs.txt)
show what the compiler receives; the corresponding Reactive outputs and compiler
inputs are retained alongside them. The
[released package IL inspection](../investigations/BindingGenerators/evidence/released-oaph-il.txt)
establishes the existing context/helper OAPH interception described above.
The [generated native summary](../investigations/BindingGenerators/evidence/native-summary.json)
records source identity, per-flavor outcomes and warning counts.

Binding's [9.1.0 release](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/releases/tag/v9.1.0)
includes support for calls with types that generated code cannot name. These
positive and negative cases narrow the earlier
[#990 caveat](upstream/README.md#compatibility): they do not
establish that every historical anonymous/private selector is fixed. Reproduce a
specific failing selector and inspect its diagnostic/output before adding a
Validation workaround.

## Dated native consumer results

Both standalone package consumers target `net10.0` and restore the exact released
Validation/ReactiveUI/DynamicData cohort. They reference no source library
projects. Native executables verify
`RuntimeFeature.IsDynamicCodeSupported == false` and run the same behavioral
checks in each flavor. The native toolchain was Linux x64 with SDK `10.0.401`,
Clang `21.1.8` and zlib `1.3.2`, reused from the locked Runic SDK development
environment.
The [native probe source and reproduction instructions](../investigations/NativeAot/README.md)
record asset hashes, isolated feed inputs and serialized managed/trimmed/native
commands. All ten scenarios also passed managed and fully trimmed
self-contained execution in both flavors.
The [retained native results](../investigations/NativeAot/evidence/results.json)
record the tested program hash, actual runtime rows and diagnostic occurrences.
The final base-probe native publishes ran one flavor at a time with
`IlcSingleThreaded=true`, verified as `--parallelism:1` in the compiler response
files. The generated consumer's earlier native proof used default ILC concurrency;
its recorded outcome is not relabeled as a single-threaded run.

| Scenario | Primitives native | Reactive native | What the checks establish |
| --- | --- | --- | --- |
| Standard context with a custom validation component | Pass | Pass | Empty validity, add, message-only update, valid transition, removal and detachment. |
| Supplied-state `ObservableValidation` component | Pass | Pass | Initial invalid state, later validity and context removal without property observation. |
| Supplied-observable `ValidationRule` and helper | Pass | Pass | Current helper/context validity and helper-disposal removal. |
| Expression property rule and `ReactiveValidationObject` | Pass | Pass | Property updates, `HasErrors`, property `GetErrors`, clearing and restoring errors. |
| Legacy helper binding with a string target | Pass | Pass | Initial state, model/helper replacement and null, old-source detachment and idempotent disposal. |
| Typed helper binding with a bool target | Pass | Pass | The same lifecycle using full-state projection to a reflected bool setter. |
| Selected-context callback binding | Pass | Pass | Context replacement/null, stale-source detachment and disposal. |
| Explicit state subscription and static setter | Pass | Pass | Initial/current state and disposal without expression observation or reflected target assignment. |
| Legacy target setter used only through reflection | Pass | Pass | Dedicated string setter has no ordinary setter calls or member-preservation attributes. |
| Typed target setter used only through reflection | Pass | Pass | Dedicated bool setter has the same restriction. |

The earlier target scenarios share a `View` type whose bool setter is also used
by the direct-setter control. The final two use a separate target type and avoid
ordinary setter calls, explicit rooting descriptors, preservation attributes
and complete-type-metadata generation. Their getter
expressions still refer to the concrete property. These results cover those
known scalar properties; they do not cover arbitrary runtime-selected members,
nested target hosts, custom structs, platform UI controls or every overload.

Publish diagnostics are retained, not suppressed. The opt-in probe configuration
changes IL2026/IL3050 from the repository's error severity to warning severity so
the emitted warnings and native behavior can both be inspected. RDC/RUC body
boundaries still limit transitive analysis. Neither the positive native runs nor
an absence of separately attributed DynamicData/Binding warnings proves those
entire packages warning-free.

| Publish phase | Primitives compiler | Primitives analysis | Reactive compiler | Reactive analysis |
| --- | --- | --- | --- | --- |
| Managed | 0 | Not applicable | 0 | Not applicable |
| Fully trimmed | 17 IL2026 | 17 IL2026 | 17 IL2026 | 17 IL2026 |
| NativeAOT | 17 IL2026 + 7 IL3050 | 17 IL2026 + 8 IL3050 | 17 IL2026 + 7 IL3050 | 17 IL2026 + 8 IL3050 |

Each native publish log contains **49 warning occurrences**, including compiler
messages repeated by trim/native analysis. The 25 analysis occurrences comprise
17 consumer IL2026, seven consumer IL3050 and one IL3050 in Validation's
`BasePropertyValidation` constructor. These are occurrences, not 49 distinct
defects. No separately attributed Binding or DynamicData warning appears in
these consumers; annotation boundaries prevent treating that as a full library
audit.

## Migration and annotation boundaries

Use the [explicit observable runtime recipe](examples/native-validation.md) for
new native consumers. It retains standard context aggregation, captured rule
ownership, synchronous domain state and matching-flavor imports. For the
immutable runtime release, expression APIs retain reflective source selection even when they use typed callbacks. On the working branch, supported
normal calls instead require interception or an exact supplied/registered typed
provider/catalog plan; retained reflection is explicitly
selected with `ValidationRuleUnsafe`, `BindValidationUnsafe`,
`BindValidationContextUnsafe` or `BindValidationStateUnsafe`. See the
[migration recipe](examples/generated-validation.md) for precompiled-call and
package-asset requirements.

The generator/tooling cohort is aligned to the locked SDK's actual Roslyn 5.9.0
compiler and `analyzers/dotnet/roslyn5.9/cs` package path. A reproduced
nullable-generic CS8714 in C#14's synthesized static bridge requires the six safe
observable methods to use traditional `this` extensions. CLR signatures and
inferred calls remain compatible; explicit observable binding source calls now
use `<TSource, TOut>`, while `AddObservableRule<TValue>` keeps its arity. The
narrow SST1703 exception addresses that measured compiler error, without IL
suppression or tuple-based signatures. The same direct-static nullable caller
sources produced eight CS8714 errors
before the correction and zero warnings/errors afterward; inferred receiver
calls already passed before it. The retained proof is
`artifacts/verification/generator-nullability/results.json`, with before,
inferred and after logs/source inputs. Focused aligned compiler/runtime checks
pass at the final tested source, with clean-source local/CI gates recorded above. Historical proof/release source and package inputs are unchanged.

The producer audit distinguishes dynamic-code requirements from trimming
requirements. In the audited .NET 10 cohort, legacy reflection operations
require RUC, while the old blanket RDC messages overstated the need for runtime
code generation. Explicit supplied-observable rule overloads and metadata-only
selectors do not perform that reflected observation. Context, helper and
`ReactiveValidationObject` constructors use generated OAPH dispatch, as released
IL inspection above already established. Annotation removal is an intentional
public-contract change reviewed in both flavor API baselines.

`IsAotCompatible` enables analyzer/trimmability settings and advertises a library
compatibility contract. It may coexist with correctly annotated unsafe APIs;
it is not a promise that every legacy API is safe. The expanded implementation
enables this flag only on the two inner .NET 10 core targets. Fresh complete
flagged-package audit and managed/full-trim/native acceptance remain pending;
historical unset-flag evidence above is not the current source contract.
The [official property guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
describes the settings it enables. Native support remains limited to the safe
paths, dependency cohort and actual host executions recorded in the review.

## Implementation options

| Option | Role | Benefit | Cost and limit |
| --- | --- | --- | --- |
| Keep corresponding Binding runtime dependencies | Required by the current cohort | Preserves existing rule and view binding behavior | Existing reflective observation remains subject to its annotations and native runtime constraints. |
| Explicit observables, delegates and state subscriptions | Released safe console contract | Explicit rules and replacement streams expose ownership and direct callbacks | Both-flavor API review, producer analysis and strict actual-package consumer gates for the recorded host scope. |
| Binding interceptors in the application | Optional | Generates recognized application observation/binding calls | Compiler/interceptor configuration and generated-output verification; does not replace package-internal calls. |
| SourceGenerators in the application | Optional | Reduces reactive property, command and notification boilerplate | Select compatible flavor/output, inspect generated code; OAPH/view generation belongs to Binding in the current split. |
| Validation-specific source generator and typed plans | [Unreleased normal-call implementation](generated-validation-design.md) | Direct observations/metadata/assignment, plus explicit typed provider/catalog alternatives | Embedded assets, source/ABI/flavor regressions and current strict package gates required. Unintercepted and unregistered normal calls fail; explicit registered mode supplies no reflection fallback. |
| Library `IsAotCompatible` contract | Source-landed for inner .NET 10 core flavors; final acceptance pending | Enables producer analysis and communicates the compatibility contract | Complete current surface/dependency audit, precise Unsafe warnings and exact flagged-package managed/full-trim/native gates on claimed hosts are mandatory. |

## Native platform scope

NativeAOT binaries target a specific runtime identifier and require the native
toolchain and libraries for that platform. Ordinary .NET build/test success on
Linux and Windows does not stand in for a native publish/run. Keep native consumers standalone from the shipping solution, reuse the locked
development environment, and record native compiler/runtime inputs with each
result. Run strict package gates separately from the dated warning-bearing probes.
The released strict five-case safe console paths are verified on **`linux-x64` and
`win-x64`**, in both flavors at exact `f22d2bb`. Both final native jobs execute
the same Ubuntu package artifact, with zero candidate warnings/errors.
Standalone full-trim managed execution is established locally on Linux x64;
Windows evidence is actual native execution with trimming enabled. The dated
warning-bearing investigations above establish only Linux x64. Linux Arm64,
macOS and cross-compilation need matching toolchains and actual host execution.

The new generated split at `2550376` adds actual managed/full-trim/native
consumer execution on both Linux x64 and Windows x64, in both flavors. The
runtime gate at that source independently executes native with trimming on both
CI hosts; its standalone fully trimmed managed run remains local Linux evidence.
These are the exact console corpus/cohort/host contracts recorded above.

The retained AndroidX and desktop/mobile samples remain outside core releases.
This implementation does not add Android, iOS, macOS, Windows desktop UI,
NativeAOT cross-compilation or Runic bridge/browser support. A future platform
expansion must define its RID, UI/framework cohort and native consumer behavior
under the [maintenance policy](maintenance.md#validation-and-development-environment).
