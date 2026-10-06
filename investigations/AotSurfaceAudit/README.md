# Core surface and dependency inspection

This investigation records the .NET 10 source/producer audit for A02–A04 at
`f960c4bf1ca9efa5682a51a624f0339e49aa6d99`. It adds reproducible read-only IL
inspection to the formatter registration implementation. It is not a packed
consumer, trim/native runtime result, Windows result, or package-wide AOT claim.
The root integration must repeat the audit for subsequent capability changes
and run the required actual-package gates before deciding `IsAotCompatible`.

The later [integrated source checkpoint](IntegratedSourceCheckpoint.md) records
the expanded typed/runtime/adapter surface and preparation of the .NET 10
compatibility flags. Its source findings and completion gates are separate from
this historical producer cohort.

## Reproduce the byte inspection

`InspectIl --metadata-only <assembly.dll> ...` reports each target's SHA256,
assembly version, MVID and compatibility metadata without enumerating method
bodies. Targets remain PE/metadata inputs and are never loaded or executed.

Reuse the locked Runic SDK shell and existing package caches. The inspector has
no package dependencies. It uses `PEReader`/`MetadataReader`; it never loads or
executes inspected assemblies. It prints hashes, assembly compatibility
metadata, type/method trimming/dynamic-code annotations and explicit method, field
and type operands, including closed generic method instantiations. Selection
includes all Validation bodies, the relevant Splat locator/resolver bodies,
Binding observation/fallback/setter bodies and generated OAPH interceptors.
The extended selection also covers the concrete Primitives task/cancellation
and scheduled-delivery bodies used by the integrated adapters, without selecting
unrelated expression-based extension APIs.
This is an inspection aid, not an automatic reachability proof.

From the repository root, set `RUNIC_SDK` to the existing locked Runic SDK shell
directory:

```sh
direnv exec "$RUNIC_SDK" dotnet build investigations/AotSurfaceAudit/InspectIl/InspectIl.csproj -c Release -p:BuildInParallel=false -p:TreatWarningsAsErrors=true
direnv exec "$RUNIC_SDK" bash -c 'dotnet investigations/AotSurfaceAudit/InspectIl/bin/Release/net10.0/InspectIl.dll \
  src/ReactiveUI.Validation/bin/Release/net10.0/ReactiveUI.Validation.dll \
  src/ReactiveUI.Validation.Reactive/bin/Release/net10.0/ReactiveUI.Validation.Reactive.dll \
  "$NUGET_PACKAGES/splat.core/21.0.0/lib/net10.0/Splat.Core.dll" \
  "$NUGET_PACKAGES/reactiveui.binding/9.1.0/lib/net10.0/ReactiveUI.Binding.dll" \
  "$NUGET_PACKAGES/reactiveui.binding.reactive/9.1.0/lib/net10.0/ReactiveUI.Binding.Reactive.dll" \
  "$NUGET_PACKAGES/reactiveui.primitives/9.0.0/lib/net10.0/ReactiveUI.Primitives.dll" \
  "$NUGET_PACKAGES/reactiveui.primitives.reactive/9.0.0/lib/net10.0/ReactiveUI.Primitives.Reactive.dll"'
```

For a package gate, inspect DLLs extracted from that gate's actual package bytes
instead of local build outputs. Keep task-owned raw dumps in
`artifacts/verification/aot-surface-audit`; the retained summaries below identify
the source and producer/dependency cohort without committing megabytes of IL.
Remove disposable inspector outputs when no longer needed, keeping useful
shared caches and the evidence required to explain failures.

## Exposed API and operation boundaries

Both .NET 10 baselines contain 203 explicit public/protected declarations at this
source, including types, constructors, properties, fields, events and methods.
The [surface manifest](evidence/2026-10-06-formatter-surface.json) pins both exact
baseline hashes, inspected producer DLL hashes and every shared-source file hash.
The baselines enumerate every
overload; the following table groups them by the actual reachable operation.
Every row applies to both namespace/scheduler flavors.

| Public surface | Reachable implementation and boundary |
| --- | --- |
| `IValidatableViewModel`, component/context/state/binding/formatter interfaces | Typed interface contracts. Implementations supplied by an application retain their own obligations. |
| `ValidationText`, `IValidationText`, `ValidationState`, `ValidationStateComparer` | Typed collection/string/state operations; no value reflection, scanning or expression execution. |
| `BasePropertyValidation<TViewModel>` members and protected `AddProperty` | Typed replay/activation/disposal and `ExpressionExtensions.GetPropertyPath`, which reads only expression structure and `MemberInfo.Name`. |
| Three public `BasePropertyValidation<TViewModel,TViewModelProperty>` expression constructors | RUC at the entry point. Main internal constructor (`Components/BasePropertyValidation{TViewModel,TViewModelProperty}.cs:94`) calls `WhenAnyValueUnsafe` at line 112. This is reflected value observation, not a metadata-only selector. |
| `ObservableValidationBase`, both `ObservableValidation` arities and constructors | Supplied observables and typed delegates; property expressions only contribute names through `AddProperty`. No RUC/RDC is necessary for name-only metadata. |
| `ValidationContext` constructors/members | Delegate-based DynamicData `AutoRefreshOnObservable`, list/query/state operations and generated OAPH `ToProperty` interceptors. Actual producer IL confirms generated calls for both context properties; no `ToPropertyUnsafe` call. |
| `ValidationHelper` constructors/members | Supplied component subscriptions and captured registration cleanup; both OAPH fields use generated interceptors in actual producer IL. |
| `ReactiveValidationObject` constructors/members | Typed state/error export, DynamicData membership, ordinary property notification and formatter resolution. Explicit formatter bypasses resolver lookup. |
| `ObserveFor`, `ContainsProperty`, property/all `ClearValidationRules`, `IsValid` | Typed membership operations. Expressions used for matching contribute only member names; they do not read values or compile expressions. |
| Supplied-observable `ValidationRule` and `AddObservableRule` overloads | Typed state/predicate/message delegates plus optional structural property metadata. No hidden observation is introduced by accepting metadata. |
| Normal predicate `ValidationRule` and normal view/context/state binding overloads | At this source, generated dispatch or actionable missing-generation failure. Typed runtime catalog alternatives are being integrated separately and require a fresh audit. |
| Explicit `ValidationRuleUnsafe` and `BindValidation*Unsafe` overloads | RUC originates in real reflected source observation and, for target expressions, reflected assignment. Action targets eliminate only reflected assignment. Private observation helpers retain RUC too. |
| Eleven public `ValidationBinding.For*` overloads | At this source, all are RUC entry points. Underlying source selection uses `WhenAnyValueUnsafe`; expression target assignment reaches annotated `BindToView` at line 412. Safe/static factory alternatives are integrated by the runtime lane and require fresh evidence. |
| `GeneratedValidationProperty`, `GeneratedValidationObservation` | Typed owner getters and INPC event subscription/removal. No member discovery or expression evaluation. |
| `GeneratedValidationBindingSupport` | Typed target setter/callback handoff, complete state formatting and the same closed generic formatter resolver. |
| `SingleLineFormatter`, `ValidationTextFormatterRegistration` | Explicit typed construction, existing instances or supplied factories. Registration and retrieval use exact closed `IValidationTextFormatter<string>` interfaces in actual IL. No implementation discovery/activation. |

The complete shared-source audit found no product `Expression.Compile`,
interpreted expression execution, dynamic emit, `MakeGenericMethod`,
`MakeGenericType`, `Activator`, assembly scanning, `DynamicDependency`, trimming
suppression, or broad `DynamicallyAccessedMembers` roots. `MemberInfo.Name` for
metadata is distinct from `FieldInfo.GetValue`/`PropertyInfo.GetValue` for value
access. The remaining real reflection operations are within the documented RUC
paths. This finding does not certify arbitrary application callbacks or entire
dependency packages.

## Actual dependency boundaries

The [producer cohort](evidence/2026-10-06-formatter-producer-cohort.json) records
the resolved runtime assets and SHA-256 of every package archive and runtime DLL
for both producer graphs. It preserves the released `.30` pair and matching
flavors; the Primitives producer has no System.Reactive or upstream DynamicData.
System.Reactive belongs only to the Reactive producer. Test-project dependency
graphs are separate, because their reactive test tools intentionally reference
System.Reactive.

| Dependency | Pinned operation and byte/source evidence |
| --- | --- |
| Runic.DynamicData `10.0.0-runic.30` pair, source `9e3039a597f912c6ddc2628e38bb1162ad9c4563` | Context uses `SourceList.Connect`, `AsObservableList`, delegate `AutoRefreshOnObservable`, `QueryWhenChanged` and typed list membership. No expression refresh/grouping API is selected. Package/consumer verification must execute this pipeline. Historical `.5` investigations remain historical. |
| ReactiveUI `26.0.1`, source `88312688d0281fd260977dfd78bb12e1c85ffaaa` | `ReactiveObject` exposes typed event/state helpers. Core builder initialization registers known modules through direct construction; `WithViewsFromAssembly` separately declares RUC and is not a Validation call. Source: [ReactiveObject](https://github.com/reactiveui/ReactiveUI/blob/88312688d0281fd260977dfd78bb12e1c85ffaaa/src/ReactiveUI.Shared/ReactiveObject/ReactiveObject.cs), [builder](https://github.com/reactiveui/ReactiveUI/blob/88312688d0281fd260977dfd78bb12e1c85ffaaa/src/ReactiveUI.Shared/Builder/ReactiveUIBuilder.cs). |
| Binding `9.1.0` pair, source `05c45cec845835d670960fac733bb302bc1c1071` | Actual producer context/helper bodies call generated typed OAPH interceptors. `WhenAnyValueUnsafe` reaches the annotated runtime observation fallback; its chain reads property/field values and resolves an explicitly registered provider. `Reflection.Rewrite` reaches an annotated lazy expression rewriter which may find indexer/length metadata. [Pinned reflection source](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.Shared/Expression/Reflection.cs). |
| Binding reflection setter | Actual DLLs in both flavors implement `GetValueSetterForProperty` with `ldvirtftn PropertyInfo.SetValue` or a closure calling `FieldInfo.SetValue`; `GetValueSetterOrThrow` calls this operation. There is no compile/emit/constructed-generic operation on this path. RUC-only on the Validation setter boundary is precise; adding RDC here would invent a dynamic-code operation. Dedicated packed diagnostic coverage must retain IL2026 and reject a false IL3050. |
| Binding provider initialization | The pinned core module constructs known INPC/POCO providers with explicit typed factories. Runtime provider selection enumerates `GetServices<ICreatesObservableForProperty>` and uses declared affinity. It does not make arbitrary custom providers, platform assemblies or scanning safe. [Pinned module](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.Shared/Builder/ReactiveUIBindingModule.cs). |
| Splat `21.0.0`, source `8cfe76a7b7023f4c1be72113a20cff2b7d77ee96` | `AppLocator` constructs an `InternalLocator`, whose default resolver is `InstanceGenericFirstDependencyResolver`. Typed `GetServices<T>` reads typed containers and an exact-type registry; factories are explicit delegates. Formatter lookup ignores null entries and chooses the last formatter or `SingleLineFormatter.Default`. No scanning/activation occurs. [Pinned locator](https://github.com/reactiveui/splat/blob/8cfe76a7b7023f4c1be72113a20cff2b7d77ee96/src/Splat.Core/ServiceLocation/InternalLocator.cs), [pinned resolver](https://github.com/reactiveui/splat/blob/8cfe76a7b7023f4c1be72113a20cff2b7d77ee96/src/Splat.Core/ServiceLocation/InstanceGenericFirst/InstanceGenericFirstDependencyResolver.cs). |

Splat.Core and both Binding DLLs declare `IsTrimmable=True` and
`IsAotCompatible=True` in their actual .NET 10 metadata. Validation declares
neither at this source. Those dependency declarations do not prove unused APIs,
arbitrary custom service setup, or Validation's newly added paths. Unrelated
Binding `ToPropertyUnsafe` entry points retain RDC/RUC; the producer audit verifies
that Validation's generated OAPH calls do not reach them.

## Formatter verification and AOT decision

Current-source builds with `TreatWarningsAsErrors=true` passed all five formatter
tests per flavor on Linux x64: three new registration/ownership tests and two
existing strict-resolver regressions, ten passes total, no failures/skips. The
tests cover actual flow-local Splat lookup, empty/missing fallback, interface
registration, last registration wins, explicit formatter bypass, deferred
factory construction, null service fallback, construction exceptions, and the
pinned resolver's constant/transient disposal contract. Validation itself owns
neither the formatter nor resolver.

Evidence from the audit worktree is retained under
`artifacts/verification/aot-surface-audit`: `primitives-formatter-run.txt`,
`reactive-formatter-run.txt`, `primitives-formatter.trx`,
`reactive-formatter.trx`, `producer-dependency-il.txt`, and `inspector-build.txt`.
The documented `dotnet run` test-project alternative was used with current
builds, maximum test parallelism two and serialized build dependencies. Early
style/disposal analyzer failures were corrected without suppression. Separate
`dotnet test` invocations reported zero tests; those retained failures are not
counted as successful verification.

`IsAotCompatible` remains unset at the audited source. The concrete decision for
the completed integration must use all exposed normal/typed/direct component
and static-factory routes, complete producer warnings-as-errors analysis, exact
packed graphs/bytes, and actual managed/full-trim/native executions on the
required Linux and Windows hosts. Keep accurate Unsafe diagnostics and the
reflection-only setter control; annotated Unsafe APIs may remain. Do not infer
the package contract from this formatter test subset or from suppressed analysis
of an annotated body. The property also enables trimmability, single-file and
AOT/trim analysis, so it changes a producer contract rather than merely recording
a console result. [Microsoft's compatibility property contract](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers).
