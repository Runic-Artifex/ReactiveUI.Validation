# Generated validation, typed plans and explicit Unsafe APIs

The generated/Unsafe split remains **UNRELEASED**. Expanded capability source is
integrated for verification at `ccd5aad`; the [43-row plan](generated-capabilities-plan.md)
and [completion ledger](generated-capabilities-progress.md) separate landed code,
focused checks and outstanding integrated/package acceptance. No new capability
row is complete. Later documentation commits are not tested package source.

The earlier restricted split was verified at
`2550376b230ccfb78beb8d3b4ced8866b65d809e`, with all four Linux/Windows CI jobs
passing. Its [immutable evidence](upstream/evidence/generated-api-implementation.json)
and [review](upstream/reviews/2026-10-implementation.md#unreleased-generated-api-follow-up)
remain proof for that source and historical package cohort, not the expanded
implementation. Published releases are unchanged.

## Generated, typed and registered routes

Four normal predicate `ValidationRule` overloads, fourteen expression binding
extension overloads and eleven static `ValidationBinding.For*` factories lower
through typed access, observation and assignment plans. Eligible source calls
receive interceptors. Explicit typed factories have real callable bodies when
interception cannot represent the required access or lifetime.

Normal CLR facades also support deliberately registered runtime dispatch.
`ValidationPlanRegistry` has finite capacity, owner attachment and registration
leases. `IValidationPlanProvider` uses original API generic slots and role-specific
`ValidationPlanRequest`; it does not discover members. New source callers opt in
with `ValidationRuntimeDispatchAttribute`, a compiler-visible contract rather
than warning suppression. An unregistered normal call still fails actionably.
There is no hidden reflection fallback.

Explicit `ValidationRuleUnsafe`, `BindValidationUnsafe`,
`BindValidationContextUnsafe`, `BindValidationStateUnsafe` and static factory
Unsafe routes retain reflected observation/assignment and accurate
`RequiresUnreferencedCode`. Supplied-observable/metadata-only rules and explicit
observable APIs retain their safe runtime contract.

## Compiler and package delivery

Both branded core packages embed the analyzer under
`analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll` and
matching `buildTransitive` props allowlisting `ReactiveUI.Validation.Generated`.
Retain these assets when recompiling normal calls. No separate Validation
generator package is needed. The compiler cohort is SDK 10.0.401/C#14/Roslyn 5.9.
Keep one ReactiveUI 26.0.1 flavor and released DynamicData `.30` counterpart.
Primitives uses `ReactiveUI.Validation.Capabilities`; Reactive uses
`ReactiveUI.Validation.Reactive.Capabilities` and its matching scheduler cohort.

Generic interceptor methods preserve original arity/constraints inside a
nongeneric containing type. Legal lexical partial bridges or typed providers
handle private/open generic contexts. A caller host alone cannot supply hidden
generic arguments absent from the original receiver/arguments. Nonpartial,
external, anonymous/file-local or otherwise unnameable contexts have authored
typed factory routes; generation does not erase every type into `object`.

For C#14 extension-form calls, constraints come from the bound logical extension
declaration and map by ordinal to its CLR shim's original generic slots. This
avoids copying the synthesized shim's incorrect `notnull` metadata described in
[Roslyn #79896](https://github.com/dotnet/roslyn/issues/79896), while preserving
genuine declared constraints and CLR signature arity. Static-form calls retain
the constraint contract of the static method that the compiler actually bound.

## Observable extension compatibility correction

The six safe observable APIs keep traditional `this` extensions for the measured
nullable-generic CS8714 C#14 static-bridge bug. CLR signatures and inferred calls
remain compatible. Explicit observable binding calls use `<TSource, TOut>`;
`AddObservableRule<TValue>` retains its arity. `ValidationOutput<TOutput>.FromStates`
and `FromPropertyStates` add output-first factories with source inference. The
scoped SST1703 exception does not suppress IL warnings. Historical before/after
nullable proof retains its original source/compiler identity.

## Selector and assignment support

The semantic planner separates readable operations, notification dependencies,
path metadata and writable storage. Property/field paths, conversions, computed
expressions and index arguments use typed getters. A conversion has no inferred
inverse setter. Opaque method effects, silent owners and unknown notification
providers need explicit typed dependencies, manual invalidation or a snapshot.
The implementation cannot infer nonexistent notifications; legacy struct boxing
was not mutation of the caller's original struct.

`ValidationSelector<TSource,TValue>` binds to `ValidationAccessPlan<TValue>`.
`ValidationRead<TValue>` carries value, owner availability and current paths in
one snapshot: a present null leaf differs from a missing parent.
`ValidationDependency.Create` accepts a reference owner getter and typed
subscription callback; property/collection adapters are explicit. Cold
`CreateObservation` factories provide per-subscription caches. `AfterRead`
handles dependencies on owners cached by that read. Metadata-only binding
selection uses a separate path reader without executing the selected leaf getter.
Structural path changes count even when values compare equal, with old/new error
display-path notification.

`ValidationPath.Legacy(fullPath)` deliberately selects complete ordinal display
name membership, including a structural counterpart with that exact name.
When both paths are Structural, member/index/conversion and comparer-domain
identity remains authoritative. This compatibility membership rule does not
change structural equality/hashing or make suffixes such as `Name` match
`Address.Name`. Choose Legacy only when name-based compatibility is intended.

`ValidationTarget<TSource,TOut>` binds to `ValidationWritePlan<TOut>` with stable
reference/slot identity and a typed assignment. Null targets skip assignment and
cache latest output. Declared replacement notifications immediately replay it,
including for references that compare equal. Each new domain output also refreshes
the current target and dependency owners through the same owned engine, so a
silent replacement receives that new output. This does not fabricate an event or
replay an old output into a silently replaced target. `ValidationCell<T>` owns mutable structs or
immutable roots by reference. `ValidationLens<TStorage,TValue>` reads current
storage and supplies complete replacement/write-back. Root stack structs cannot
be retained by a by-value binding; cells/lenses or synchronous
`ValidationSnapshot.Read`/`Validate` provide usable alternatives. Ref-struct
snapshots borrow storage synchronously without creating subscriptions.

Private/protected ordinary access is checked in its original legal context.
A readable public getter does not make its protected setter legal through a
base-typed receiver. Supply a typed setter in a legal derived context when needed.
`GeneratedValidationAccessAttribute` selects actual property/field/accessor
storage for static compatibility bridges, including existing init setters and
named readonly fields. It never infers a getter-only backing field. .NET 10
accessor bridges preserve exact declaring types, generic positions and constraints.
`UnsafeAccessorType` can erase an inaccessible reference owner with a nameable
byref field value; inaccessible byref field value types and value-type owners
require legal lexical getters/lenses or replacement. Managed static bridge proof
is separate from pending automatic/package/native acceptance.

Accessor flow metadata is part of the contract: effective property/field/
getter-return MaybeNull/NotNull postconditions must survive exact read bridges,
separately from setter AllowNull/DisallowNull input, including Nullable<T> values.
The latest 29-case accessor and 80-case binding managed proofs cover these
contracts; actual-package/full-trim/native acceptance remains pending. An authored
typed getter in a legal C# context does not create an invented nonnull guarantee.

Shared storage uses `ValidationStoragePolicy` and disposable
`ValidationWriteReceipt` instances. Custom bound plans attach
`ValidationDependency.Writer(receipt)` for queued-failure cleanup. Peer writes
are serialized on the source owner and read current storage before copy-back;
direct mutation during replacement construction is a conflict. This does not
promise arbitrary concurrent mutation. Final cross-binding and missing-owner
Suppress acceptance is tracked separately in the ledger.

## Runtime selectors, captures and precompiled consumers

Sound finite source provenance can be generated. Mutable runtime selection,
method groups and precompiled calls use typed factories or explicit catalog/
provider dispatch. Instance registration identifies one expression. Structural
pattern registration matches fresh expressions against predeclared operations,
then binds the current receiver and supplied typed arguments. Registration is
bounded/scoped; ambiguity fails deterministically.

Finite provenance must prove actual dispatch and final storage. A static
abstract/virtual interface selector factory can execute another implementation;
a readonly selector field can be reassigned or escaped from its static
constructor. The generator cannot substitute a convenient source body/initializer
for those runtime values. An implicit `in` argument can also expose a mutable
local selector without an `in` token at the call. These runtime choices use typed
factory/provider/catalog routes. Constructed generic fixed definitions preserve
their actual operator/conversion, checked/lifted/reference-equality semantics,
`nameof(T)` constant and finite block returns. The 70-case planner/boundary/generic
managed proof covers these distinctions; package/native acceptance is separate.

Finite generated plans use catalog-preferred fallback: a matching explicit plan
wins, and only genuine lookup absence selects the compiled typed descriptor.
Ambiguity, mismatched or invalid matched plans and factory failures propagate;
fallback never masks them. Opaque runtime choices still require registration.

Optional/default/params index arguments are admitted by the locked compiler.
Omitted enum and nullable-enum defaults retain their exact implicit-conversion
operation and typed cast. Reordered named arguments in an expression tree are
rejected by C#14 with CS9307. The executable alternative is an authored `Func`
getter with typed access/dependencies and metadata; its argument keys/evaluation
order remain explicit. It is not an expression-tree interception claim.

`ValidationExpressionPattern` inspects finite expression structure and opaque
member identities. It never compiles/evaluates trees, reads closure fields using
reflection or constructs arbitrary generic types. Private compiler-generated
captures need a legal typed extractor, explicit closure binding or a peer-exported
provider/argument contract. Unknown captures fail actionably; shape matching
must never freeze the first invocation's receiver/capture.

Package replacement cannot add interceptors to old IL. Registered normal facades
can serve unchanged precompiled managed callers through preserved CLR signatures;
fresh-expression ABI and native-link acceptance remain pending. An already built
native executable cannot acquire this behavior by replacing a managed DLL.
Recompilation, typed registration, supplied observables and deliberate Unsafe
migration are distinct choices.

## Known producer interoperability

Ordinary generators do not consume peers' normal source output; fixed post-init
attributes/helpers can be visible. Bundling analyzers creates no ordering. The
frontend uses a private, never-emitted declaration projection for exact
[profiles](../src/ReactiveUI.Validation.SourceGenerators/Producers/ProducerProfiles.md):
SourceGenerators 4.2.0, Binding 9.1.0, Avalonia 12.1.3 and MAUI 10.0.110.
Real producers own implementations, notifications, initialization and validity
diagnostics. Final checks compare actual nullability, accessibility, member/
accessor/storage and generic/inheritance contracts, not just matching names.

Declared partial Reactive/OAPH properties need no prediction. Known field,
interface, command, collection and marked XAML projection has focused
producer/platform compiler proof recorded in the ledger; actual-package and
host acceptance remains pending. WPF/WinUI declarations
present before Csc are used directly. MAUI/Avalonia named fields need exact
AdditionalTexts/build metadata. WinForms profiles do not invent `IViewFor<T>`
where producers emit nongeneric `IViewFor`; `ValidationViewAdapter<TOwner,TModel>`
supplies a typed model route. Compiler/provider integration is not native UI
execution or an advertised platform expansion. The
[interop investigation](generated-member-interop.md) preserves historical `.5`
measurements separately.

| Diagnostic | Current action |
| --- | --- |
| RUVG001 / RUVG006 | Selection/dependency/storage cannot be generated: supply the indicated typed selector, target or provider. |
| RUVG002 | No legal accessible/generic bridge: use a legal lexical/provider/factory route. |
| RUVG003 / RUVG004 | Interception location or runtime contract mismatch: inspect compiler and matching analyzer/runtime assets. |
| RUVG005 / RUVG007 | Final normal call/reference lacks interception or explicit runtime-dispatch opt-in. Register deliberately; `nameof` remains allowed. |
| RUVG008 | Actual producer contract differs from projection; correct profile/input. No fake declaration is emitted. |
| RUVG009 / RUVG010 | Unsupported/missing resolved profile or malformed/unresolved marked XAML; use supported metadata or typed descriptors. |

`ReactiveUIValidationProducerProjectionEnabled=false` disables prediction,
not final dispatch guards. Declared/typed routes remain available. Analyzer
removal neither installs reflection nor supplies a missing registration.

## State, lifetime and acceptance

Rules preserve complete states, full paths and captured registration context.
Default context respects `IValidatableViewModel` and explicit typed default-context
provider selection. Selected contexts dispatch through validation interfaces,
so shadow members do not replace that contract. Helper disposal removes its
component, not borrowed context/model/source ownership. Blocking/advisory contexts
stay independent; replacement does not transfer existing registrations.

Normal presentation defaults to actual initial states. Explicit
`ValidationInitialSequence.LegacyEmpty`/`LegacyValid` select compatibility preludes
without manufacturing raw domain validity. `ValidationMissingOwnerPolicy`
exposes DefaultValue/Suppress/Fallback; present null leaves remain present.
Bindings preserve strict full-path membership, complete custom states,
source-thread callbacks, replacement replay and owned failure cleanup.
`ValidationAsync`, `ValidationCollection`, `ValidationRowLease` and explicit
scheduling adapters supply async/collection ownership policies; generation
cannot infer network cancellation or application admission.

Completion requires code, permanent positive/negative and behavior tests, and
applicable actual-package managed/full-trim/native execution on Linux/Windows x64.
The .NET 10 `IsAotCompatible` flag is source-landed; final flagged-package audit
and host acceptance remain pending. Accurate Unsafe warnings remain. See the
[migration recipes](examples/generated-validation.md),
[capability ledger](generated-capabilities-progress.md) and
[NativeAOT evidence](aot-and-generators.md). Earlier green gates certify only
their exact source/package bytes.

## Historical annotation proof

The retained [managed annotation proof](../investigations/BindingGenerators/evidence/interceptor-annotation-proof.txt)
at SDK 10.0.401/C#14 and ILLink 10.0.12 intercepts a synthetic annotated call with
an unannotated safe body, executes `safe-interceptor`, and still reports IL2026
and IL3050 at the original call. Its warning severity was lowered for inspection;
no native binary was published. The [source bundle](../investigations/BindingGenerators/evidence/interceptor-annotation-proof-sources.json)
remains immutable. This explains the explicit Unsafe annotation boundary:
interception alone cannot erase warnings on an annotated public entry point.
The older deferred partial-method recommendation is superseded, while its dated
rationale and evidence remain historical.

Roslyn's [interceptor design](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md)
and [generator pipeline](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
explain original-arity/location and same-input visibility constraints. The
[historical Binding output](../investigations/BindingGenerators/evidence/Primitives/WhenAnyValueDispatch.g.cs.txt)
and [released OAPH IL](../investigations/BindingGenerators/evidence/released-oaph-il.txt)
remain proof for their dated cohorts, not the expanded Validation package.
