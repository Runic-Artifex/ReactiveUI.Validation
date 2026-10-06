# Final source readiness for package gates

No production-source blocker was found at integration
`ccd5aad260995bd23a9abd4e4991d40b801168fd`. The product/generator delta to
strict-test alias followup `167d4b0e4eceeae82ab42d0aa7dd2962e40a5a8d` is empty.
The two .NET 10 core flags can proceed to fresh strict producer and actual
package gates. This is source readiness, not the final package compatibility
decision or acceptance completion of the 43 capability rows.

The [manifest](evidence/2026-10-06-final-source-readiness.json) pins all 102 shared
core, 35 generator and ten configuration/producer-contract files, both complete
public baselines, and the plan/ledger used for coverage. Each baseline contains
451 explicit public/protected declarations, 32 RUC declarations and zero RDC
declarations; complete paired baselines normalize identically. Both flags remain
conditioned on inner `TargetFramework == net10.0`. Exact source copies remain in
the audit worktree's `artifacts/aot-surface-audit/final-source-readiness`.

The source coverage map accounts for all 43 unique IDs with existing code,
permanent regression references and usable typed alternatives where automatic
behavior needs an application contract. It does not count file existence or
focused compiler results as final acceptance. Silent changes use snapshot/manual
invalidation, opaque effects use typed dependency/observable factories,
unnameable/private or generic contexts use legal typed access, immutable/stack
storage uses replacement/cell/lens or synchronous snapshot, and precompiled or
runtime selections use deliberate finite typed registration. No mandatory row is
replaced by a diagnostic alone. Pinned producer/XAML/provider integration remains
distinct from new native UI or additional host support.

## Delta from the prior checkpoint

The [prior source preparation](FinalIntegratedSourcePreparation.md) remains
pinned to `c1ec40d`; its nullable-flow followups are now integrated. Fifteen
product/generator files differ, including three shared core files and the paired
public baselines. The three additive resolver overloads account for the increase
from 448 to 451 explicit declarations per flavor; the existing target resolver
also gains a MethodImpl annotation.

| Changed route | Source operation and audit result |
| --- | --- |
| Selector/path/target resolver fallback overloads | Exact typed provider/catalog lookup precedes a supplied compiled descriptor. Only genuine no-match selects that fallback. Provider exceptions, ambiguity, success-null and matched metadata-reader failures propagate. No reflected or interpreted fallback is introduced; original expression generic slots and current receivers remain intact. |
| Generated binding fallback | Familiar source/metadata/target calls construct cold typed descriptors and pass them to those overloads. Registered custom policy remains preferred. Metadata fallback has its separate path reader and never acquires a selected leaf merely to identify a property. |
| Output refresh and target handoff | A private typed invalidation adapter joins the same access observation engine. Each output refreshes current target/keys/dependency owners before assignment; notification adapters additionally permit immediate replacement replay. Cold factories, ordinary/AfterRead phases, owner/slot identity and owned rollback/cleanup remain explicit. This deliberate refresh contract does not discover an unnotified mutation between outputs. |
| Accessor null flow | Actual property/field/getter-return MaybeNull/NotNull postconditions are copied uniquely to generated read signatures; field extern/ref and wrapper contracts agree. DisallowNull remains an input annotation on the original nullable CLR value signature, including index operands and Nullable<T>. Getter output and setter input are separate contracts; forbidden nullable output requires an explicit typed policy. No invented null guarantee, reflection root or warning suppression is added. |
| Finite generic selector instantiation | Roslyn speculative binding substitutes original declaration type slots and verifies physical member/operator identity, constant values, checked/lifted semantics and contextual conversions. Enum/default/params operands retain their exact typed constants, `nameof` retains its original constant, and generic reference equality is not reinterpreted as string equality. Roslyn symbol construction happens during compilation; it is not runtime MakeGenericType or expression evaluation. |
| Typed semantic read emission | Operation-keyed cached indexes/receivers preserve evaluation order and branch activation. Generated code uses direct C# operations and static accessor calls. Actual DLR dispatch, opaque invocation effects, polymorphic static selector dispatch and unsafe mutable/escaped provenance require the implemented typed factory/catalog route. There is no expression interpreter or blanket fallback. |

The full source scan and body review find no expression compilation/interpreter,
runtime activation/generic construction, hidden reflected member/value access,
IL-warning suppression or reflection roots added to safe routes. Exact static
UnsafeAccessor/UnsafeAccessorType signatures remain selected compile-time
metadata, not runtime type lookup. Existing style/performance suppressions do
not suppress IL diagnostics.

The 32 public RUC boundaries still describe retained expression component
constructors and explicit Unsafe observation/assignment. Normal typed rule,
component, registry/provider, binding and formatter routes do not enter those
helpers. The actual Binding setter still needs RUC for reflected assignment
without runtime code generation; final isolated setter diagnostics must report
IL2026 and reject a false IL3050. No new RUC/RDC requirement was found.

## Remaining evidence gates

This audit ran no .NET build or new product/dependency byte inspection. Root and
workstream strict/focused results retain their own exact manifests; the source
audit does not relabel them as its execution evidence. The source keeps matching
.NET 10 ReactiveUI 26.0.1/Binding 9.1/Primitives 9/Splat 21 and released
Runic.DynamicData `.30` flavors. Historical `.5` and `f960c4b` bytes stay separate.

Before the final AOT/package decision, strict producers/full tests/examples and
actual shipping packages must be frozen together. The verifier must identify the
exact packed/restored core, analyzer, build assets and dependency DLL bytes. The
framework-only PE inspector can then reconfirm annotations/compatibility metadata,
typed OAPH and the selected normal/Unsafe/DI/task/scheduler routes without
loading target assemblies. Complete generated semantic/byte inventories and
actual same-package managed/full-trim/native execution on Linux and Windows x64,
with precise Unsafe negatives and matching graphs, remain mandatory. Any later
affected source/dependency change needs its scoped delta rechecked.
