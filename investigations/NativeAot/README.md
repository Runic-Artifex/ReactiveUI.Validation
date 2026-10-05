# Opt-in package-only NativeAOT investigation

This is a diagnostic consumer, outside the shipping solution/CI. It references
immutable released packages, never sibling project references. On 2026-10-05 the
investigation started at maintained source `2ba57c537172d841cdc545ef9cd255f1e41a4af8`;
the tested Validation packages are release `8.1.0-runic.0.790.17`, released source
`c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`. Both projects explicitly pin matching
ReactiveUI 26.0.1 and Runic.DynamicData 10.0.0-runic.5 flavors. No production
annotations, APIs, package pins, or library implementation are changed.

## Reproduce

Provide directories containing the two released Validation packages and matching
DynamicData pair. The repository bootstrap can populate the DynamicData feed;
use the verified helper below to download Validation from its immutable GitHub
release (or reuse an existing verified feed with `--reuse FEED`). Run inside the pinned SDK
10.0.401 environment. On this NixOS workspace:

```sh
# From the repository root, materialize pinned immutable feeds.
direnv exec "$RUNIC_SDK" python3 eng/restore-fork-dependencies.py
direnv exec "$RUNIC_SDK" python3 investigations/NativeAot/restore-validation-feed.py \
  investigations/NativeAot/artifacts/released-validation
VALIDATION_FEED="$PWD/investigations/NativeAot/artifacts/released-validation"
DYNAMICDATA_FEED="$PWD/artifacts/dependencies"
# Absolute SDK/feed paths are inputs, not committed machine-specific defaults.
direnv exec "$RUNIC_SDK" investigations/NativeAot/run.sh \
  "$VALIDATION_FEED" "$DYNAMICDATA_FEED" all linux-x64
# Individual phases: managed, trimmed, native.
direnv exec "$RUNIC_SDK" python3 investigations/NativeAot/summarize.py \
  > investigations/NativeAot/artifacts/diagnostics.json
```

On a correctly configured non-Nix .NET/native build environment, invoke `run.sh`
directly. The RID argument selects publish RID; running the produced binary
requires a host matching that RID. Windows NativeAOT requires its documented
native toolchain and a Bash/Python runner; this investigation has verified only
Linux x64. Do not interpret the parameter as a cross-platform verification claim.

The runner serializes the two flavor builds, uses `-m:2` and native compiler
parallelism 1 (the SDK-supported `IlcSingleThreaded=true` switch), emits all publish diagnostics, and runs actual executable artifacts.
Native mode asserts `RuntimeFeature.IsDynamicCodeSupported == false`. Managed
and trimmed modes report its value. The investigation `.editorconfig` changes
IL2026/IL3050 from this repository's error severity to warnings so diagnostic
publishes finish; messages are retained without suppression, preservation
annotations, complete-metadata mode, linker descriptors, or assembly roots.
`TrimmerSingleWarn=false` retains detailed diagnostics. `summarize.py` counts
occurrences by compiler/linker/AOT stage and emitting source; repeated diagnostic
stages are not independent defects. Annotated `Requires*` boundaries can hide
callee analysis; no dependency warnings is not a transitive safety certificate.

## Scenarios and evidence

Each binary runs independent scenarios and reports JSON Lines plus exception
stacks in ignored `artifacts/`. Scenarios assert actual state/ownership behavior:

- Custom `IValidationComponent` and `ValidationContext` membership, initial valid
  state, invalid/valid transitions, message-only updates and source removal.
- `ObservableValidation` from a raw `IObservable<IValidationState>`, without
  expressions, and plain observable `ValidationRule` plus helper disposal.
- Expression `ValidationRule` and `ReactiveValidationObject.HasErrors`/property
  `GetErrors` transitions.
- Legacy helper `BindValidation` and typed bool helper `BindValidationState`,
  with model/helper replacement, null transitions, stale source detachment,
  repeatable disposal and no updates afterward.
- Selected `BindValidationContext`, context replacement/null/model transitions
  and disposal.
- Direct context observation with a statically compiled setter. This is a manual
  prototype of a generated adapter boundary; it is **not** generated code.
- Legacy and typed bindings to a separate view whose target setters have zero
  ordinary call sites. Constructors initialize private backing fields; assertions
  only read getters/counters. This detects trimming of reflection-only setters
  that would be masked by the earlier view's ordinary writes.

The combined program deliberately includes all scenarios as reachable entrypoint
calls; it establishes behavior for these exact application shapes. It does not
prove arbitrary expression/generic/private/nested targets, UI platform services,
all DynamicData operators, cold-start costs, or library-wide warning-free AOT.
The dedicated reflection-only targets prevent ordinary static target writes from
providing preservation accidentally. `RxAppBuilder.WithCoreServices` initializes
matching ReactiveUI property observation services before execution.

`*.build.log`, `*.publish.log`, `*.runtime.jsonl`, and `*.runtime.log` are retained
locally under ignored `artifacts/`; the human-readable results below and [compact result evidence](evidence/results.json)
are committed. Raw build logs stay local because they include machine paths.
Delete task-owned publish directories once no longer needed, preserve useful
logs and shared NuGet/Nix caches. NuGet restore reuses the locked SDK shell cache.

## Tested immutable assets

| Package | SHA-256 |
| --- | --- |
| Runic.ReactiveUI.Validation 8.1.0-runic.0.790.17 | `5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df` |
| Runic.ReactiveUI.Validation.Reactive 8.1.0-runic.0.790.17 | `e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b` |
| Runic.DynamicData 10.0.0-runic.5 | `ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366` |
| Runic.DynamicData.Reactive 10.0.0-runic.5 | `49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268` |

Verified native toolchain: SDK 10.0.401, locked Runic SDK Nix shell, Clang 21.1.8,
zlib 1.3.2, Linux x86_64. No guessed store paths or global tools were installed.

## Linux x64 results, 2026-10-05

Both flavors passed all ten scenarios in managed, fully trimmed self-contained,
and NativeAOT execution. Native binaries asserted dynamic code unsupported;
managed/trimmed binaries reported dynamic code supported. Public simple string
and bool setters with no direct write sites also survived and updated correctly.
No run had an exception. This is a successful warning-bearing experiment,
not a warning-free supported API contract.

| Phase (each flavor) | Compiler diagnostics | Link/native analysis diagnostics |
| --- | --- | --- |
| Managed | 0 | Not applicable |
| Fully trimmed | 17 × IL2026 | 17 × IL2026 |
| NativeAOT | 17 × IL2026; 7 × IL3050 | 17 × IL2026; 8 × IL3050 |

Each NativeAOT log contains 49 warning occurrences across stages; the 25 native
analysis occurrences are 17 consumer IL2026, 7 consumer IL3050, and 1 IL3050
originating in Validation's `BasePropertyValidation` constructor. The remaining
24 are compiler consumer diagnostics repeated later by native analysis. No
DynamicData or Binding-owned warning appears in this graph, but Validation's
`Requires*` boundaries prevent treating that absence as analysis of all internals.

The minimal tested implementation shape is an explicit state observable/custom
component added to the existing context, plus a direct subscription with a static
setter (and a disposable owner). That shape removes expression observation and
reflection assignment in an app adapter. Existing Context/Helper constructor
annotations still warn. Producer changes to annotations or a supported new adapter
API would need focused API/behavior review and independent trim/native gates;
this experiment does not authorize those changes.
