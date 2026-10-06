# Generated validation capabilities

This reference lists the 43 capabilities of the generated validation surface:
what the Validation generator handles automatically, which explicit API applies
where automatic support is impossible, and where each capability is tested. For
the architecture see the [design](generated-validation-design.md); for code
recipes see the [migration guide](examples/generated-validation.md).

Every capability has three possible shapes:

- **Automatic**: ordinary source syntax is lowered by the generator into typed
  getters, notifications and setters, with no reflection.
- **Explicit typed route**: the application supplies a contract the compiler
  cannot infer (a notification provider, stable storage, a replacement lens, a
  registered plan or a cancellation policy).
- **Impossible automatically**: the constraint is stated and the explicit route
  above is the supported alternative. A diagnostic alone is never the answer.

Normal calls that are neither intercepted nor matched by a supplied/registered
plan fail with an actionable error. There is no hidden reflection fallback; the
`*Unsafe` methods are the only reflection-based routes and keep
`RequiresUnreferencedCode`.

**Coverage.** "Gate cases" are the named application cases in
[examples/GeneratedValidation](../examples/GeneratedValidation/README.md), which
[`eng/verify-generated-validation.py`](../eng/verify-generated-validation.py)
runs against the packed packages in both flavors, managed, fully trimmed and
NativeAOT, on Linux x64 and Windows x64. The mapping is kept in
[`generated-capability-cases.json`](../eng/verification-fixtures/generated-capability-cases.json).
Test suites live in `src/tests/ReactiveUI.Validation.SourceGenerators.Tests`
(compiler) and `src/tests/ReactiveUI.Validation.Tests` (runtime, run per flavor).

## Typed `Func` overloads and the `Expression` boundary

The normal API has **57 definitions**: the **29 original definitions** with their
CLR signatures preserved (4 predicate `ValidationRule` overloads, 14 expression
binding extensions and 11 static `ValidationBinding.For*` factories; 28 take an
`Expression` selector and one, `ForViewModel(view, Action<TOut>, formatter)`, has
no selector) plus **28 typed counterparts** taking `Func` selectors (4 predicates,
14 bindings, 10 selector-taking factories).

The `Func` overloads carry `[OverloadResolutionPriority(1)]`, so an inline lambda
binds to them and the compiler builds a delegate, not an expression tree. The
generator lowers that lambda through the same semantic plan as before: same
dependencies, paths, null/initial policy and target storage. A stored
`Expression<Func<...>>` variable still binds to the original overload. Calls
compiled against an older package keep working through the preserved signatures
(see S07).

An interceptor replaces the invoked method, **not the evaluation of its
arguments**. When the original expression argument needs dynamic code to build,
for example an expanded params indexer `x => x[x.Key, 1, 2]` (which emits
`Expression.NewArrayInit`), NativeAOT reports IL3050 at the caller. The analyzer
explains this with warning `RUVG011` for AOT-enabled projects. To avoid it, use
the `Func` overload with the same inline lambda, a typed `ValidationSelector`, or
pass a preconstructed array. Nothing suppresses IL3050. Covered by
`ExpressionConstructionCompilerTests`, `FuncProducerCompilerTests`,
`FuncRootTransportCompilerTests` and the
[`ExpressionParams`](../eng/verification-fixtures/ExpressionParams/ExpressionParams.cs)
control in the generated gate. The gate checks both inventories (29 preserved, 28
typed) separately; registered and observable calls never count toward either.

The same applies to expression trees the application stores itself. Building a
lifted user-defined operator on a nullable struct, such as
`x => x.Number + x.Number` with `Count?` operands, throws `NotSupportedException`
under NativeAOT without an IL3050 warning. That is a .NET runtime boundary,
independent of Validation. The `Func` overload with the same inline lambda works.
The `rules-generics-private-captures-indices` gate case covers both sides.

## Compiler, types and access

Suites: `CompilerTests`, `AccessBridgeCompilerTests`, `AccessBridgeLexicalCompilerTests`,
`RuleCapabilityCompilerTests`, `BindingCapabilitiesCompilerTests`, `ExtensionConstraintCompilerTests`.

| ID | Behavior and alternative | Gate cases |
| --- | --- | --- |
| C01 Safe metadata classification | Overload classification uses the overload the compiler actually selects. Supplied-observable and metadata-only overloads (for example a generated `SubmitCommand` used as metadata) never need interception and get no false `RUVG001`. A genuinely unsupported normal call still fails with `RUVG005`. | `initial-field-and-text`, `real-producer-commands-collections-oaph` |
| C02 Open generic contexts | Open model/method type parameters, nested generic owners and their constraints are supported through legal typed bridges that keep the original method arity (an interceptor cannot live inside a generic type). For C#14 extension calls, constraints come from the logical extension declaration, not the synthesized shim ([Roslyn #79896](https://github.com/dotnet/roslyn/issues/79896)). | `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| C03 Private/inaccessible members | Private, protected and nested members of a **partial** containing type are reached through lexical bridges generated in that type. Protected setters are only used through a legal derived receiver. Members of a private generic class nested in a **non-partial** type (even closed, such as `Hidden<string>`) are not supported automatically and report `RUVG006`: make the enclosing type partial, or supply a typed `ValidationLens`/`ValidationCell` factory (see `AccessNonpartialGenericFactory` in [AccessCompatibilityScenario.cs](../examples/GeneratedValidation/AccessCompatibilityScenario.cs)). Other external/non-partial members need an exported typed accessor or an explicit `GeneratedValidationAccessAttribute` bridge. | `selected-static-access-owner-replay`, `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| C04 Anonymous and file-local shapes | Anonymous projections and file-local hosts are handled through inferred typed factories; the generator never names an inaccessible type from its own namespace or invents a public surrogate. | `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| C05 Init-only and readonly targets | Ordinary accessible setters are used directly. Existing `init` setters and named readonly fields are written only when explicitly selected with `GeneratedValidationAccessAttribute` (a .NET 10 accessor bridge). Getter-only properties never have a backing field guessed; use an immutable replacement (`ValidationLens`) instead. | `selected-static-access-owner-replay` |
| C06 Closed generic and internal types | Closed generic nested models/targets and same-assembly internal types work as before. Accessor bridges keep `MaybeNull`/`NotNull` read flow separately from `AllowNull`/`DisallowNull` setter input, including `Nullable<T>`. | `selected-static-access-owner-replay`, `rules-generics-private-captures-indices` |

## Selectors, dispatch and storage

Suites: `SemanticPlannerTests`, `SemanticOperationBoundaryCompilerTests`,
`GenericSelectorProvenanceCompilerTests`, `StaticSelectorOperandCompilerTests`,
`RuleCapabilityCompilerTests`, `BindingCapabilitiesCompilerTests`,
`RuntimeDispatchPolicyCompilerTests`; runtime `CapabilityCatalogTests`,
`CapabilityFallbackTests`, `DelegateCapabilityCatalogTests`, `DelegateCapabilityRuntimeTests`.

| ID | Behavior and alternative | Gate cases |
| --- | --- | --- |
| S01 Casts and conversions | Interface/base casts, boxing, checked, nullable and user conversions are read through typed getters with their dependencies. A target conversion has no inferred inverse: assigning through it needs an explicit writable lens. | `rules-generics-private-captures-indices`, `bindings-generics-storage-policies`, `selected-static-access-owner-replay` |
| S02 Fields | Field reads and writes are generated. A field write raises no event, so live updates need a notifying owner, `ValidationCell.Invalidate` or a `ValidationDependency.Create` provider; silent writes are never claimed to be observed. | `rules-generics-private-captures-indices`, `selected-static-access-owner-replay`, `typed-providers-components-host-adapter` |
| S03 Computed, captured and method expressions | Multi-member computations and captured values are supported with their member dependencies. An opaque method call is not a notification source: declare its dependencies or supply an observable. | `rules-generics-private-captures-indices`, `precompiled-fresh-expressions-callables` |
| S04 Indexers | Index reads/writes keep exact argument order, optional/default/params arguments and enum default conversions, with live argument dependencies. C#14 rejects reordered named arguments in expression trees (`CS9307`); the same lambda through the `Func` overload, or an authored typed selector, is the alternative. | `rules-generics-private-captures-indices`, `bindings-generics-storage-policies`, `historical-peer-unchanged-original-api` |
| S05 Stored selectors and factories | A selector whose value is provably fixed (immutable local, fixed generic definition) is generated, preserving operators, conversions, `nameof(T)` and block returns. Runtime choices (mutable locals, `in` escapes, static abstract/virtual factories, readonly fields rewritten in a static constructor) use a `ValidationPlanRegistry`/`IValidationPlanProvider` plan. A registered match always wins; the compiled fallback is used only when no plan is registered, never to hide ambiguity or failure. | `rules-generics-private-captures-indices`, `precompiled-fresh-expressions-callables`, `historical-peer-unchanged-original-api` |
| S06 Delegates and method groups | Normal methods passed as delegates or method groups cannot be intercepted; they dispatch through explicitly registered typed plans when the caller opts in with `ValidationRuntimeDispatchAttribute`. Unregistered use fails (`RUVG007`). | `precompiled-fresh-expressions-callables`, `historical-peer-unchanged-original-api`, `bindings-generics-storage-policies` |
| S07 Precompiled callers | Already-compiled IL cannot gain interceptors. Normal methods dispatch through registered plans: structural patterns (`RegisterSelectorPattern`/`RegisterTargetPattern`) match fresh expressions from old binaries and bind the current receiver and typed arguments per call. Registrations are bounded and scoped by leases; ambiguity fails. A native executable must be relinked; swapping a DLL changes nothing. | `historical-peer-unchanged-original-api` |
| S08 Non-INPC and custom providers | Reactive/INPC owners are observed automatically. Other sources use a typed `ValidationDependency.Create` provider, a snapshot or explicit invalidation; provider errors, completion and disposal are owned by the binding. | `typed-providers-components-host-adapter`, `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| S09 Struct storage | A struct cannot be observed or mutated through a temporary copy. `ValidationCell<T>` holds it by reference and `ValidationLens<TStorage,TValue>` writes changes back through every enclosing segment. Concrete struct views fail with `RUVG006`; a stable box behind a notifying interface is supported. | `selected-static-access-owner-replay`, `bindings-generics-storage-policies` |
| S10 Calls emitted by other generators | Generators cannot intercept each other's output. A peer generator emits typed registered calls (or normal calls with registered plans); unregistered normal calls still fail, independent of generator ordering. | `peer-generated-registered-callers` |
| S11 Ref structs and stack data | A subscription cannot outlive a ref-struct borrow. `ValidationSnapshot.Read`/`Validate` validate synchronously; copy data into owned storage for live observation. | `nullable-output-stack-snapshots`, `selected-static-access-owner-replay` |

## Producer and XAML contracts

The generator sees peer-generated members through a private, never-emitted
projection of the pinned producers listed in
[ProducerProfiles.md](../src/ReactiveUI.Validation.SourceGenerators/Producers/ProducerProfiles.md)
(ReactiveUI.SourceGenerators 4.2.0, ReactiveUI.Binding 9.1.0, Avalonia 12.1.3,
MAUI 10.0.110). Final checks compare the projection with the actual output
(`RUVG008`); an unsupported version reports `RUVG009`, unresolved XAML `RUVG010`.
Setting `ReactiveUIValidationProducerProjectionEnabled=false` disables projection
only. Suites: `ProducerCompilerTests`, `ProducerInteropCompilerTests`,
`ProducerXamlCompilerTests`, `PlatformProducerCompilerTests`, `CapabilityCompilerHostTests`.

| ID | Behavior and alternative | Gate cases |
| --- | --- | --- |
| P01 `[Reactive]` fields | Properties generated from `[Reactive]` fields (including `_name`/`m_name` prefixes, nullability, access and inheritance) are visible in every selector position. Alternative: declare a `[Reactive] partial` property. | `real-producer-commands-collections-oaph` |
| P02 Generated notification interfaces | `[IReactiveObject]`-generated notification contracts make an owner observable. Some nested producer shapes are not supported; derive from a ReactiveObject base or declare the interfaces. | `real-producer-commands-collections-oaph` |
| P03 Generated commands | Synchronous, `Task` and observable commands with their naming, input and cancellation-token contracts can be used as metadata and gated by explicit blocking state. | `real-producer-commands-collections-oaph` |
| P04 Generated collections | Generated reactive collections and derived lists are observed with item, same-key and whole-source replacement. Collection ownership is not invented; use `ValidationCollection` for owned rows. | `real-producer-commands-collections-oaph`, `stable-rows-source-ownership` |
| P05 Declared partials and OAPH | Declared `[Reactive] partial` properties and Binding `[ObservableAsProperty]` partials (initialized with `ToProperty`) need no projection. | `real-producer-commands-collections-oaph` |
| P06 XAML controls | Named MAUI/Avalonia controls are resolved from AdditionalTexts metadata; WPF/WinUI declarations are already in source. WinForms producers emit nongeneric `IViewFor`, so use `ValidationViewAdapter<TOwner,TModel>`. This is compiler/provider integration, not native UI host support. | `typed-providers-components-host-adapter` |
| P07 Incremental projection | Projection is incremental, deterministic, cancellable, keeps original locations and is order-independent with other generators; drift is an error rather than a silent fallback. | `real-producer-commands-collections-oaph` |

## Runtime behavior and compatibility

Suites: `CapabilityAccessTests`, `CapabilityFacadeTests`, `CapabilityObservationTests`,
`CapabilityOwnershipTests`, `CapabilityLegacySequenceTests`, `CapabilityPathCompatibilityTests`,
`CapabilityDefaultContextTests`, `CapabilityWriteRefreshTests`, `ValidationCapabilityAdapterTests`,
`ValidationOwnedAdapterTests`, `ValidationErrorHookTests`, plus the rule/binding compiler suites.

| ID | Behavior and alternative | Gate cases |
| --- | --- | --- |
| R01 Rule metadata and states | Rules keep full structural paths, strict multi-property membership and complete custom states (code, severity, revision). `ValidationPath.Legacy(fullPath)` opts into ordinal display-name matching. | `helper-model-rich-state`, `property-state-membership`, `static-factory-raw-nullable-projections`, `historical-peer-unchanged-original-api` |
| R02 Context ownership | Default context comes from `IValidatableViewModel` through interface dispatch; selected contexts stay independent; a helper removes its rule only from the context captured at registration. | `context-replacement`, `helper-model-rich-state`, `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| R03 Null policy | A missing parent is distinct from a present null leaf. `ValidationMissingOwnerPolicy` selects DefaultValue (default), Suppress (legacy) or Fallback; null targets skip assignment and cache the output. | `nested-null-and-replacement`, `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| R04 Initial emission | Normal bindings deliver actual initial state. `ValidationInitialSequence.LegacyEmpty`/`LegacyValid` restore the old presentation prelude explicitly. | `initial-field-and-text`, `rules-generics-private-captures-indices`, `bindings-generics-storage-policies` |
| R05 Target handoff | Each output is assigned to the current target; a notified target replacement replays the latest output immediately, a silent replacement receives the next output. Typed outputs (bool, enum, nullable struct) are assigned exactly. | `nested-target-handoff`, `static-factory-raw-nullable-projections`, `selected-static-access-owner-replay`, `bindings-generics-storage-policies` |
| R06 Reentry, errors and disposal | Reentrant replacement/removal/disposal, getter/callback/provider failures and cleanup failures are handled; disposal never disposes borrowed models, contexts or sources. | `precompiled-fresh-expressions-callables`, `historical-peer-unchanged-original-api`, `bindings-generics-storage-policies`, `rules-generics-private-captures-indices`, `domain-presentation-independence` |
| R07 Async and collections | Generation cannot infer request lifetime. `ValidationAsync.ForLatest` (cancellation, stale-result rejection), `ValidationCollection.Observe` and `ValidationRowLease` provide owned policies. | `rows-owned-observable-results`, `latest-requests-owner-scheduling`, `stable-rows-source-ownership`, `removed-pending-row-cancellation` |
| R08 Scheduling | Domain validity is synchronous on the model owner; UI presentation is dispatched explicitly through `ValidationScheduling`. No concurrent mutation is promised. | `domain-presentation-independence`, `latest-requests-owner-scheduling`, `real-producer-commands-collections-oaph` |
| R09 Error hook | `ReactiveValidationObject.RaiseErrorsChanged(string)` is protected virtual; overrides must call base to raise `ErrorsChanged`. `HasErrors` updates before the event. | `virtual-error-hook-order-and-disposal` |
| R10 Direct components | `BasePropertyValidation` has typed constructors next to the reflection-based expression constructors (which keep `RequiresUnreferencedCode`). | `typed-providers-components-host-adapter` |
| R11 Static binding factories | The ten selector-taking `ValidationBinding.For*` factories are generated like the extensions, keeping `FormatAll`, formatter resolution and strictness. | `static-factory-raw-nullable-projections` |

## SDK, warnings and packaging

Suites: `CapabilityCompilerHostTests`, `CapabilityAnalyzerOptionsTests`,
`CompilerLoadingPolicyTests`, and the Python gate tests in `eng/test_verify_*.py`.

| ID | Behavior and alternative | Gate cases |
| --- | --- | --- |
| A01 Generic observable ergonomics | The six safe observable methods are classic `this` extensions to avoid a C#14 nullable-generic `CS8714` bug. Explicit observable binding calls spell `<TSource, TOut>`; `ValidationOutput<TOutput>.FromStates`/`FromPropertyStates` offer output-first inference. | `nullable-output-stack-snapshots`, `static-factory-raw-nullable-projections`, `bindings-generics-storage-policies` |
| A02 Unsafe and annotation boundaries | Reflection exists only in `*Unsafe` APIs and reflection-based component constructors, which carry precise `RequiresUnreferencedCode`. Safe routes publish without warnings; nothing is suppressed. | `typed-providers-components-host-adapter`, `precompiled-fresh-expressions-callables`, `historical-peer-unchanged-original-api`, `selected-static-access-owner-replay`, `typed-formatter-resolver-lifetimes` |
| A03 Formatter and DI setup | `ValidationTextFormatterRegistration.Register`/`RegisterFactory` register a formatter without assembly scanning; an explicit formatter argument still wins; ownership follows the resolver. | `typed-formatter-resolver-lifetimes`, `typed-providers-components-host-adapter` |
| A04 AOT contract | The .NET 10 core targets declare `IsAotCompatible`. Unsafe APIs remain available and annotated. | `typed-providers-components-host-adapter`, `real-producer-commands-collections-oaph` |
| A05 Analyzer delivery | Both core packages embed the analyzer under `analyzers/dotnet/roslyn5.9/cs` plus `buildTransitive` props; missing, mismatched or disabled assets produce `RUVG003`/`RUVG004`/`RUVG005`. Consumers get no Roslyn runtime dependency. | `initial-field-and-text`, `real-producer-commands-collections-oaph`, `selected-static-access-owner-replay` |
| A06 Dependency graph | Packages depend on ReactiveUI 26.0.1 and the matching Runic.DynamicData 10.0.0-runic.30 flavor; Primitives has no System.Reactive. Gates verify restored bytes and graphs. | `historical-peer-unchanged-original-api`, `initial-field-and-text` |
| A07 Supported hosts | Managed, fully trimmed and NativeAOT execution is verified on Linux x64 and Windows x64. XAML/provider support is compiler integration only; no UI-native or additional RID support is implied. | `typed-providers-components-host-adapter` |
| A08 Existing infrastructure | Generated OAPH dispatch, the context abstraction, read-only helpers, formatter fallback and explicit schedulers are unchanged. | `real-producer-commands-collections-oaph`, `context-replacement`, `typed-formatter-resolver-lifetimes`, `domain-presentation-independence` |
