# NativeAOT and ReactiveUI generators

Investigation dated **2026-10-05** for the released Runic ReactiveUI.Validation
.NET 10 cohort. The released packages passed the ten investigated native
scenarios in each flavor on Linux x64, including expression rules and reflected
view setters. Publish warnings remain visible, and this result establishes those
consumer paths rather than whole-package NativeAOT support.

Keep the current dependency cohort. Binding runtime and interceptors are already
part of that graph; view-model SourceGenerators are optional consumer tooling.
Neither generator rewrites `ValidationRule` or `BindValidationState` inside the
compiled Validation package. For a maintainable native contract, first document
and verify the observable/delegate subset, then add explicit stream APIs only
where a consumer needs them. A Validation-specific generator is a later
convenience option. No production APIs, dependencies, support metadata or release
scope change in this investigation.

## Cohort and support status

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
the other. A future Validation generator should emit direct runtime operations,
for example the appropriate `ObservedProperty` API, rather than emit fresh
`WhenAnyValue` calls and assume another generator will rewrite them.

## API and warning boundaries

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

## Generated consumer results

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

## Native consumer results

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

## Incremental migration design

The smallest existing route is to construct `ValidationContext`, supply an
`IObservable<IValidationState>` to `ObservableValidation<TViewModel,TValue>`,
register that component with `Add`, and subscribe to `ValidationStatusChange`
with a direct callback. Remove the rule and dispose its observation when its
owner ends. This preserves standard aggregation while avoiding library property
observation and target reflection. The supplied-observable rule/helper path is
another tested route, with the release's caller annotations still present.

The minimal native design should accept caller-created `IObservable<T>` inputs,
ordinary delegates and explicit property names or generated metadata. It should
subscribe directly to `IValidationState` streams and invoke typed callbacks at
the presentation boundary. Avoid discovering setters, observing `ViewModel`
through an expression, or resolving arbitrary properties by reflection on this
path. For model/helper/context replacement, accept an explicit outer observable
and switch to the selected inner stream, including null selections and disposal.
Preserve the existing captured rule ownership and synchronous domain-state
contracts.
Property metadata must retain full paths and exclusive/strict rule matching.
New typed observations should seed actual active rule state, while legacy
callback overloads keep their documented empty prelude. Domain validity remains
synchronous on its owner; dispatch UI presentation at the adapter boundary.

The existing
[observable validation component](../src/ReactiveUI.Validation/Components/ObservableValidation%7BTViewModel,TValue%7D.cs)
is a useful foundation. The
[context](../src/ReactiveUI.Validation/Contexts/ValidationContext.cs) constructs
observable-as-property helpers, and the
[helper](../src/ReactiveUI.Validation/Helpers/ValidationHelper.cs) retains a
trimming annotation. The native consumers exercise these objects, but removing
their annotations needs analysis of generated implementation and supported
inputs beyond those executions. A custom context implementation is not required
merely to avoid the warning message; it would introduce a separate aggregation
contract.

Introduce the native route additively. Keep existing expression overloads,
annotations, namespaces, schedulers, null behavior and formatter contracts.
Share rule-state and ownership logic where possible without routing the native
overloads back through expression observation. Review both public API baselines;
removing an annotation is an observable contract change, not a cosmetic cleanup.
Run a producer audit with `EnableAotAnalyzer` and `EnableTrimAnalyzer` enabled,
including unannotated exposed paths and emitted code otherwise hidden behind
RDC/RUC boundaries. Narrow overly broad annotations only after that analysis and
the matching package consumers verify the exact generated constructor or
observable path. Add native/trim consumer gates for the safe subset and retain
the correct annotations on unsafe legacy surfaces.

`IsAotCompatible` enables analyzer/trimmability settings and advertises a library
compatibility contract; it does not mean every API is unconditionally safe.
A compatible library may retain correctly annotated RDC/RUC APIs for consumers
that deliberately use them. Keep the flag unset in this investigation because
the complete exposed unannotated surface has not received that producer audit,
not because every legacy expression overload must first be removed. The
[official property guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
describes the settings it enables.

## Implementation options

| Option | Role | Benefit | Cost and limit |
| --- | --- | --- | --- |
| Keep corresponding Binding runtime dependencies | Required by the current cohort | Preserves existing rule and view binding behavior | Existing dynamic observation remains subject to its annotations and native runtime constraints. |
| Explicit observables, delegates and state subscriptions | First support target | Makes ownership and native reachability explicit; existing low-level route needs no new generator | Document and verify the context/helper subset; additive convenience APIs require both-flavor API and native consumer gates. |
| Binding interceptors in the application | Optional | Generates recognized application observation/binding calls | Compiler/interceptor configuration and generated-output verification; does not replace package-internal calls. |
| SourceGenerators in the application | Optional | Reduces reactive property, command and notification boilerplate | Select compatible flavor/output, inspect generated code; OAPH/view generation belongs to Binding in the current split. |
| Validation-specific source generator | Future convenience layer | Could preserve familiar selector syntax while producing explicit observations and metadata | New generator package, analyzer diagnostics, selector coverage, compilation/flavor tests and versioning. Build it only after the native runtime contract is stable. |
| Library `IsAotCompatible` contract | Future support decision | Enables producer analyzers and communicates the compatibility contract | Audit exposed unannotated paths and dependencies, retain warnings on unsafe APIs, verify safe-subset native gates and define platform scope. |

## Native platform scope

NativeAOT binaries target a specific runtime identifier and require the native
toolchain and libraries for that platform. Ordinary .NET build/test success on
Linux and Windows does not stand in for a native publish/run. Keep the probes
opt-in and standalone from the shipping solution, reuse the locked development
environment, and record native compiler/runtime inputs with each result.
Only `linux-x64` native execution is established here. A runner accepting another
RID does not verify `win-x64`, Linux Arm64, macOS or cross-compilation; those need
matching toolchains and actual execution on the corresponding hosts.

The retained AndroidX and desktop/mobile samples remain outside core releases.
This investigation does not add Android, iOS, macOS, Windows desktop UI,
NativeAOT cross-compilation or Runic bridge/browser support. A future platform
expansion must define its RID, UI/framework cohort and native consumer behavior
under the [maintenance policy](maintenance.md#validation-and-development-environment).
