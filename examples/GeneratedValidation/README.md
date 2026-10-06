# Generated actual-package acceptance

This unreleased corpus exercises the familiar `ValidationRule`, `BindValidation`,
`BindValidationState` and `BindValidationContext` syntax from an independent
application. Both flavor projects compile the same source and consume the real
packed Runic Validation pair. They do not reference production projects or add a
separate generator package: the analyzer and transitive interceptor namespace
allowlist must arrive inside each shipping package.

The original cases cover initially invalid single fields; all four predicate
rule, six text binding, four selected-context binding and four typed-state binding
overloads; nested nullable address paths; view model, helper and context
replacement/null/disposal; typed rich/null output; original boxed struct state
identity and message-equal metadata changes; dynamic property membership and
strict versus cross-field selection; and nested target handoff with latest-value
replay, null targets and equal-overriding target replacements. The existing
observable and metadata-only overloads remain normal calls. Metadata-only paths
include a nonnotifying parent because their values come from a supplied stream.

The row case uses the existing observable API for application-owned asynchronous
result delivery. Disposing a removed row releases the validation subscription and
late results cannot affect current membership. The generator does not infer
cancellation, launch asynchronous work, or own the application's request lifetime.

The expanded corpus includes explicit latest-request cancellation and owner delivery,
row leases and source replacement, domain/presentation scheduling, nullable output
factories and synchronous stack snapshots. It also defines all eleven static
binding factories, ordered raw/custom nullable formatter projections, typed
formatter registrations, selected init/readonly/private access, immutable storage
alternatives, direct typed component constructors and custom observation providers.

Each flavor also builds a separate precompiled caller DLL against the actual
package before compiling the application. The application consumes its binary
reference. The peer creates fresh expressions with different current receivers
and live typed captures and exposes a real normal API method group. Runtime cases
require finite registration, reject ambiguity and missing/unregistered plans, and
check detached callback lifetimes. Compiler replay records these calls as explicit
`RuntimeRegistered` dispatch; they do not satisfy the normal generated inventory.

The legacy ABI lane uses a second pair of peer DLLs, compiled once against an
earlier package pair (`8.1.0-runic.0.790.17.15.10`, DynamicData `.5`) and kept
unchanged in `eng/verification-fixtures/LegacyPeer`. That compilation excluded
the old Validation analyzer, so its IL retains the original API invocation and
method group. The current host supplies the finite
typed catalog and links those unchanged binaries into every managed, trimmed and
native consumer. The gate checks their source, compiler manifest, baseline package
hashes and actual original-call IL separately from the current-package peer.

`Negative.cs` contains six genuine compile-negative configurations: an opaque
runtime selector without deliberate registration, unmarked init/readonly targets,
a getter-only target without a writable descriptor, an invalid output-to-storage
assignment, and a concrete value-copy view without stable reference ownership.
They require actionable typed selector/target or registered alternatives. The
current Validation Unsafe observer and Binding reflection-only setter separately
require precise `IL2026` and reject invented `IL3050`. Every configuration gets a fresh consumer folder.

From the repository root, with the pinned SDK/native toolchain loaded:

```sh
python3 eng/restore-fork-dependencies.py
python3 eng/verify-generated-validation.py --rid linux-x64 --mode all --package-feed artifacts/packages
```

Use `win-x64` only on Windows x64. Without `--package-feed`, the gate packs the
current source itself. The strict gate requires a clean committed checkout and
packages recording that exact HEAD, verifies SHA-256 package bytes before and
after restore, checks every neutral/RID target for exact flavor versions, rejects
upstream DynamicData and System.Reactive in Primitives, replays exact compiler inputs and final semantic dispatch for all 29 shipping
normal overload definitions, and builds/publishes/runs managed, fully trimmed and NativeAOT
consumers with warnings as errors and no suppressions. Native binaries also
assert dynamic code is unavailable. It retains logs, graphs and a source/package
report, every Validation-owned emitted source, exact original source/analyzer/reference
hashes, generic/hosted declaration signatures, and typed overload inventory under `artifacts/verification/generated-gates`; task-owned temporary
consumers are removed automatically while its package cache is reused.

The CI native matrix executes all three modes on Linux x64 and Windows x64 using
the same Linux shipping package artifact. Release requires both complete reports
at the release SHA. The explicit-observable `NativeValidation` corpus has its own
gate.

The same application also links the maintained rule, real producer, error-hook and
generated-peer scenarios from `eng/verification-fixtures`. The gate builds the
private `PeerGeneratedCaller` analyzer once and records its bytes and emitted
caller source. Direct example builds require that tool DLL through
`PeerGeneratedCallerAnalyzerPath`; it is an analyzer input and never a runtime
reference. The [capability reference](../../docs/generated-capabilities.md) maps
the 23 application cases to capabilities; platform producer/UI contracts are
covered by compiler tests only.
SDK compiler replay retains the effective CoreCompile warning defaults and rejects
any additions to the three verified .NETCoreApp defaults (1701, 1702, 8002).
