# Concrete built-in attachment dispatch source delta

No new AOT/trim source blocker was found in isolated runtime candidate
`532ffc5ae31de729653560349ff789066c16dec7` against integration
`55156e3bbd89c430c56f22dc48c472c3ebd520b2`. This is a source-only update to the
[final source readiness](FinalSourceReadiness.md) at report commit `79aabede`.
The actual integration merge, old/new product IL and final package/host gates
remain separate evidence requirements.

The [manifest](evidence/2026-10-06-attached-registry-source-delta.json) compares
all 149 prior inventory entries: 102 shared core, 35 generator, ten configuration
and producer-contract files, and both complete public baselines. All 149 base
entries match the audited `ccd5aad` source bytes. The candidate changes exactly
one product file, `ValidationPlanRegistry.cs`; the other 148 entries match.
Its new Git blob is `86545e9666d627496b47b3aa0a71a5ad888768a0` and SHA256 is
`338f9e4e349ae2295c7803d277439c1d9b3ea1b174bf401c5179422f551fa3de`.
Exact changed and selected reachable source copies remain in
`artifacts/aot-surface-audit/attached-registry-source-delta` in the audit worktree.

## Source operation and boundary

`ValidationPlanRegistry.Attach` already stores only `this`, whose type is the
sealed built-in registry. The private attachment now retains that concrete type,
and internal `TryGetAttached` returns `ValidationPlanRegistry?` instead of
`IValidationPlanProvider?`. The unchanged runtime locals consequently select
the registry's concrete generic `TryGetSelector`/`TryGetTarget` methods at three
sites: `TryResolveSelectorOn`, `TryResolveTargetCore`, and `ObserveModelReads`.
This makes the intended built-in dispatch explicit in the source type contract;
fresh emitted IL still needs its independent check.

Each site retains its earlier custom `IValidationPlanProvider` branch, with the
same precedence, requests, expression type slots and successful-null handling.
The public provider interface and public registry APIs are unchanged. Weak owner
reference identity, locking, bounded capacity, association leases, snapshot
lookup, ambiguity-before-factory checks and descriptor ownership are unchanged.
No extra getter evaluation, reflection, expression execution/interpreter,
runtime activation, generic construction, reflection roots or IL-warning
suppression is introduced.

Both complete public baselines are byte-identical to the prior readiness report:
451 explicit public/protected declarations, 32 RUC declarations and zero RDC
declarations per flavor, with full normalized parity. The source's 41 RUC
annotation uses are unchanged. No new RUC/RDC boundary was found. Both core
`IsAotCompatible` flags remain conditioned on the inner `net10.0` target;
generator, build assets, dependency graph configuration and exposed API remain
unchanged.

The candidate also adds a managed attachment/closed-slot/lease regression and a
framework-inspector output checker with synthetic positive/negative controls.
Those are test/gate source, not additional product execution. The checker
requires a concrete generic registry call and the existing custom-provider
interface call at all three sites in both flavors. It is explicitly a dispatch
shape check. It cannot establish a compiler crash cause or replace the package
and native gates.

## Remaining gates

This audit ran no .NET build, test, target assembly load, new product/dependency
byte inspection or package/native execution. The verifier is separately
preserving the old product DLLs and recording the old/new IL check. The exact
Roslyn fatal cause remains unproven; neither this source delta nor a successful
dispatch check proves that cause or completion of the affected compiler gate.

After the actual merge, the final source inventory must reconcile any additional
changes. Fresh frozen strict producer/full-suite/example checks, exact shipping
package/restored core/analyzer/dependency byte and annotation inspection, and
same-package managed/full-trim/native execution on Linux and Windows x64 remain
mandatory. Accurate explicit Unsafe negatives remain part of those gates.
Historical package/source checkpoints retain their own bytes and scope. Final
AOT/package compatibility and acceptance of all 43 rows remain pending.
