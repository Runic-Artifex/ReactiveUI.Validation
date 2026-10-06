# Completing generated validation and NativeAOT capabilities

Plan dated **2026-10-06**, based on clean source
`0029213f55a49f147b472c1c8037a75ab606b548`. The user requested a written plan
covering all previously identified functional/API/NativeAOT limitations, followed
by implementation. Every mandatory capability below needs implemented code and
meaningful verification. Difficulty, elapsed time and an earlier narrow MVP are
not reasons to leave a row deferred. This document is a plan, not evidence that
the new work has passed.

Keep the supported **.NET 10 / SDK 10.0.401 / Roslyn 5.9** core, ReactiveUI
**26.0.1**, and released Runic.DynamicData **10.0.0-runic.30** pair. Preserve both
branded packages, namespace/scheduler flavors, MIT attribution and immutable
releases. This work does not request .NET 11, a dependency upgrade, new release
publication, or unrelated mobile/desktop/RID support. Provider and XAML
integration are library capabilities to implement; their tests must distinguish
compiler/typed integration from actual UI and native-host evidence.

The [current generated contract](generated-validation-design.md),
[NativeAOT audit](aot-and-generators.md),
[member-interoperability investigation](generated-member-interop.md),
[migration recipe](examples/generated-validation.md) and
[implementation ledger](upstream/implementation-2026-10.md) identify the starting
limitations. Earlier released `.5` dependency / `.15.10` Validation and
`9ad0d6a` investigation results remain historical. Repeat acceptance against the
current `.30` cohort; do not relabel old package bytes as new verification.

## Completion contract

A restriction has three possible outcomes, each requiring executable proof:

- Extend ordinary supported syntax or generated behavior while retaining the
  existing API, when the language and compiler permit it.
- Implement an explicit typed capability for cases needing an application
  contract: notification providers, stable ownership, runtime descriptor
  registration, immutable replacement, or request cancellation.
- Where the requested automatic behavior is impossible, implement that usable
  alternative and explain the precise constraint. A diagnostic alone does not
  complete the capability.

For example, a silent field mutation supplies no event to observe. A snapshot or
explicit invalidation provider supplies the missing contract. Already compiled
IL cannot acquire a new call-site interceptor, but an explicit registered
compatibility dispatcher can execute a precompiled call without runtime code
generation. An immutable target needs replacement rather than an ordinary
post-construction setter. An existing init setter may have a typed compatibility
bridge, which must be explicitly selected and proven. NativeAOT cannot invent arbitrary executable code or
generic instantiations at runtime; a finite compiled descriptor catalog covers
known operations and rejects unregistered operations explicitly.

Already supported behavior is a preservation task, not a reason to rewrite
working implementation. Keep precise warnings on unrestricted Unsafe APIs and
never hide reflection behind an unsupported normal call.

## Architecture and implementation order

### 1. Correct semantic classification and freeze the capability baseline

Inventory all four predicate-rule and fourteen expression-binding overloads,
supplied-observable/metadata overloads, direct component constructors and
observable APIs in both public baselines. Add permanent reproductions for the
safe metadata false positive and every newly supported shape before changing
selection, including all eleven public static `ValidationBinding.For*` methods.
Classify actual overload applicability rather than accepting any
normal candidate during incomplete semantic binding. Retain the final dispatch
analyzer and actual compiler interceptor lookup, extending its contract to
recognize explicit typed runtime dispatch/delegate factories. Do not silence
missing-interception diagnostics to make a registered route compile.

Record existing behavior separately from capabilities genuinely lost during
migration. Legacy Binding strips ordinary `Convert` nodes, rejects checked
conversions/ordinary calls, and supports only constant index arguments;
Validation's dotted path extraction can lose conversion/index metadata.
Legacy targets have no outward struct write-back. Extending these cases must
distinguish existing constant-index/field access from previously broken or
unsupported behavior. Initial
text emission and null intermediate policy have real intentional differences;
provide explicit compatibility options without silently changing the modern
defaults.

### 2. Use one typed capability model

Represent access, observation and assignment separately. A selector descriptor
contains typed getters, dependency identities/full rule paths, null policy,
notification/provider adapters and, where applicable, a setter or immutable
replacement callback. A runtime selection can choose among descriptors and
provide index arguments/captured values without discovering members through
reflection or compiling expression trees. Use a structural validation-path
identity for index arguments/conversions rather than ambiguous dotted text;
retain an explicit legacy path mode where compatibility needs it.

Use the same descriptor semantics for inline generation, explicitly supplied
selectors/delegates, compiled model assemblies and runtime catalogs. The public
API must permit callers to construct descriptors in their own legal access and
generic context. The eventual API names and signatures require both baseline
reviews; illustrative terms in this plan are not shipped names.

Introduce a finite, typed registration/catalog route for stored expression
variables, selector parameters/factories, delegate calls and precompiled peers.
Resolve registered expression member/operation identity to compiled descriptors;
handle runtime argument values separately from the compiled operation set.
Registration must be explicit, deterministic and bounded, with ownership,
duplicate/mismatch and missing-registration behavior. Prefer explicit owner
scopes; any ABI-required process-wide registration uses an explicit disposable
lease, with deterministic ambiguity rejection and no accidental overwrite.
Expression-instance registration with a supplied/generated plan preserves
runtime closure identity. Precompiled callers ordinarily construct fresh
expression instances which the application cannot register individually, so
also match structural member/operation identity against predeclared compiled
operations. Bind the current receiver and registered capture/argument values on
each invocation; do not reuse a stale receiver/closure. A private compiler-generated
display-class field is not automatically readable. Unknown capture access
reports a deterministic missing capability and uses the implemented supplied
typed extractor/factory route; it is not an implicit expression interpreter.
No ambient scan,
`Expression.Compile`, arbitrary `MakeGenericType`, hidden Unsafe fallback or
unbounded expression-object cache is permitted on the warning-free route.

For precompiled normal callers, implement an explicit compatibility entry/mode
that dispatches the preserved CLR signature through registered descriptors.
For native acceptance, link the unchanged precompiled consumer IL into a fresh
native application with the new dispatcher and registrations. An already built
native executable cannot gain new managed DLL behavior by swapping a package.
Ungenerated, unregistered normal calls continue to fail actionably. Registration
must be a deliberate application choice; it must not change an ordinary missing
interceptor into reflection. Preserve the existing explicit Unsafe route for
unrestricted managed expressions and accurately explain its trimming boundary.

### 3. Make producer contracts visible without ordering generators

Build a versioned producer descriptor extractor/private semantic compilation for
the pinned SourceGenerators 4.2.0 and Binding 9.1.0 contracts. Cover Reactive
fields/properties, generated notification interfaces, commands, collections,
derived lists, OAPH and recognized XAML declarations. Preserve original tree
identity and interception locations; **never emit predicted declaration trees**.
Validate actual producer output, accessible member types/accessors and final
interception, rejecting prediction drift before claiming support.

Binding's internal projection is a reference, not a public API to invoke or a
complete 4.2 contract. Reused code needs MIT attribution and pinned provenance;
implement missing fidelity rather than copy its old assumptions. Do not import
unrelated platform plugins merely to obtain property names. Ordinary generator
output ordering and co-bundling do not solve visibility. Fixed post-init
attributes are visible; Roslyn's early-output API cannot consume syntax or
semantic inputs for this attribute-based pipeline.

The retained prototype's inner Validation driver establishes narrow feasibility
only. Production integration needs incremental invalidation, deterministic
output, cancellation and full-contract tests. Newly emitted peer calls require
an owned integration/descriptor contract; final analysis alone cannot generate
their bodies afterward.

### 4. Separate legal access from interceptor placement

Accessible closed generic types already work. Extend open generic method/type
contexts, nested/private/protected/file-local access and anonymous output
shapes through legal typed bridges and descriptor factories.

Roslyn permits generic interceptor methods only under its original method-arity
contract; an interceptor cannot be placed inside a generic containing type.
Existing `TModel` cannot simply be decomposed into an extra `T` for `Model<T>`.
Do not implement generic support by emitting an illegal generic partial host.
Put typed accessor/factory bridges in a legal caller/model context and have a
legal interceptor or explicit typed API consume them. Preserve constraints and
nullable annotations. Use lexical partial hosting for inaccessible symbols where
possible; a nonpartial/external host needs an explicit legal accessor or .NET 10
accessor bridge with its own static proof. The descriptor route also covers
cases for which the compiler cannot intercept directly. A global interceptor can
forward through a per-site generated interface/adapter whose payload types are
the original API generic parameters. A public property-bearing constraint can
also permit direct generic access after constraint proof. Generated callable
factories have real bodies for method groups and compiled-peer reuse.

`.NET 10 UnsafeAccessorType` handles inaccessible reference signatures only,
excluding inaccessible value-type owners and inaccessible byref return/field
value types. An inaccessible reference owner can use a field accessor whose
byref field value type is directly nameable. UnsafeAccessor
also requires exact declaring types and type-versus-method generic positions and
constraints. Private structs and open hosted types therefore need typed lexical
bridges or caller factories, not an erased-accessor claim.

### 5. Extend selector and observation semantics

Analyze operations and dependencies rather than only member-access syntax.
Support direct fields, legal casts/conversions, computed expressions and indexers
with typed getters/setters. Track changing argument/root dependencies, collection
and item events, and replacement/null transitions. Opaque method effects need
explicit dependencies or a supplied observable; a method call is not itself a
notification mechanism.

Add typed notification-provider and snapshot/invalidation contracts for
non-INPC and platform objects. Specify provider selection, duplicate filtering,
initial delivery and null behavior. Handle structs through stable boxes,
ref/storage access or write-back into an owned root, not subscriptions and
assignment to temporary copies. A reference-owned struct container or typed
getter/write-back/invalidation lens mutates the current value and writes it back
through every enclosing writable segment. Acquire any ref only during the
synchronous setter. Existing by-value view APIs cannot mutate a caller's stack
local after returning; a long-lived subscription cannot capture a ref struct.
Provide synchronous validation/snapshot and stable-storage alternatives.

For existing init setters, investigate an explicit typed accessor compatibility
mode and prove its modreq signature under the locked SDK, trimming and NativeAOT.
Readonly fields require their own exact bridge proof; do not infer private
backing storage for a getter-only property. An immutable replacement callback
remains the usable alternative. Target casts require an explicit writable lens;
never invent the inverse of an arbitrary conversion.

### 6. Complete runtime and compatibility behavior

Reuse the existing observable registration/binding infrastructure, captured
context ownership and direct getters/setters. Add typed direct-component
construction, caller-owned async/collection adapters and the originally deferred
virtual error-delivery hook. Supply explicit modern/legacy null and initial
emission options and SDK-compatible generic binding conveniences. Do not replace
working OAPH, context abstraction, formatter fallback or domain scheduling with
obsolete upstream proposals.

Keep domain membership/current validity synchronous on the serialized owner;
UI/provider presentation dispatch is explicit. Request identity, cancellation,
stale-result rejection, collection source replacement and row ownership are
configurable contracts, not policies inferred from a selector.

### 7. Audit, package and prove the completed surface

Review both API baselines, annotations, generated dispatch and public dependency
reachability. Evaluate `IsAotCompatible` as a concrete audited package contract;
annotated Unsafe APIs can remain. Verify each advertised safe route independently
with actual packaged consumers, including direct constructors and registered
runtime/precompiled dispatch. Audit formatter/DI and provider paths rather than
inferring their safety from annotation boundaries.

Run focused checks per stage, then one exact-source Linux/Windows core and
package matrix. Exercise managed, full-trim managed and actual NativeAOT on both
supported x64 hosts, using the same accepted shipping package pair. Verify
package/analyzer/build asset bytes and restored graphs. Keep UI compiler/provider
integration evidence separate from unexecuted UI-native or additional-RID claims.
These stages order the work; none is an optional stopping point.

## Detailed capability register

All rows start **planned**, except rows labeled **preserve**. A row completes only
with code, permanent positive/negative regressions and the applicable package
execution evidence. Related rows can share implementation but cannot disappear
from the completion record.

### Compiler, types and access

| ID / starting point | Implementation or preserved contract | Required acceptance |
| --- | --- | --- |
| C01 — early safe-metadata `RUVG001` false positive | Applicability-aware classification and projected rebinding; safe observable/metadata overloads never require interception. | Real generated `SubmitCommand` metadata call passes; declared control passes; genuine unsupported normal call still receives final missing-dispatch error, both flavors. |
| C02 — open method/type parameters rejected | Legal generic typed bridges/descriptors and interceptor signatures, with original arity and constraints. | Open `Model<T>`, method `T`, nested generic owner, nullable/class/struct/interface constraints, direct-static and receiver syntax; actual native closed instantiations. |
| C03 — inaccessible/private nested types and members | Lexical access bridges where legal; explicit typed accessors or proven selected storage bridges otherwise. Prove protected setter legality through the actual receiver type, independently of readable getter access. | Private/protected/internal/private-protected getters/setters, nested containing declarations, nonpartial and external peers; legal derived receivers versus illegal base-cast writes, with an authored legal typed setter/registered route. |
| C04 — anonymous/file-local result shapes | Typed inference/factory bridge without naming an inaccessible type from a generated namespace. | Anonymous rich projection, file-local host/output, metadata preservation and compile-time access errors; no invented public surrogate losing state. |
| C05 — readonly/init-only target restriction | Ordinary setter when legal; explicitly selected existing init/readonly accessor bridge where proven; immutable construction/replacement callback otherwise. | Init modreq/readonly storage compiler+trim+native proof, initial construction/replacement and null-target replay; never infer getter-only backing storage or mutate temporary copies. |
| C06 — preserve closed accessible generic and internal types | Retain existing namespace generation and exact type/nullable contracts, including effective property/field/getter-return MaybeNull/NotNull flow attributes on typed accessor bridges. | Closed generic nested models/targets and same-assembly internal types remain accepted; ordinary-read versus exact-bridge flow controls for reference and Nullable<T> values must compile meaningfully without warnings or invented null guarantees. |

Source: [generator type/selector checks](../src/ReactiveUI.Validation.SourceGenerators/GeneratorHelpers.cs),
[binding emitter](../src/ReactiveUI.Validation.SourceGenerators/BindingEmitter.cs),
[current type limits](generated-validation-design.md#selector-and-assignment-support),
and [Binding audit](../investigations/GeneratorInterop/evidence/source-generator-contracts.txt).

### Selectors, runtime dispatch and ownership of storage

| ID / starting point | Implementation or preserved contract | Required acceptance |
| --- | --- | --- |
| S01 — casts/conversions rejected | Semantic typed conversion and dependency graph; explicit writable lens for target conversion. Resolve effective MaybeNull/NotNull getter output separately from AllowNull/DisallowNull setter input on ordinary and bridged accessors. | Interface/base casts, boxing/unboxing, checked/nullable/user conversions; updates and conversion failures, no invented inverse target conversion. Both flavors need meaningful read-postcondition and permitted/forbidden setter-input controls for references and Nullable<T>, without deriving assignment solely from getter annotations. |
| S02 — fields rejected | Typed field read/write descriptors plus supplied notification/invalidation policy. | Field update with explicit events, null/replacement, readonly field snapshot; no claim to see silent writes. |
| S03 — computed/captured/method expressions rejected | Typed computation with declared dependencies/observables, runtime captured values and root replacement. | Multi-property calculation, changing captured root, opaque method explicit dependency, getter exceptions and stale-source detachment. |
| S04 — indexers rejected | Typed index/key argument descriptors, collection/item providers and writable indexer assignments; preserve optional enum/nullable-enum default conversion nodes and exact argument order. Locked C#14 rejects reordered named arguments in expression trees (CS9307); implement the legal typed Func/access-plan alternative instead of promising interception of invalid original source. | Original optional/default/params admission plus exact typed enum/nullable-enum emission; reordered named expression-tree negative and executable typed delegate with SF evaluation/slot keys. Also item/collection events, live arguments, absence/bounds, replace/null, replay and structural metadata. |
| S05 — selector variables/parameters/factories rejected | Recover finite provenance only when actual dispatch/storage is fixed. Detect implicit in-parameter local escapes as well as explicit ref syntax. Instantiate constructed generic definitions preserving operators, conversions, checked/lifted/reference-equality semantics, original nameof(T) constants and finite block returns. Polymorphic static factories/rewritten readonly fields use typed catalogs; registered matches override compiled finite fallback. | Inline/local/factory equivalence, static interface overrides, readonly constructor writes/byref and omitted-in escapes; finite generic selector/property/block-factory substitution. Registered override wins; only genuine absence may use known generated fallback, never ambiguity/mismatch/factory failure. Live captures, bounded catalog and actual native execution remain required. |
| S06 — indirect normal delegates rejected | Callable typed selector/operation factories and explicit registered runtime dispatch. | Method-group/delegate invocation, generic factories, no call-site interceptor assumption, disposal and native execution; Unsafe delegate warnings preserved. |
| S07 — precompiled normal binaries throw | Deliberate structurally registered compatibility mode preserving CLR entry signatures and current receiver/capture values; explicit scoped registration/leases and ambiguity rejection. | Old binary creates fresh same-shape expressions with different receiver/capture values without exposing them to registration; registered/unregistered, unknown closure and competing-registration paths execute in managed/fresh native host without hidden reflection or frozen first closure. |
| S08 — non-INPC/platform/provider sources excluded | Typed observation-provider precedence, snapshot and manual invalidation modes. | Reactive/INPC/custom provider affinity, POCO snapshots, explicit invalidation, provider error/completion/disposal, replace/null owner. |
| S09 — concrete struct views/chain owners excluded | Stable box/ref/storage descriptors and explicit struct write-back or root invalidation. | Actual owner mutation versus copy, nested struct write-back, stable interface box, identity/replacement; reject impossible unowned-copy observation. |
| S10 — generated calls emitted by peers lack interception | Explicit producer integration/runtime descriptor contract for generated callers; final dispatch validation retained. | A peer emits a normal call and a typed registered call; implemented route executes, unsupported unregistered call fails; no ordering dependence. |
| S11 — ref-struct/stack-local lifetime constraint | Synchronous typed validation/snapshot and stable reference-owned storage route; no long-lived borrowed-ref subscription. | Ref-struct synchronous positive, lifetime escape negative, by-value stack mutation distinction and stable owned struct write-back. |

Source: [rule selector emitter](../src/ReactiveUI.Validation.SourceGenerators/RuleEmitter.cs),
[call classification](../src/ReactiveUI.Validation.SourceGenerators/ValidationGenerator.cs),
[final dispatch analyzer](../src/ReactiveUI.Validation.SourceGenerators/ValidationDispatchAnalyzer.cs),
[observation runtime](../src/ReactiveUI.Validation/Extensions/GeneratedValidationObservation.cs)
and [migration boundaries](examples/generated-validation.md#runtime-selectors-and-precompiled-consumers).

### Actual producer and XAML contracts

| ID / starting point | Implementation or preserved contract | Required acceptance |
| --- | --- | --- |
| P01 — Reactive field-produced properties invisible | Full pinned declaration prediction with naming, nullability/access/init/required/inheritance/containing fidelity; never emit it. | Actual SG4.2 fields/prefixes/collisions/override/generics, normal rule/helper/context/target positions, both flavors and producer/adapter order permutations. |
| P02 — generated notification interfaces invisible | Predict exact `IReactiveObject`/INPC producer contract and generic applicability; final validate actual implementation. | Generated/inherited notification class, explicit interfaces and partial property; original contract absent/present controls and actual event lifetime. |
| P03 — generated commands invisible | Exact synchronous/Task/observable input/output/naming/token/flavor contracts; producer validity diagnostics retained. | Real commands and metadata selectors, CanExecute from explicit blocking state, command async disposal/cancellation; invalid signatures and ValueTask distinction. |
| P04 — generated collections/derived lists invisible | Exact ReactiveCollection/BindableDerivedList member contracts integrated into descriptors. | Actual producer members, changing items, same-key and whole-source replacement, null and removal cleanup; no invented collection ownership. |
| P05 — preserve declared partial properties and Binding OAPH | Declared contracts remain usable; OAPH/helper generation belongs to Binding with explicit initialization/lifetime. | Actual `[Reactive]` partial setter, partial get-only OAPH and `ToProperty`, nullable initial/change, helper disposal and both flavors. |
| P06 — XAML-produced controls and provider capability | Recognized MAUI/Avalonia AdditionalTexts name/type contracts with typed access/provider/assignment adapters and final member validation. | Real pinned generator/XAML inputs and typed integration fixture, malformed/missing controls and drift errors; report compiler/provider proof separately from actual UI/native-host proof. |
| P07 — projection/ordering/version drift | Production incremental extraction, cancellation/invalidation, original location identity and final actual-output validation. | No fake tree in output, deterministic rerun, changed/removed attribute/input, exact producer version, final member mismatch and peer-order permutation. |

Source: [interop evidence and constraints](generated-member-interop.md),
[pinned extractor/producer audit](../investigations/GeneratorInterop/evidence/source-generator-contracts.txt),
and [prototype limits](../investigations/GeneratorInterop/ProjectionProof/README.md#limits).
The four managed prototype runs are not proof of these complete production rows.

### Runtime behavior and legacy compatibility

| ID / starting point | Implementation or preserved contract | Required acceptance |
| --- | --- | --- |
| R01 — preserve rule metadata and complete states | Full paths, exact/strict multi-property membership, model-wide/entity metadata; explicit Legacy metadata opts into full ordinal display-name membership while structural/structural paths preserve physical/index/conversion identities. No text-only state reconstruction. | One component with several paths, strict exclusivity, add/remove/clear, legacy field-path compatibility, same-display distinct structural identities, custom class/boxed struct code/severity/revision and property/entity error export. |
| R02 — preserve context policy/interface dispatch | Default and selected interface contracts, independent blocking/advisory contexts, captured registration ownership. | Explicit implementation/shadow controls, context/model replace/null, helper removal from original context, no automatic advisory command/error aggregation. |
| R03 — deliberate null policy differences | Explicit default-value, legacy missing-parent suppression and caller fallback policies; actual null leaf still delivers. | Null reference/nullable/nonnullable leaf, disappear/reappear parent, predicate/message inputs and exact policy traces; target null skips/caches. |
| R04 — deliberate initial emission differences | Explicit actual-state versus legacy presentation-prelude option, modern default retained. | Legacy empty prelude on nonnull model selection and synthetic valid membership seeds versus current raw-state behavior; initially invalid/late rules, formatter/action/typed traces, no fabricated command state. |
| R05 — preserve target handoff and typed outputs | Latest output replay, null-target retention/reference identity and exact setter input independently of getter reads. Refresh target/dependency owners through the same per-binding engine on every new domain output; silent replacement does not imply an event or immediate replay. | Bool/enum/nullable custom struct and nullable setter controls; equal-overriding replace/null, notified immediate replay versus silent replacement followed on next output, no old-output replay during refresh, independent AfterRead caches, reentry/failure/disposal. |
| R06 — preserve reentry/error/disposal ownership | Pending refresh/subscription handoff, stale callback rejection, initial-failure rollback and original/cleanup error preservation. | Reentrant replacement/removal/disposal, getter/callback/provider throws, cleanup throws, completion/error, repeated disposal and borrowed source/model/context not disposed. |
| R07 — asynchronous/collection lifetime needs explicit policy | Reusable owned adapters with request cancellation/version rejection, stable row identity and outer source switching. | Initial empty/add/edit/remove/clear, same-key/whole-source replacement, late result after removal, cancellation, pending/blocking/advisory policy and owned helper cleanup. |
| R08 — preserve scheduling and add explicit presentation adaptation | Synchronous domain/current validity on owner; explicit serialized input and UI/provider dispatch adapters. | Same-turn invalidation/command admission, deferred presentation, asynchronous completion, membership reentry; no promise of arbitrary concurrent mutation. |
| R09 — virtual error hook previously deferred | Make protected `RaiseErrorsChanged(string)` virtual; retain parameterless forwarding and document the base-call notification contract. | Derived dispatch, base-call contract, null/empty/nested paths, `HasErrors` before `ErrorsChanged`, disposal and both API baselines. |
| R10 — direct property component remains reflection-only | Add typed observable/descriptor constructor/factory for direct `BasePropertyValidation` consumers. | Direct constructor users receive initial/predicate/message/context ownership behavior and warning-free native route; unrestricted expression constructors retain precise RUC. |
| R11 — public static binding factories remain reflection-only | Safe typed counterparts or intentional normal/Unsafe split for all eleven `ValidationBinding.For*` overloads; preserve `FormatAll` and resolver behavior. | Property/model/helper factories called directly, state plus formatted-list callbacks, formatter resolution, strictness, selected-source lifetime, API parity and actual native execution. |

Source: [observable registration](../src/ReactiveUI.Validation/Extensions/ObservableValidationRuleExtensions.cs),
[observable binding](../src/ReactiveUI.Validation/Extensions/ObservableValidationBindingExtensions.cs),
[target support](../src/ReactiveUI.Validation/Extensions/GeneratedValidationBindingSupport.cs),
[error delivery](../src/ReactiveUI.Validation/Helpers/ReactiveValidationObject.cs),
[public static binding factories](../src/ReactiveUI.Validation/ValidationBindings/ValidationBinding.cs),
[legacy property component](../src/ReactiveUI.Validation/Components/BasePropertyValidation%7BTViewModel,TViewModelProperty%7D.cs),
and [prior behavior/deferrals](upstream/implementation-2026-10.md#explicit-deferrals-and-superseded-proposals).

### SDK, warnings, packaging and advertised support

| ID / starting point | Implementation or preserved contract | Required acceptance |
| --- | --- | --- |
| A01 — SDK nullable bridge and explicit generic source spelling | Retain traditional observable extensions; add a source-friendly typed factory/output-type convenience rather than restore CS8714-producing extension blocks. | Inferred/static/explicit generic calls with reference/value nullability, ergonomic output-only factory use, unchanged CLR signatures and both baselines; narrow SST1703 exception justified. |
| A02 — Unsafe/reflection and annotation boundaries | Complete exposed API/reachable dependency audit; precise RUC/RDC where actual operations need them, safe catalog/provider paths proven independently. | Unsafe warnings originate at real reflection, safe direct/component/runtime routes warning-free, dedicated reflection-only setter fixture and no hidden suppression/accidental roots. |
| A03 — formatter/DI/provider initialization | Explicit typed formatter/static DI registration and safe fallback; audit custom-provider initialization separately. | Resolver missing/custom registration, explicit formatter, provider factory/lifetime, trim/native reachability; no assumption arbitrary assembly scanning is safe. |
| A04 — package-wide AOT contract not yet advertised | Complete audit and decide/document `IsAotCompatible` from actual producer/package evidence; annotated Unsafe may remain. | Public surface/producer checks, warning-free safe consumers and accurate unsafe diagnostics; no inference from one console run or annotation-hidden dependency body. |
| A05 — compiler/analyzer/configuration/package restrictions | Same analyzer plus flavor props, clean compile-band checks, actionable missing-runtime/location/config messages, no compiler runtime payloads. | Real packed transitive consumers, excluded/disabled assets, allowlist failure, old compiler, mismatched runtime, assembly collisions and emitted generic/access signatures. |
| A06 — preserve current graphs and immutable evidence | Current released `.30` pair, matching flavors and exact restored bytes; historical `.5` remains baseline only. | No upstream DynamicData/opposite flavor/System.Reactive in Primitives; clean-source pair manifests, corrupt/cache-byte failures, current-invocation reports and native same-artifact audit. |
| A07 — native host versus provider/UI coverage | Prove completed library routes on supported Linux/Windows x64; distinguish XAML/provider fixtures from advertised platform execution. | Managed/full-trim/native actual-package execution on both hosts; no fake UI-native evidence, extra RID/.NET11 claim or accidental workload/release expansion. |
| A08 — preserve existing OAPH/context/formatter infrastructure | Existing generated OAPH, context abstraction and helper lifecycle stay intact; old helper/global task-pool patches are superseded contracts. | Released/current dispatch comparison, read-only helpers, explicit scheduler, formatter fallback and ownership regressions; no gratuitous historical patch replay. |

Source: [observable compatibility correction](generated-validation-design.md#observable-extension-compatibility-correction),
[NativeAOT warning/platform audit](aot-and-generators.md),
[maintenance gates](maintenance.md#validation-and-development-environment),
[difference register](fork-differences.md) and
[development instructions](../CONTRIBUTING.md).

## Verification and final record

For every row, keep a positive behavioral test and a negative boundary test where
applicable. Compiler success alone does not prove correct observation, storage
mutation, registration ownership or trimming. Run both flavors, preserve exact
original call spans and inspect emitted getter/setter/provider operations. Build
fixtures against real producer packages; verify the selected bytes and graphs
before execution. Include ABI fixtures compiled separately before the new library
and tests for registered and unregistered runtime operation dispatch.

The final actual-package corpus must enumerate every normal overload, safe
metadata/observable variant, direct component route and new typed catalog/API
shape. Exercise source/target null and replacement, provider affinity, struct
write-back, explicit access/generic bridges, runtime selectors/delegates,
precompiled callers, full metadata, initial sequences, rich states, async/row
ownership, synchronous scheduling, and failure cleanup. Preserve warning errors
for managed/trim/native stages and require runtime NativeAOT assertions.

Use the locked development environment and existing caches. Check storage before
expensive matrices, serialize native work conservatively and retain bounded logs,
reports and generated-source snapshots. No builds are performed by writing this
plan. Implementation verification follows the current maintenance policy; a
historical green gate does not complete new code.

Maintain a completion table linked to each ID: implementation commit/files,
permanent regression, compiler/runtime result, exact package source/version/hash,
host and restored cohort, compatibility/API decision, and remaining genuine
automatic impossibility with its **implemented alternative**. When a new gap is
found during implementation, add it rather than shrink the promised surface.
Update migration/design/register documentation to reflect completed behavior;
retain dated research as historical evidence. Tested source and a later document
record stay distinct. Verified implementation does not mean published release.

The [implementation progress ledger](generated-capabilities-progress.md) tracks
the 43 rows and their required code, regression and applicable package proof.

## Mandatory addendum: typed normal overloads and original argument evaluation

**Decision checkpoint, 2026-10-06; implementation and final acceptance pending.**
The accepted local core at `8c872ef7224d27ddb7708fab4f691f4be44bc0e2` has
1,287 tests (455 per flavor +377 compiler), zero warnings/errors/failures/skips,
with both new portability controls retaining the prior 1,285 test identities.
The latest guard count is 49. Exact-head CI 37492455572 passes both full core
suites but its managed package gate fails the Unsafe RUC display guard; an
ENG correction is separate from accepted package execution. The `.206` local
Primitives managed/full-trim 23-case execution, 29-method inventory and seven
accessor contracts pass. Native publication fails with a real caller IL3050.
These are scoped checkpoints, not final Linux/Windows or 43-row completion.

The failure occurs while evaluating the original expression argument for an
expanded params indexer, such as `x => x[x.Key, 1, 2]`: the compiler constructs
`Expression.NewArrayInit`, which requires dynamic code. An interceptor replaces
the invoked method, **not evaluation of its original arguments**. Successful
C# expression-tree admission and typed emitted getters therefore do not prove
warning-free NativeAOT construction of every original selector expression.
The existing expression signature cannot transparently erase that construction
in already compiled callers.

The locked compiler probe establishes a feasible source-compatible route:
higher-priority normal overloads accepting typed `Func` selectors select ordinary
inline lambda syntax without constructing an expression tree. Implement this
route as part of the mandatory work, rather than replacing the shipping fixture
alone with an explicit alternative. Preserve stored `Expression` calls and all
29 original normal definitions/CLR signatures: 28 selector-expression methods
and one selector-free static factory. Add 28 typed counterparts, for 57 normal
definitions in total. Explicit expression calls
remain supported with their precise construction boundary; a qualified call
using a preconstructed array is a separate generated positive control. Expanded
expression construction keeps a truthful IL3050 control and an implemented
typed alternative, without adding suppression.

### Implementation order and contracts

1. **Freeze overload selection before lowering.** Add the typed normal overload
   family across the four predicate rules, fourteen binding extensions and ten
   selector-taking static factories. The eleventh static factory,
   `ForViewModel(view, Action<TOut>, formatter)`, has no selector and remains a
   single preserved definition. Apply deliberate overload
   priority and prove actual C#14 receiver, traditional static and synthesized
   static-shim selection in both flavors. Preserve generic arity, constraints,
   nullable input/output contracts, metadata-only observable applicability and
   existing explicit-generic ergonomics. Stored/explicit expression arguments
   still bind to their original signatures; safe observable metadata calls must
   not acquire a false normal-generator diagnostic. Do not infer selection from
   candidate lists or the priority attribute's presence alone.
2. **Lower typed normal selectors through the same semantic plans.** Ordinary
   source lambdas retain exact dependencies, structural metadata, original
   evaluation order, captures, private/generic bridges, null and initial policy,
   writable target conversion, struct write-back and lifetime behavior. A target
   getter delegate is not an assignable target by itself: generation must supply
   a legal setter/lens or use the existing authored target capability. Method
   effects and silent owners still require declared dependencies/providers.
   No expression construction, hidden delegate-target inspection or reflected
   closure reads may be introduced to recover metadata or execute selectors.
3. **Implement the callable and registered bodies.** Typed normal overloads need
   usable finite provider/catalog dispatch for stored delegates, method groups,
   captured runtime selectors and separately compiled peers. Bind each current
   receiver/capture through an explicit typed contract; preserve scoped leases,
   deterministic ambiguity, registered precedence and genuine-absence fallback.
   Unknown opaque delegates use an authored selector/target/provider route and
   actionable missing-capability behavior. Do not present an interception-only
   stub or a diagnostic as completion of this route.
4. **Extend permanent and actual-package inventories independently.** Retain all
   29 original normal method IDs: 28 explicit Expression controls plus the
   selector-free static factory. Add
   an exact typed-normal inventory from the final public definitions, verifying
   compiler-selected symbols and actual interceptors; do not count registered
   calls or safe metadata overloads as generated inventory. Cover original
   expanded-expression IL3050, ordinary inline typed params calls with no
   `NewArrayInit`, preconstructed-array expression calls, and authored typed
   callable alternatives. Inspect actual source/IL and execute results.
5. **Repeat final acceptance on a fresh clean source and pair.** Review both API
   baselines and compatibility before packing. Run fresh discovery/full strict
   core, both collection examples, current guards and independent consumers.
   Then require both flavors' managed/full-trim/NativeAOT actual-package runtime
   and semantic checks on Linux/Windows x64, using the same audited package
   artifacts. Keep Unsafe observer/setter diagnostics, effective SDK warning
   defaults and analyzer/configuration negatives precise. The failed `.206`
   attempt and earlier source-pinned core proof remain historical.

### Existing register rows extended by this decision

| IDs | Additional implementation and acceptance obligation |
| --- | --- |
| C01 | Classify the actually selected typed or expression definition; safe supplied-observable metadata remains safe, with no early false positive or missing-dispatch escape. |
| C02 | Typed normal overloads preserve original generic slots, logical C#14 constraints and legal private/open hosted bridges; prove nullable and genuinely constrained controls through receiver/static calls. |
| S03 / S04 | Ordinary inline computed/captured/params selectors use typed delegates and exact plans without expression construction; index keys/order, optional/default/params conversions and actual update/disposal behavior survive. Reordered named expression trees remain CS9307; legal typed source and authored alternatives execute. |
| S05 / S06 | Stored delegates, finite factories and method groups have real typed callable/catalog bodies with current captures and ownership; missing, ambiguous, failed and registered-override cases remain deterministic. |
| S07 | Preserve old expression CLR ABI and fresh-expression matching for unchanged peers. Also prove separately compiled typed callers. Neither route claims to rewrite original expression construction in old IL. |
| R11 | Ten selector-taking static factories gain typed counterparts; the selector-free eleventh factory stays intact, preserving formatted-list/raw-state callbacks, resolver, strictness and selected-source lifetime. |
| A01 | Ordinary inferred, explicit-generic and qualified static call syntax remains usable without the nullable shim regression or ambiguous overloads; intentional new public definitions are reviewed in both baselines. |
| A02 | Safe typed normal params calls publish and execute without IL3050; the original expanded-expression construction warning remains truthful. No authored IL suppression or automatic expression evaluator is added. |
| A05 | Package assets deliver the expanded generator/classifier and both inventories together; missing, mismatched and disabled configuration controls still prove actual compiler inputs and actionable dispatch. |

These obligations extend the existing 43 mandatory rows; none is deferred for
implementation difficulty or time. Source/API implementation, permanent
positive/negative tests, behavioral execution and applicable final package/host
proof are all required before completion. Dependencies and supported platform
scope remain unchanged, and verified implementation remains UNRELEASED until
publication.
