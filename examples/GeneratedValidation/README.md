# Generated actual-package acceptance

This unreleased corpus exercises the familiar `ValidationRule`, `BindValidation`,
`BindValidationState` and `BindValidationContext` syntax from an independent
application. Both flavor projects compile the same source and consume the real
packed Runic Validation pair. They do not reference production projects or add a
separate generator package: the analyzer and transitive interceptor namespace
allowlist must arrive inside each shipping package.

The seven reported cases cover initially invalid single fields; all four predicate
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

`Negative.cs` contains isolated compile-negative configurations for stored
expression variables, computed selectors, indexers, observed nonnotifying and
struct owners, and a nested writable target with a nonnotifying parent. Unsupported calls must emit actionable generator
errors recommending an explicit observable boundary or deliberate `Unsafe` API.
Each negative configuration is compiled separately in a fresh consumer folder.

From the repository root, with the pinned SDK/native toolchain loaded:

```sh
python3 eng/restore-fork-dependencies.py
python3 eng/verify-generated-validation.py --rid linux-x64 --mode all --package-feed artifacts/packages
```

Use `win-x64` only on Windows x64. Without `--package-feed`, the gate packs the
current source itself. The strict gate requires a clean committed checkout and
packages recording that exact HEAD, verifies SHA-256 package bytes before and
after restore, checks every neutral/RID target for exact flavor versions, rejects
upstream DynamicData and System.Reactive in Primitives, verifies emitted
interceptors, and builds/publishes/runs managed, fully trimmed and NativeAOT
consumers with warnings as errors and no suppressions. Native binaries also
assert dynamic code is unavailable. It retains logs, graphs and a source/package
report and validated emitted interceptor sources with SHA-256 hashes and typed
overload inventory under `artifacts/verification/generated-gates`; task-owned temporary
consumers are removed automatically while its package cache is reused.

The CI native matrix executes all three modes on Linux x64 and Windows x64 using
the same Linux shipping package artifact. Release requires both complete reports
at the release SHA. These workflow requirements are not evidence of a completed
run; verification records must identify the executed source and package bytes.
The historical `NativeValidation` corpus and immutable-release expectations remain
separate.
