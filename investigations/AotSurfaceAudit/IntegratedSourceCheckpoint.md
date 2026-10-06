# Integrated .NET 10 source checkpoint

The core source at `22f6f15ea230689509118078f2a541deceeeb705` supports preparing
the two .NET 10 `IsAotCompatible` contracts for the fresh shipping gates. Normal
APIs dispatch through concrete typed capabilities, and explicit Unsafe APIs
retain warnings for real reflected operations. This is a source checkpoint;
the final package compatibility decision also requires current flagged producer
analysis and actual packed managed, full-trim and NativeAOT execution on the
required Linux and Windows hosts.

The [source manifest](evidence/2026-10-06-integrated-source-checkpoint.json)
records Git-object bytes for 98 shared core source files, both project contracts,
both complete public baselines and 33 generator source files. It excludes live
merge files and build outputs. Each baseline contains 422 explicit
public/protected declarations, 32 RUC declarations and zero RDC declarations.
The comparable declaration metric counts lines with explicit access; implicit
interface members remain present in the pinned complete baselines. The prior
core foundation at `0b0517cd5295b4d274de60639d7d706bea80e80d` contained 398
explicit declarations per flavor. Neither count is a type count or a count of
executed scenarios.

The [earlier report](README.md) and its `f960c4b` producer byte manifests remain
historical: 203 explicit declarations per flavor, the flag then unset and ten
focused formatter tests. Those results do not certify the expanded integration.

## Public routes and reachable operations

Every row applies to both namespace and scheduler flavors. The complete
baselines, including each overload and retained annotation, are identified by
their hashes in the manifest.

| Public API family | Source reachability and annotation decision |
| --- | --- |
| Validation text, state, comparers, component/context/binding/formatter interfaces | Typed values, strings, collections and interface dispatch. Application implementations and callbacks retain their own obligations. |
| `BasePropertyValidation<TViewModel>`, supplied-observable component constructors, structural metadata operations | Typed state/replay and expression-shape/member-name metadata. No reflected value access or expression execution. No blanket RUC/RDC. |
| Three retained expression constructors of `BasePropertyValidation<TViewModel,TViewModelProperty>` | RUC on the public constructors and internal constructor that actually calls `WhenAnyValueUnsafe`. New selector constructors use direct typed plans. |
| `SelectorValidation`, selectors, access plans, dependency adapters and observation options | Concrete getter/notification delegates; typed INPC/collection subscriptions; missing-owner policy and supplied equality. Metadata observation requires an explicit metadata-only reader; generated metadata dependencies are separate. No implicit leaf-reading fallback. |
| Normal rule/view/context/state extensions and normal `ValidationBinding.For*` factories | `ValidationRuntime` selects exact typed provider/attached-registry descriptors or throws an actionable missing-capability error. Known view/model/context access uses declared interfaces and typed notifications. No reflective fallback. |
| Explicit `ValidationRuleUnsafe`, `BindValidation*Unsafe` and `ValidationBinding.For*Unsafe` | RUC originates in real reflected source/model/helper observation. Expression targets also reach the RUC setter boundary. Action targets remove assignment reflection while source observation retains RUC. |
| `ValidationPlanRegistry`, requests, roles and expression-pattern/argument contracts | Bounded exact closed generic registrations; weak owner reference identity; metadata shape matching and typed payloads; ambiguity checked before descriptor factories. No scanning, runtime generic construction, value reflection or compilation. Supplied extractors must uphold their documented finite typed contract. |
| `ValidationTarget`, write plans, cells, lenses and storage receipts | Concrete setters/outward write-back delegates, structural slot identity, explicit write origins/revisions and supplied normalization equivalence. The runtime does not invent a setter or read a field through reflection. |
| `GeneratedValidationProperty`, `GeneratedValidationObservation`, `GeneratedValidationBindingSupport` | Typed owner access, direct INPC handlers, setter callbacks and complete-state formatting. Borrowed owners remain application-owned. |
| `ValidationContext`, `ValidationHelper` | Typed DynamicData list/query/delegate-refresh pipelines and generated typed OAPH routes. Fresh producer IL must reconfirm the OAPH calls for the final bytes; the earlier DLL inspection is not reused as that proof. |
| `ReactiveValidationObject`, including the virtual error hook | Typed context membership, complete state/text export, ordinal property metadata and direct virtual/event notification. No platform member discovery. |
| `SingleLineFormatter`, formatter resolver and registration helpers | Direct construction or explicitly supplied instance/factory under the exact closed `IValidationTextFormatter<string>` service contract. Real Splat fallback selects the last non-null service or the default formatter. No implementation scanning or activation. |
| Async/scheduling/output/collection/row/view adapters | Typed task, cancellation, scheduler, observable, key selector, state projector and host-provider delegates. Owned observable/row/provider registrations are explicit. Additional dependency routes are described below. |
| Snapshot reader/validator contracts | Synchronous scoped typed borrowing, including `allows ref struct`; no subscription or runtime member discovery. |

The shared core contains no expression compilation/interpreter, dynamic emit,
`MakeGenericMethod`, `MakeGenericType`, `Activator`, assembly scanning,
`DynamicDependency`, broad `DynamicallyAccessedMembers` roots, or trim/AOT
diagnostic suppression. Style, lifetime and owner-thread analyzer suppressions
are not trim/AOT suppression. Existing RUC annotations delimit unsupported
operations; they do not make those operations safe.

## Precise Unsafe and dependency boundaries

`ValidationBinding.BindToView` now explicitly describes expression-based target
observation and property assignment. Its actual operations remain
`Reflection.Rewrite`, `GetValueSetterOrThrow` and, for nested targets, dynamic
parent observation. `ValidationStateBinding.BindToView` describes actual
reflected assignment. Private reflected observation helpers retain RUC. Public
normal factories do not call these methods.

The pinned Binding `9.1.0` setter uses `PropertyInfo.SetValue` or
`FieldInfo.SetValue`; the prior actual DLL inspection confirms those operations
for both flavors. That path has no expression compile, emit or constructed
generic operation, so adding RDC would misstate the boundary. The fresh
setter-only negative fixture must report IL2026 and reject a false IL3050.
It is separate from the immutable historical `.5` diagnostic investigation.

The Splat `21.0.0`, Binding `9.1.0`, ReactiveUI `26.0.1` and matching
Runic.DynamicData `.30` source/operation findings are retained in the prior
dependency table. Fresh shipping verification must identify and execute its
actual resolved package cohort. Custom resolvers, providers, callbacks and
schedulers remain explicit application contracts.

The adapter additions reach `Signal.FromAsync` and `ObserveOnSafe` from
ReactiveUI.Primitives `9.0.0`, pinned by the archive nuspec to source
`56523df08d686aba128e0abef660a855fd7951f3`. The new manifest records the exact
selected primary-source hashes. `FromAsync` constructs typed
`FromAsyncSignal<T>`/`FromAsyncSubscription<T>` state and typed task continuations;
cancellation owns concrete token sources. `ObserveOnSafe` constructs
`ObserveOnObservable<T>`/`ScheduledDrainState<T>` and calls the supplied typed
scheduler. The same shared source compiles against the declared scheduler type
for each flavor. Neither selected operation discovers members or compiles an
expression. An unrelated expression-based property-change helper in the same
dependency source does compile an expression; these selected methods do not
call it. The normal scheduler extension constructs a typed delegate work item
and calls `ISequencer.Schedule`; the Reactive route calls its declared
`IScheduler.Schedule` contract.

The extended framework-only inspector built with warnings as errors in 1.87
seconds, with zero warnings/errors. Its read-only inspection selected 102
method bodies from each actual pinned Primitives DLL. The emitted operands
confirm these typed factories, task continuations and scheduler contracts; the
selected type/method bodies declare no RUC/RDC or root/suppression attributes.
Both dependency DLLs declare `IsAotCompatible=True` and `IsTrimmable=True`.
The manifest pins their exact DLL hashes, the inspector source and retained
build/dump hashes. This verifies the selected cached dependency bytes; it does
not execute those DLLs or substitute for fresh current product/package gates.

The current access emitter uses selected static `UnsafeAccessor` signatures,
full compile-time assembly identities and generic VAR positions. Marker
authorization checks the exact runtime assembly identity. Unsupported erased
value/byref signatures are rejected. These are static generated signatures,
not runtime `Type.GetType` lookup or reflection roots. Actual managed bridge
tests are separate evidence; fresh generated-corpus trim/native execution must
prove the final emitted signatures and bytes.

## Compatibility contract and completion gates

Both core project files enable `IsAotCompatible` only when the inner
`TargetFramework` is `net10.0`; existing strict AOT/trim analyzers remain enabled.
No platform, generator or historical project compatibility metadata is added by
this change. Accurate annotated Unsafe APIs are compatible with the package
contract; their presence alone is not a reason to leave it unset.

Before recording the final shipping decision, the root integration must pin
the completed source (including pending origin/epoch followups), run strict
flagged producers and verify both actual package bytes through the complete
managed/full-trim/native corpus on the required hosts. Public baselines,
generated semantic/byte inventories, assembly compatibility metadata, dependency
graphs, precise negative warnings and retained execution reports must refer to
that same completed cohort. Any further product/generator or dependency change
requires its affected source/IL boundary to be rechecked. This checkpoint adds
no current package or host execution claim.
