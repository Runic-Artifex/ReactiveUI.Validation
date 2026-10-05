# Generated validation and explicit Unsafe APIs

Implementation decision **2026-10-06**, implemented and verified at tested source
`2550376b230ccfb78beb8d3b4ced8866b65d809e`. The change remains **UNRELEASED**
until publication. This replaces the earlier recommendation to defer a Validation
generator and prefer attributes on partial methods. The primary surface preserves
supported inline-lambda calls. Local strict package gates and all four
[Linux/Windows CI jobs](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37386897109)
pass; the [verification record](upstream/reviews/2026-10-implementation.md#unreleased-generated-api-follow-up)
separates tested source and local/CI package identities from this later
documentation record and immutable historical releases.

## Normal calls require generation

Four predicate `ValidationRule` overloads (default/explicit context and
constant/delegate message) and the six `BindValidation`, four
`BindValidationContext` and four `BindValidationState` expression overloads
have normal generated entry points. Their bodies throw if a call was not
intercepted. They have matching explicit `Unsafe` methods containing the
reflection-based implementation with its `RequiresUnreferencedCode` warnings.
There is no hidden Unsafe fallback. Supplied-observable and metadata-only rule
overloads, plus the explicit observable APIs, retain their runtime contract.

This is a binary behavior change. An already compiled caller cannot gain an
interceptor when its referenced package is replaced: recompile supported call
sites with the matching core package's analyzer/build assets, or rebuild calls
against the explicit Unsafe API. Disabling analyzers, stripping package build
assets or calling the normal method indirectly does not select runtime
reflection.

The same analyzer DLL is embedded in both branded core packages under
`analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll`.
`buildTransitive/Runic.ReactiveUI.Validation.props` and
`buildTransitive/Runic.ReactiveUI.Validation.Reactive.props` respectively add
`ReactiveUI.Validation.Generated` to `InterceptorsNamespaces`. No separate
Validation generator package is required. The supported compiler cohort is SDK
10.0.401, C#14 and Roslyn 5.9.0. Use exactly one matching
Validation/ReactiveUI/ DynamicData flavor. Primitives uses
`ReactiveUI.Validation.*` and `DynamicData`; Reactive uses
`ReactiveUI.Validation.Reactive.*`, `DynamicData.Reactive` and the
System.Reactive scheduler/observable cohort.

## Observable extension compatibility correction

The locked SDK's actual compiler is Roslyn 5.9.0
(`5.9.0-1.26423.113`). Generator/driver references and the analyzer asset path are
aligned with that compiler, replacing the initial 5.0 tooling assumption from
older investigation material. Historical proof inputs retain their dated identity.

The same compiler reports CS8714 for certain nullable-generic calls through
C#14's synthesized static bridge for the six safe observable methods. The
observable rule and binding APIs therefore use traditional `this` extension
methods. Their CLR signatures and runtime semantics remain compatible, and
ordinary calls with inferred type arguments are unchanged. Explicit generic
source binding syntax changes from
`sources.BindObservableValidationState<TOut>(...)` to
`sources.BindObservableValidationState<TSource, TOut>(...)`; the property-state
binding requires the same two explicit type arguments. Prefer inference when
possible. `AddObservableRule<TValue>(...)` keeps its single explicit type
argument.

The narrowly scoped SST1703 exception in those two observable extension files is
justified by the reproduced compiler bridge error. It does not suppress trimming
or NativeAOT diagnostics or introduce tuple-based workarounds. Both flavor API
baselines track this correction. The final core gate passes 683 tests (323 per
library flavor plus 37 compiler-fixture tests); the 14 generated-runtime
infrastructure tests per flavor are included in that library count. Builds and
emitted source have zero warnings/errors at the tested source. Identical
direct-static nullable caller sources change from eight CS8714 errors before
correction to zero warnings/errors afterward; inferred calls already passed.
Proof/source/log inputs are retained under
`artifacts/verification/generator-nullability`, including `results.json`. Final
clean-source local and Linux/Windows core/package/native CI gates pass as recorded
in the [structured evidence](upstream/evidence/generated-api-implementation.json).

## Selector and assignment support

Selectors must be inline simple/property-chain lambdas over user-declared,
readable instance properties accessible to the emitted code. A notification
source requires reference-type owners implementing `INotifyPropertyChanged` at
every observed chain segment. Generated code attaches notifications, reads
direct getters initially and on matching or empty property-name notifications,
switches nested subscriptions on replacement, and detaches on disposal. It
emits direct runtime operations, never fresh `WhenAnyValue` calls for Binding
to intercept later. Metadata-only property selectors need a readable full path
but no notification source/getter execution.

A null intermediate owner emits `default` for the selected value. Reference
and nullable values therefore emit null; nonnullable value types emit their
default value. Predicates and message delegates must define what that value
means. A missing address does not retain the previous postcode. This fixed
rule-source policy is distinct from a null binding model/helper/context, whose
aggregate projection is valid and property projection is empty.

A binding view's static type must be a reference type implementing
`INotifyPropertyChanged`. Concrete struct view receivers produce `RUVG006`:
boxing the notification owner and capturing setter copies would lose stable
ownership/mutation. A stable boxed view referenced through a supported notifying
view interface remains valid. Both shapes have compiler regressions in both
flavors; this reference-ownership guard is recorded in final source `2550376`.

Writable accessible view properties support typed output, including bool,
enums and nullable custom structs; text targets must accept a string. Nested
target chains require reference-type intermediate parents and
`INotifyPropertyChanged` on observed owners; the final property has an
accessible ordinary non-init setter. Parent replacement replays the latest
projected output to the new target. A null target skips assignment while
retaining the latest value for a later replacement; model/rule changes
continue to update that cached value. A leaf owner that is only assigned need
not notify. For unsupported target shapes, use a callback with an explicit
application-owned target replacement stream, or explicitly choose Unsafe.

Private/inaccessible or open generic model/view/output call types are outside
the initial scope. A call inside a private or generic containing host can
still work when every type named by the generated call is accessible and
closed. Internal types in the same consumer assembly are supported. No hosted
partial implementation or attribute API is shipped. Members that exist only in
another generator's output are not automatically discovered from the original
compilation.

| Diagnostic | Meaning and action |
| --- | --- |
| `RUVG001` (error) | Unsupported call or selector: stored expression, method/indexer/cast/computed selector, unsupported notification contract. Use a supported inline path, explicit observable API/callback, or deliberately choose Unsafe. |
| `RUVG002` (error) | Call types cannot be named by emitted code, including inaccessible/open generic types. Move the call to a supported accessible closed shape or use explicit observables/Unsafe. |
| `RUVG003` (error) | The compiler cannot provide an encoded interception location. Use the pinned compiler and inspect the actual analyzer/build configuration. |
| `RUVG004` (error) | The required runtime generation contract is missing. Use the matching core runtime package containing the generated observation support and safe observable APIs, with its bundled analyzer. |
| `RUVG005` (error) | A normal invocation still lacks generated interception in the final compilation. Retain analyzer/props, declare selectors in original source, or use explicit observables/Unsafe. This check also covers calls and members introduced by other generators. |
| `RUVG006` (error) | Unsupported binding receiver/selector/target: require a notifying reference-type view (a stable interface-typed box is supported), use an inline readable source path and an accessible ordinary setter with supported notifying reference parents, or an explicit callback/Unsafe. |
| `RUVG007` (error) | A normal method is referenced indirectly as a method group/delegate. Use a direct inline call, an explicit observable API, or an explicit Unsafe delegate. `nameof` references remain allowed. |

Unsafe calls and safe supplied-observable/metadata rule overloads are not
intercepted. Unsupported normal calls are build errors, not warnings that can
silently choose reflection. A final-compilation dispatch analyzer checks
resolved normal invocations after all generators run, including generated
source, using the compiler's interceptor lookup. Other generators' output and
indirect method references cannot silently bypass the normal generation
requirement. Removing or suppressing that analyzer does not add runtime
fallback: the normal body still throws. Generator diagnostics do not prove
native support; that requires actual-package publish and execution evidence.

## Runtime semantics and ownership

Generated rules register through `AddObservableRule`: full ordinal root paths,
initial values, preserved complete states and captured registration context.
`Address.Postcode` differs from `Postcode`. Empty metadata means a model-wide
rule; multi-property metadata belongs to one component and is not equivalent
to two separate rules. Exact-path matching applies in both modes; `strict:
true` additionally includes only a rule exclusively associated with that one
path. Invalid/empty/whitespace path segments remain rejected by the runtime
API.

Default context access resolves through `IValidatableViewModel.ValidationContext`,
including explicit interface implementations and private shadows. Selected
contexts are observed through `IValidationContext`, with
`IValidationComponent.ValidationStatusChange` dispatched through its interface,
so concrete subtype shadows do not alter the selected state contract.
A helper removes its component from the captured context and disposes that
component's subscriptions. It owns neither the context nor a supplied
observable object. Blocking and advisory contexts stay independent. Replacing
a model's context property does not transfer existing registrations. Advisory
state does not automatically become command admission or default
`HasErrors`/`GetErrors`.

Generated bindings follow the view's current model and the selected
helper/context, detaching old selections. Null aggregate selections emit
`ValidationState.Valid`; null/empty property selections emit an empty list.
Typed/context property bindings seed actual membership and wait for active
rules' own initial states, without inserting a synthetic valid state.
Generated normal property text bindings expose actual initial rule states
immediately and omit the legacy synthetic empty-list prelude for active rules.
Explicit Unsafe property callbacks retain that legacy prelude. This
intentional migration difference changes the initial emission sequence, not
current rule validity. Projection delegates see complete custom
`IValidationState` objects, including boxed structs and their
code/severity/revision; generation must not rebuild states from text or filter
metadata changes solely by validity/text. Ordinary aggregate context states
retain their aggregation contract.

Binding disposal detaches subscriptions, without disposing selected models,
helpers, contexts or rules. Callback/setter execution follows the source
thread; UI dispatch belongs at the presentation boundary. Domain
validity/membership remain synchronous on the serialized model owner. This
promises no arbitrary concurrent mutation.

Notification sources own event registrations and refresh the complete
descriptor chain through a serialized pending-refresh drain. They attach
before initial getter delivery, rebind after reentrant replacement/null,
reject stale generation notifications and detach all registrations on disposal
or initial getter/callback failure. Value observations use default equality;
model/helper/context and target reference selections use identity so an
equal-comparing replacement is followed.

Nested-target binding support separately installs pending owned slots for its
target and validation subscriptions before synchronous delivery. If reentry
replaces/nulls/disposes their owner, an obsolete returned handle is disposed
instead of overwriting the current selection. Initial failures clean up
installed subscriptions/registrations and retain the original error; if
cleanup also fails, its error is reported alongside the original. A
caller-supplied source that throws before returning a handle still owns its
own failure-cleanup contract.

Asynchronous requests, cancellation, row identity and runtime collection
membership remain application responsibilities. Safe observable rule
registration is the supported path for those runtime inputs. A generator
cannot infer request ownership from a literal property selector.

## Realistic consumers and acceptance

The [GeneratedValidation recipe](examples/generated-validation.md) and
[corpus](../examples/GeneratedValidation/Program.cs) cover initial
property/text updates, nested null/replacement, model/helper rich-state
replacement, independent contexts, property membership/strictness, nested
target handoff and row-owned observable results in both flavors. This is
separate from the immutable NativeValidation release corpus and its
caller-written adapters.

Required gates inspect emitted direct notification/getter/setter operations,
absence of Unsafe/expression calls, exact branded/flavor/version package
graphs, negative diagnostic IDs/locations and ungenerated normal-stub
failures. Preserve synchronous handoff/null/disposal/error cleanup, full
metadata/strictness, custom-state identity, typed nullable output, membership
and formatter behavior. The package gate verifies all 18 distinct normal
overload shapes, analyzer/props assets and the absence of Roslyn/compiler
runtime dependencies. Six isolated negative build configurations cover
stored/computed/indexed rule selectors, nonnotifying nested targets and rule
parents, and notifying struct chain owners, in each flavor. Packaged consumers
must publish with warning-as-error policy and execute under managed, full
trimming and actual NativeAOT on each claimed host. Record exact source,
package hashes and executed platform separately; do not reuse old release
results for changed generated code.

No blanket `IsAotCompatible`, all-reflection-removed, retained UI-platform or
bridge/browser claim follows. Explicit Unsafe APIs and some retained runtime
components remain reflection boundaries. Preserve producer analysis and their
accurate warnings. The released DynamicData 10.0.0-runic.5 and ReactiveUI
26.0.1 cohort remains pinned; no sibling source adoption is required.

## Historical annotation proof and superseded alternatives

The [managed compiler
proof](../investigations/BindingGenerators/evidence/interceptor-annotation-proof.txt)
at SDK 10.0.401/C#14 with ILLink 10.0.12 intercepted a synthetic
RDC/RUC-annotated `UnsafeApi.Read` with an unannotated method and executed
`safe-interceptor`. The original call still produced IL2026 and IL3050. The
proof changed warning severity to permit inspection, without suppression, and
published no native binary. It remains historical evidence explaining why
simply intercepting an annotated expression API does not remove caller
warnings.

The new split instead moves actual reflection to explicit annotated Unsafe
methods and leaves normal generated-only stubs unannotated. This changes the
public contract and requires both API baselines to be reviewed; it does not
erase the proof or alter its immutable source/evidence.

The prior future partial-method/attribute recommendation is superseded for
this implementation. Explicit observables remain a supported runtime surface;
a later attribute API would be a separate scoped feature. Inaccessible/open
generic call types, other generators' output and additional target shapes
require their own implementation/diagnostics/acceptance before being
advertised.

Roslyn's [interceptor
design](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md)
and [encoded location
API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpextensions.getinterceptablelocation?view=roslyn-dotnet-5.0.0)
underlie the call substitution. Its [generator pipeline
design](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
explains why generated Validation operations must not depend on another
generator processing newly emitted calls. The existing [Binding
output](../investigations/BindingGenerators/evidence/Primitives/WhenAnyValueDispatch.g.cs.txt)
and [released OAPH
IL](../investigations/BindingGenerators/evidence/released-oaph-il.txt) remain
dated evidence about their original cohort, not generated Validation support.

To reproduce only the historical managed annotation proof, extract the [source
bundle](../investigations/BindingGenerators/evidence/interceptor-annotation-proof-sources.json)
into a task-owned directory and reuse the locked environment:

```sh
direnv exec "$RUNIC_SDK" dotnet run --project "$PROOF_ROOT/Emitter/Emitter.csproj" \
  -- "$PROOF_ROOT/Consumer/Program.cs"
direnv exec "$RUNIC_SDK" dotnet run --project "$PROOF_ROOT/Consumer/Consumer.csproj"
```

The first command emits the encoded location, the second retains both warnings
and prints `safe-interceptor`. Keep the source/log and remove task-owned
temporary outputs after inspection. This reproduction is not a
generated-package gate.
