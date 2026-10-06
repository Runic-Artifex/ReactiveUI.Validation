# Generated capability implementation progress

Tracking the [approved capability plan](generated-capabilities-plan.md), recorded
at `e8d934275889ef0e04d98699efb7aaf4a1184021`, starting from
`0029213f55a49f147b472c1c8037a75ab606b548`. This ledger records implementation
and acceptance separately. It does not supersede historical release/source or
package evidence, change the `.30` dependency pins, or announce a release.

**Current status: no new capability row is complete.** Typed formatter,
runtime, adapter/error-hook and static access stages have focused managed verification.
Typed runtime, semantic lowering, producer prediction and the .NET 10 AOT flag
have landed. Reviewed focused compiler/behavior results are recorded below;
the final full-solution, actual-package and Linux/Windows trim/native gates remain
pending.
Architecture agreement or
a diagnostic is not implementation proof. Existing supported behavior marked
**Preserve** needs current-source regression and package verification after the
new implementation is integrated.

Workstream owners are **Compiler** (selection, projection and typed access
bridges), **Bindings** (source/target lowering and binding compatibility),
**Runtime** (typed plans, catalog/dispatch and component construction),
**Adapters** (owned async/collection/presentation behavior), and **Verification**
(package, compiler configuration and actual-host acceptance). The integration
owner coordinates shared rows; API and independent review apply throughout.

Each **Complete** row must link landed code, permanent positive/negative
regressions, behavioral results and applicable actual-package trim/native
evidence. Compiler-only XAML/provider integration must not be described as UI
native execution. Source, package bytes, host and cohort must identify the tested
implementation. Review results alone do not complete a row.

| ID / capability | Owner | Status | Required proof before completion |
| --- | --- | --- | --- |
| C01 — safe metadata classification | Compiler | Focused compiler corpus verified; package regression pending | Real generated command metadata succeeds; normal missing-dispatch negative remains; both flavors. |
| C02 — open generic contexts | Compiler, Runtime | Typed backend landed; integrated acceptance pending | Legal original-arity bridge; constraints/nullability; open model/method contexts and native closed instantiations. |
| C03 — inaccessible/private member access | Compiler, Bindings | Static bridge managed verified; automatic/package proof pending | Legal private/protected/nested bridges and explicit nonpartial/external route; real getters/setters and access negatives. |
| C04 — anonymous/file-local shapes | Compiler | Typed factories/backend landed; integrated proof pending | Inferred typed factory execution with complete state; inaccessible-type boundary and compiled-peer coverage. |
| C05 — init/readonly targets | Bindings | Static bridge managed verified; automatic/package proof pending | Explicit init modreq/readonly bridge proof or immutable replacement; trim/native setter behavior and no invented backing storage. |
| C06 — closed accessible/internal types | Compiler | Exact bridge flow managed verified; final packages pending | Existing generic/internal shapes; effective MaybeNull/NotNull read flow and Nullable<T> bridge controls; preserve current-package behavior. |
| S01 — conversions and writable lenses | Compiler, Bindings | Access29/binding80 managed verified; final packages pending | Checked/user/interface/null conversions and errors; no invented inverse conversion; effective read postconditions separate from AllowNull/DisallowNull setter input, including Nullable<T> controls. |
| S02 — fields | Compiler, Bindings | Semantic backend/lowering landed; integrated proof pending | Typed reads/writes with events/invalidation; readonly snapshot and silent-mutation boundary. |
| S03 — computed/captured/method dependencies | Compiler, Runtime | Backend/typed factories landed; integrated proof pending | Multi-property/captured-root updates, explicit opaque dependencies, getter failure and stale detach. |
| S04 — indexers and dynamic arguments | Compiler, Bindings | Exact boundary/compiler behavior verified; final packages pending | Typed optional enum/NullableEnum defaults, exact keys/order; CS9307 reordered-expression-tree negative plus executable typed Func alternative; live collection/item events and metadata/native behavior. |
| S05 — stored/factory selectors | Compiler, Runtime | Finite provenance/fallback managed verified; final packages pending | Actual dispatch, readonly/implicit-in escapes, constructed generic semantics; registered preference versus genuine-absence compiled fallback, captures, scoped missing/ambiguity behavior and native execution. |
| S06 — callable delegates | Compiler, Runtime | Runtime dispatch opt-in/facades landed; integrated proof pending | Real callable factories/registered normal dispatch; method groups, generic signatures, disposal and native execution. |
| S07 — precompiled callers | Runtime, Verification | Structural catalog foundation landed; ABI acceptance pending | Old IL creates fresh expressions with changing receiver/captures; scoped leases/ambiguity; managed and fresh native host. |
| S08 — notification providers/snapshots | Runtime, Bindings | Typed dependency foundation landed; integrated proof pending | Explicit provider precedence; INPC/custom/POCO snapshot, invalidation, replace/null/error/disposal. |
| S09 — struct storage and write-back | Runtime, Bindings | Cell/lens foundation landed; cross-binding acceptance pending | Mutate actual stable owner through all writable parents; box/copy/identity controls and native execution. |
| S10 — peer-generated calls | Compiler, Runtime | Actual peer compiler/runtime verified; package/native pending | Owned typed integration executes; unregistered normal peer call rejected; no ordinary-generator ordering assumption. |
| S11 — ref-struct/stack lifetime | Runtime, Bindings | Snapshot adapter managed verified; final acceptance pending | Synchronous snapshot alternative, lifetime escape negative and stable owned storage behavior. |
| P01 — Reactive field prediction | Compiler | Producer/compiler interop verified; package proof pending | Actual producer naming/access/nullability/inheritance/generic fidelity in every selector position; no emitted fake tree. |
| P02 — generated notification interfaces | Compiler | Producer/compiler interop verified; package proof pending | Actual generated/inherited/explicit contracts, semantic applicability and event lifetime; real nested IReactiveObject producer limitation gets explicit alternatives. |
| P03 — generated commands | Compiler, Adapters | Producer/compiler interop verified; admission/package proof pending | Actual sync/Task/observable/token/flavor mappings; CanExecute and producer invalid-signature controls. |
| P04 — generated collections/derived lists | Compiler, Adapters | Producer/compiler interop verified; full behavior/package proof pending | Actual member contracts plus item/source replacement/null/removal and owned cleanup. |
| P05 — partial properties and OAPH | Compiler, Runtime | Preserve | Actual declared partial setters/OAPH initialization, nullable delivery and helper disposal in both flavors. |
| P06 — XAML/provider typed integration | Compiler, Bindings | Actual platform compiler/provider verified; package proof pending | Real recognized XAML/generator inputs, access/field/provider behavior and final member checks; compiler/provider evidence does not establish UI/native host execution. |
| P07 — incremental projection/final contracts | Compiler | Focused projection/drift corpus verified; package proof pending | Original locations, deterministic changed-input invalidation, cancellation, drift/order negatives and no projected declaration emission. |
| R01 — full paths and custom states | Runtime, Bindings | Preserve; explicit legacy/structural membership focused verified | Exact/strict multi-property/entity metadata; legacy full ordinal-name opt-in with structural/structural identity retained; membership changes and full class/struct state identity. |
| R02 — context/interface ownership | Runtime, Bindings | Preserve | Default/selected shadow controls, independent contexts, captured helper removal and replace/null behavior. |
| R03 — explicit null policy | Runtime, Bindings | Policy types/lowering landed; integrated proof pending | Default/suppress/fallback traces; absent parent versus present null leaf; target skip/cache behavior. |
| R04 — explicit initial presentation policy | Runtime, Bindings | Policy types/lowering landed; integrated proof pending | Actual state versus legacy prelude/membership seeds; initial/late rules and raw/format/typed outputs. |
| R05 — target handoff and typed output | Bindings | Binding80/runtime refresh managed verified; final packages pending | Nullable/custom setter inputs, identity/null cache; notified immediate replay versus per-output fresh resolution of silent replacements, owned AfterRead caches/reentry/failure cleanup. |
| R06 — reentry/failure/disposal | Runtime, Bindings | Preserve | Initial/cleanup failures, reentrant switch/removal/dispose, stale callbacks, completion/error and borrowed ownership. |
| R07 — owned async/collection policies | Adapters | Adapter managed verified; integrated/package proof pending | Initial snapshots, same-key/source switch/removal, late/cancelled requests, pending/advisory policy and cleanup. |
| R08 — domain/presentation scheduling | Runtime, Adapters | Scheduling adapter managed verified; integrated/package proof pending | Synchronous command admission/domain state, explicit serialized inputs and deferred UI/provider delivery. |
| R09 — virtual error hook | Runtime | Hook managed verified; integrated/package proof pending | Protected string hook dispatch/base forwarding, error order/path/null/disposal and both API baselines. |
| R10 — direct property components | Runtime | Typed constructors landed; integrated proof pending | Typed constructor/factory parity, initial/predicate/message/ownership and warning-free package-native route. |
| R11 — static binding factories | Bindings, Runtime | All-eleven lowering landed; integrated proof pending | All eleven public factories, `FormatAll`/resolver/strictness/state/lifetime and package-native execution. |
| A01 — generic observable ergonomics | Runtime, Verification | Output factory managed verified; full caller/package proof pending | Inferred/static/explicit nullable callers and output-type convenience; CLR compatibility without CS8714 regression. |
| A02 — accurate Unsafe/AOT boundaries | Runtime, Verification | Integrated source audited; final package proof pending | Full exposed/reachable audit; safe routes zero warnings, reflection warnings accurate, no suppression or accidental roots. |
| A03 — formatter/DI/provider setup | Runtime, Verification | Formatter implemented; focused managed verified; package/native pending | Custom/missing resolver, explicit formatter/provider factories and trim/native reachability without scanning assumptions. |
| A04 — audited package AOT contract | Verification | net10.0 flag landed; final flagged-package proof pending | Producer/public/dependency audit and actual safe-package proof; accurate annotated Unsafe contract and reviewed metadata. |
| A05 — compiler/analyzer/package delivery | Compiler, Verification | Actual collision/host corpora verified; package controls pending | Actual transitive assets, missing/mismatched/disabled configuration negatives, collisions and no runtime compiler payload. |
| A06 — current graphs/immutable evidence | Verification | Preserve | Current `.30` exact bytes/flavors, separate historical `.5`, stale/cache/corrupt failures and same-artifact host reports. |
| A07 — supported host/provider scope | Verification | Planned | Current packages managed/full-trim/native on Linux/Windows x64; compiler/provider evidence distinct from UI-native claims. |
| A08 — OAPH/context/helper infrastructure | Runtime, Verification | Preserve | Existing generated dispatch, readonly helpers, context abstraction, formatter fallback and explicit scheduler regressions. |

## Landed implementation and acceptance records

**A03, formatter registration:** implementation
`f960c4bf1ca9efa5682a51a624f0339e49aa6d99`, integrated by real merge
`4e6ea11afebb78b145891ce3724c939b78636e67`, adds
[ValidationTextFormatterRegistration](../src/ReactiveUI.Validation/Formatters/ValidationTextFormatterRegistration.cs),
three [registration regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationTextFormatterRegistrationTests.cs)
and five reviewed public API lines in each flavor. The helper registers the
closed formatter interface or an explicit factory; it does not discover or
activate implementation types.

Focused Linux managed checks against implementation content `f960c4b` pass
**5/5 tests per flavor, 10 total**, with zero failed/skipped and strict
warnings-as-errors compilation. Each flavor runs the three new registration
tests and two existing resolver tests. They verify interface/last-registration
selection, explicit-formatter precedence, deferred factory construction,
resolver-specific disposal, null-result fallback and propagated factory errors.
API/source/lifetime review accepts the landed contract. Retained logs/TRX are in
the audit worktree's `artifacts/verification/aot-surface-audit/`:
`primitives-formatter-run.txt`, `reactive-formatter-run.txt` and matching
`*-formatter.trx`. This focused result is not new package, Windows, trim or
NativeAOT evidence; A03 is not complete. This documentation record is later than
the tested implementation content and does not relabel historical `.5` evidence.

**Shared compiler acceptance infrastructure:**
`9a16338573ef057e36e903c9df0055a86d5361ce` adds the
[actual-producer compiler host](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/CapabilityCompilerHost.cs)
and [four host tests](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/CapabilityCompilerHostTests.cs).
The supplied focused result is **4/4** for actual SourceGenerators 4.2.0 and
Binding 9.1.0, both flavors and both driver orders, with zero compiler/generator
warnings/errors. Tests execute the existing declared partial-property route,
edit/revert the input and check generated-output invalidation/determinism.
This establishes test infrastructure; it does not complete new field/interface,
command, collection, XAML or access/generic frontend capability rows.

**Semantic backend and API lowering:** implementation `81ce0da` adds the
[operation planner](../src/ReactiveUI.Validation.SourceGenerators/SemanticSelectorPlanner.cs),
typed access plans and original-arity generic/lexical bridges. The supplied
focused result is **18 mechanical compiler/execution probes** with strict
compiler/host builds. These probes deliberately use minimal runtime carriers;
they do not prove integrated Validation runtime semantics. The subsequent
[rule lowering](../src/ReactiveUI.Validation.SourceGenerators/RuleEmitter.cs)
at `73ba6ef` covers four predicates, and binding lowering at `6507e8d` covers
fourteen extension overloads and eleven static factories. Their landed
regression corpora are not recorded as passing yet. The shared classifier
(`47eda5c`) and explicit runtime-dispatch compiler policy (`0d31cc2`) also need
their integrated positive/negative results. In particular, C01 remains open
until the real generated-command safe metadata control succeeds.

**Typed runtime foundation:** source `8e269e5`, integrated at `07dfd02`, adds
[selectors](../src/ReactiveUI.Validation/Capabilities/ValidationSelector%7BTSource,TValue%7D.cs),
access/read/dependency plans, targets, stable cell/lens storage, structural
paths, explicit null/initial options, typed component constructors and normal
facade forwarding. The [finite registry](../src/ReactiveUI.Validation/Capabilities/ValidationPlanRegistry.cs)
provides owner attachment leases, exact-instance and structural-pattern
registration with original API generic slots. The compiler-visible
[dispatch opt-in](../src/ReactiveUI.Validation/Capabilities/ValidationRuntimeDispatchAttribute.cs)
does not establish that runtime registration exists. Supplied strict foundation
builds report zero warnings. Later focused runtime results are recorded below;
fresh-expression old-binary ABI and whole generator/package acceptance remain
pending. This source stage
does not replace previously verified API records or establish the new package
contract. It does not claim that arbitrary runtime expressions are automatically
generated.

**Producer frontend:** integration `0f79267` lands private declaration prediction,
XAML projection and final member-contract validation. The
[versioned profiles](../src/ReactiveUI.Validation.SourceGenerators/Producers/ProducerProfiles.md)
describe SourceGenerators 4.2.0, Binding 9.1.0, Avalonia 12.1.3 and MAUI
10.0.110 contracts. Source availability is not actual producer execution proof.
Real producer regression, changed-input/order/drift controls and marked XAML
compiler checks remain pending. These profiles do not expand advertised host
support or establish native UI execution.

**A02–A04 audit and flag:** the retained
[formatter-stage surface/IL investigation](../investigations/AotSurfaceAudit/README.md)
at `d022bdd` is explicitly scoped to `f960c4b`; it is not a whole-current-source
audit. Source `f8ed2dc` adds `IsAotCompatible` only to the two .NET 10 core
targets. Final acceptance must audit the complete new reachable surface and
run the exact flagged packages with strict analysis on both supported hosts.
Neither the historical audit nor the property alone closes A04. No new shipping
package, trim/native or Windows result is recorded here.

**Owned adapters and error hook:** exact tested source
`2978e51289ca4c74cc0125660ccd62435586c9b0`, integrated at `22f6f15`, passes
**100 managed tests: 22 adapter and 28 error tests per flavor**, with zero
failed/skipped. Both Release core builds pass strict analysis with zero
warnings/errors on SDK 10.0.401. The source/API review accepts the shared
contract. The merged baselines preserve 45 capability type/delegate blocks per
flavor: 36 runtime and nine adapter blocks. Permanent
[adapter regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationCapabilityAdapterTests.cs),
[owned-lifetime regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationOwnedAdapterTests.cs),
[view-adapter regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationViewAdapterTests.cs)
and [error-hook regressions](../src/tests/ReactiveUI.Validation.Tests/ValidationErrorHookTests.cs)
cover cancellation/stale results, explicit pending/failure policy, row ownership,
replacement/removal, synchronous snapshot/ref-struct input, output-only factory
inference, presentation scheduling and the virtual protected string hook.
Existing error regressions are included in the 28 per flavor. The hook tests
cover `HasErrors` ordering, base forwarding, old/new display paths, reentrant
removal and borrowed-helper lifetime. The retained adapter-worktree report
`artifacts/verification/adapters/2978e51/summary.json` pins source/file/log/TRX
hashes and executed case names. This proof does not establish ReactiveCommand
activation, automatic producer integration, new actual-package/native execution
or native UI support; those acceptance obligations remain open.

**Static access compatibility:** exact tested source
`2f3b423d7bab34dfa6a677552a0f3280a3998c3c`, integrated at
`4ee5741`, passes **24 actual managed compiler/execution cases**, with zero
failed/skipped, plus strict current host, both core and netstandard generator
builds with zero warnings/errors. The
[access emitter](../src/ReactiveUI.Validation.SourceGenerators/AccessBridgeEmitter.cs)
and [permanent compiler regressions](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/AccessBridgeCompilerTests.cs)
prove selected existing init/readonly/private storage, exact generic declaring
positions, erased inaccessible reference owners, legal virtual/protected access
and actionable unsupported-signature controls. They do not infer getter-only
backing storage or permit originally illegal ordinary access without explicit
selection. This static bridge proof is distinct from complete automatic semantic
backend coverage and actual-package/native bridge execution, which remain
pending. The 24 cases comprise 22 flavor-independent signature/compiler/runtime
cases and one private helper/context case for each flavor. The retained access
worktree report `artifacts/access-production-proof.json` has SHA-256
`adb3fad3d9f9a627002e949f060e41c95cb9ef63d54717ce57aea40d8b0fd41e`
and pins tested source, binary, log and TRX hashes. The source-stage proof does
not close C03 or C05.

**Runtime ownership follow-ups:** tested source `140ab94` passes **73 focused
managed tests per flavor, 146 total** with strict paired product builds reporting
zero warnings/errors. `394d8bf` adds typed
`ValidationRuntime.DefaultContext<TModel>()`/owner overloads and distinct
`ValidationPlanRole.DefaultContext`, passing **three narrow tests per flavor**.
Provider resolution uses current model before view/owner, then known context
fallback with optional INPC or snapshot. Source
`0f84c7490dfdaa7ca7cabcf09b25275cff36490a`, integrated at `d71b208`, passes
**four narrow ownership tests per flavor** and strict paired builds. Missing-owner
Suppress detaches old model/helper/context/state/path inner subscriptions without
presentation output; present null leaves keep normal valid/empty selection
semantics. Cold per-observer reads, explicit storage receipts/origins, Writer
cleanup and AfterRead cache contracts have permanent
[runtime regressions](../src/tests/ReactiveUI.Validation.Tests/CapabilityAccessTests.cs)
and [ownership regressions](../src/tests/ReactiveUI.Validation.Tests/CapabilityOwnershipTests.cs).
The runtime-worktree `artifacts/capability-focused-reproduction/manifest.json`
pins the tested stages and reproduction/file hashes. These tests use bounded
focused projects with real core references and inherited analyzers; they are
not the final full-test, package/native or old-binary matrix.

**Later planner checkpoint:** exact source
`df5dbab2bdff411759c65a3f0d1891a9274ef533` supplies **29/29 focused mechanical/
shared compiler proofs**, including actual runtime `140ab94` references. The
[permanent semantic corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/SemanticPlannerTests.cs)
preserves original-arity generic/lexical constraints and typed descriptor
alternatives. This advances the earlier minimal-carrier checkpoint without
establishing whole rule/binding/producer/platform or package-native acceptance.

The [integrated static checkpoint](../investigations/AotSurfaceAudit/IntegratedSourceCheckpoint.md)
and [runtime delta](../investigations/AotSurfaceAudit/RuntimeSourceDelta.md) retain
their exact pinned source/surface hashes separately from the historical formatter
audit. They establish source/IL inspection, not execution of final packages.

**Initial whole binding sweep:** at `d71b208`, the strict host compiles with
zero warnings and executes 52 cases: **42 pass, 10 fail, zero skipped**.
The retained binding-worktree log is
`artifacts/verification/bindings/first-compiler-sweep.log`. Reported remaining
failures involve four fixture imports, four read-side target conversions and
two legacy initial-sequence cases. Corrected source requires a new sweep;
neither initial discovery nor the passing subset completes binding rows or
establishes package/native acceptance.

**Current focused compiler checkpoints:** these later results supersede the
earlier source-only status for their particular corpora, not final package proof.
None completes a capability row. Counts describe executed cases, not a per-flavor
count unless explicitly stated.

| Group | Tested source and focused result | Retained evidence / remaining boundary |
| --- | --- | --- |
| Semantic planner and rules | `13d0221ca09a508d0f02184d29df1eb7643d3f45`: strict current compiler host/product references have zero warnings/errors; planner **35/35**, rules **38/38**, zero skipped. | Planner-worktree `artifacts/planner-review-fixes-tests.trx` and `planner-shared-rule-tests.trx` counters independently inspected. [Permanent rule corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/RuleCapabilityCompilerTests.cs); new rule/peer and setter-input cases remain pending. |
| Access and cross-assembly collision | `af3a23b16324d82bafe5b55a17f8ffd7fad7dc91`: **25 discovered/executed/passed**, zero failed/skipped; strict host/generator/both product references have zero warnings/errors. | Access-worktree `artifacts/access-collision-proof.json`, SHA-256 `b98c291124279925b741a87e79e8775dea59f4eba885a33244db6d2bd54bd67e`, independently checked. Real IVT peer has identical local bridge IDs and matching sanitized assembly text but distinct hash namespace subtrees. Automatic/package/native acceptance remains pending; the earlier 24-case report is retained. |
| Actual producer interop | `4984464e4b12b4ac9aa9035f2c4598ea2f32c7a3`: **4/4**, zero skipped and zero final compiler/generator/dispatch diagnostics; real backend `13d0221` and host `397272f`. | Producer-worktree `artifacts/verification/producers/ProducerInteropCompilerTests-final.log` and matching TRX. [Interop corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/ProducerInteropCompilerTests.cs); current actual-package/native execution remains pending. |
| Producer shapes, XAML and policy | Producer source `e8acfc3` plus `ab1fc76`: **16/16**; `015a3c4564654a600e5b977e871366402f053d66`: XAML **40/40**, dispatch/lookalike **6/6**, zero skipped. | [Producer corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/ProducerCompilerTests.cs), [XAML corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/ProducerXamlCompilerTests.cs) and [dispatch policy corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/RuntimeDispatchPolicyCompilerTests.cs). Includes real nested IReactiveObject producer limitations and usable declared-base/typed alternatives; do not advertise unsupported nested producer output. |
| Shared actual compiler host | `397272f8c6ab76c176aa26bc6548bea2c1188086`: **6/6** selected host cases. | [Host corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/CapabilityCompilerHostTests.cs); measured discovery needs a namespace-qualified class suffix filter. Host infrastructure alone is not package acceptance. |
| Actual platform producers/providers | `0aaf329f3b5a9ade3f9dded6bb52164d0d6f1ff6`: strict build zero warnings/errors, **33 discovered/executed/passed**, zero skipped. | [Permanent acceptance record](../src/tests/PlatformProducerInputs/README.md), later docs `428a536`/`dc14842`, records exact input/generator hashes and compiler/provider evidence. Twelve combined profiles emit managed PE against real framework metadata. No UI/workload/native host was executed or added to release scope. |

**Historical gaps before later access/binding proof:** S01/R05 require getter output and setter input
to be resolved independently, including `AllowNull`/`DisallowNull` on ordinary
and bridged setters. Four focused binding-nullability cases are pending. Later
six-rule/four-peer results are recorded below. The expanded runtime case map
expects 23 application cases; mapping or fixture source is not execution.
Runtime `c666a0fca0a3c42b4599f7deeb42ead9e34166f6` has **four legacy-policy tests
per flavor, eight total**. A separate **two-case generated binding proof** at
`975153d8f75923da1128ac3dad3162921a7ab1f6` is retained in binding-worktree
`artifacts/verification/bindings/legacy-initial-pair.log`. Neither establishes
the complete binding/package matrix.

The [frozen historical peer](../eng/verification-fixtures/LegacyPeer/README.md)
retains old `.5`/Validation `.790.17.15.10` compilation and IL provenance at
`1b901e4972fb17f8493c1583dac8f0b8a1566ee0`, including unchanged normal
calls/method-group references. Its current
registered managed/full-trim/native execution remains pending. Building current
peer source cannot substitute for running those unchanged historical bytes.

Keep partial implementation and incomplete proof explicit; the final record
must account for all 43 IDs. Later documentation commits identify these source
stages without becoming their tested implementation SHA.

**Later compliant rule/peer proof:** exact clean tested source
`52fcc91e2199609ba764186df5deda02ec8d1cc2` builds the strict current compiler
host and executes **48/48 rule cases, zero skipped**, including four custom-truth,
two static-object and four real peer-generated caller cases. The accepted run
uses the documented build-enabled `dotnet run` command with
`--minimum-expected-tests 48`; an earlier same-source `--no-build` invocation
is historical and is not the accepted proof. Rules-worktree
`artifacts/verification/rules/compliant-rule48-tests.log` and `.trx` retain the
result. The [actual peer corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/RuleCapabilityCompilerTests.Peer.cs)
co-runs a real private incremental generator, executes registered normal/typed
routes in both flavors/orders, and checks unregistered/unknown selection without
invented ordering or effects. The unchanged historical binary peer is separate;
neither this group nor the 38-case predecessor is actual-package/native proof.

**Explicit legacy full-path compatibility:** exact source
`90d2e5f730efd0080d62374ecd8af758156338a5` passes **four focused runtime cases
per flavor, eight total**, with strict paired builds reporting zero warnings/errors.
The [permanent compatibility tests](../src/tests/ReactiveUI.Validation.Tests/CapabilityPathCompatibilityTests.cs)
verify explicit Legacy full-name membership against structural field paths,
strict multi-property behavior and structural/structural separation. Only an
explicit Legacy counterpart opts into ordinal full `DisplayPath` membership;
structural identities, equality and hashing remain unchanged. The runtime-worktree
`artifacts/capability-focused-reproduction/manifest.json` pins this stage and
55 source/reproduction hashes, with paired `path-*.trx` and `capability-path-*.log`.
Generated field-binding/package/trim/native verification must run separately.

**Historical guard/flow checkpoint before later managed proof:** setter/indexer access emission at
`b2f70bc4ba2e58903ebccda981e4d1bf2f7d747f` and its backend lowering separate
setter input annotations from getter output and retain exact typed index keys.
At that checkpoint, the planned 27-case access and 56-case binding groups awaited
passing proof and expansion for the newly found getter-flow/nullable-value controls.
Ordinary reads honor original MaybeNull/NotNull postconditions; exact typed
UnsafeAccessor read bridges were found to drop those property/field/getter-return
contracts. C06/S01 therefore require effective return-flow propagation and strict
meaningful positive/negative controls. Nullable<T> DisallowNull setter input is a
separate binding fix. Those gaps were pending at that source checkpoint.
S05 source `6c48956`/`a301012` rejects guessed static virtual/abstract dispatch
and readonly selectors rewritten or escaped in static constructors, with a usable
typed factory/catalog alternative. Its expanded 45-case provenance corpus then
awaited execution. Diagnostic source alone does not close these mandatory rows.

**Latest reviewed managed stages:** these resolve the earlier measured gaps for
their tested corpora. No row is Complete before final applicable package/host
acceptance; focused groups must not be added together as a full-solution result.

| Stage | Exact tested source / result | Retained proof and boundary |
| --- | --- | --- |
| Catalog preference and fresh targets | `32d4fd88aa00d178f1bcdd623530d024d5e5b608`: **16 narrow tests** (four refresh and four fallback per flavor), strict paired builds zero warnings/errors; full normalized API parity. | Runtime-worktree `artifacts/capability-focused-reproduction/manifest.json` pins 57 source/project hashes and refresh/fallback TRXs. Three additive Resolve overloads prefer registered matches; compiled finite fallback applies only on absence, never matched-plan failure/ambiguity/mismatch. Every output refreshes target/dependency owners with the same owned engine and AfterRead caches; silent replacement gets the new output without stale-output replay or fabricated events. |
| Nullable accessor contracts | `a2bac2e06894925da7fc015343d9de386466b52e`: **29 discovered/executed/passed**, zero failed/skipped; strict host/generator/both product references zero warnings/errors. | Access-worktree `artifacts/access-value-input-proof.json`, SHA-256 `a753360dffa4c53ee3c3145f63e3bf13eb7682b0fdeb7d88065fca1988c12821`. Effective read flow, setter input/index keys and preserved Nullable<T> CLR types pass; truthful MaybeNull reference negative gets CS8604, forbidden nullable value inputs get two CS8607 diagnostics. Older bridge reports retain their narrower identities. |
| Finite fallback and legacy bindings | `dd0bfebb42aefaae85ca14f32268b81b876cfb93`: **80/80** (58 capability +22 legacy), zero skipped; strict host/generator/both runtime references zero warnings/errors. | Binding-worktree `artifacts/verification/bindings/finite-fallback-proof-manifest.json`, SHA-256 `db02249463709e3096abb9549c793924df383901eb6233c2ce84cc51591ae2fa`, records 200 source, 12 artifact and two later export hashes. All eight earlier legacy failures resolve; registered preference across finite aliases, exact nullable/private/indexed setters and fresh plain-target resolution pass. Opaque operations still require explicit dispatch/registration. |
| Semantic/boundary/generic stage | `ba1e49d13f06a9e896bf3caefa9267b61da9f90a`: strict build zero warnings/errors; exactly **45 planner +5 boundary +20 generic =70** discovered/executed/passed, zero failed/skipped. | Planner-worktree group logs/TRXs cover implicit-in escape, protected through-type access, exact typed enum default conversions, [original CS9307 boundary plus typed SF-order alternative](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/SemanticOperationBoundaryCompilerTests.cs) and [constructed generic operators/conversions/nameof/block definitions](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/GenericSelectorProvenanceCompilerTests.cs). Independently reviewed compiler/behavior proof; not final whole-core or native acceptance. |
| Expanded rule stage | `85e241cfba8de6705c43048c1b32f4b01005163a`: strict current-source build/run zero warnings/errors; **58/58**, zero skipped. | Supersedes the earlier 48/56-case focused counts for [this corpus](../src/tests/ReactiveUI.Validation.SourceGenerators.Tests/RuleCapabilityCompilerTests.cs). Includes optional/default/params rule selectors and the authored named-order route with explicit structural key comparison. Actual packed application and unchanged historical peer/native acceptance remain separate. |
| Restored DLL hardening | `d13bba2ef574546c8dd6d50fc7d119a877065066`: **34 Python guards pass**. | Selected extracted DLL bytes are bound to accepted archive entries. Guards do not establish actual application/package execution. |

Shipping export `46746856f1eb38aa998280c95f8662f0e84dbd97` adds 21 binding
groups after its focused proof; the later two exported files were not built by
the recorded 80-case run. The 23-case application map and linked rule/peer/binding
sources prepare final acceptance. They are source linkage, not packed managed,
full-trim/native or Linux/Windows CI execution. Final source-preparation audit and
full-solution/host gates must identify their own exact later source/packages.

### Typed formatter setup

The landed API accepts the resolver used by the application's locator and an
explicit formatter. For Primitives:

```csharp
using ReactiveUI.Validation.Formatters;
using Splat;

ValidationTextFormatterRegistration.Register(
    AppLocator.CurrentMutable,
    new SingleLineFormatter(" | "));
```

Alternatively, construct it only when resolution requests it:

```csharp
ValidationTextFormatterRegistration.RegisterFactory(
    AppLocator.CurrentMutable,
    static () => new SingleLineFormatter(" | "));
```

The Reactive flavor uses `ReactiveUI.Validation.Reactive.Formatters`. Passing an
explicit formatter to a binding still takes precedence. Ownership follows the
resolver: the pinned Splat instance resolver disposes disposable constant
services and does not dispose transient factory results. Validation does not
own those services. A custom resolver/factory retains its own construction,
disposal and native requirements. These source examples describe the landed
managed contract; their actual-package/native proof is pending.

New consumer recipes will use reviewed, landed API names and executable sources.
They must show both actual automatic generator routes and the explicit typed
route for silent owners, captures, stable struct storage, immutable replacement
and dynamic-index notifications. Proposed signatures are not consumer examples.
