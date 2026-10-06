# Real packaged generator interoperability investigation

Investigated 2026-10-06 at investigation base `4ca1ee9`. This opt-in console corpus
changes no production APIs, central pins, solution membership or shipping gates.
It co-runs **unmodified packaged analyzers** from:

- Runic Validation **8.1.0-runic.0.790.17.15.10**, verified source
  `2550376b230ccfb78beb8d3b4ced8866b65d809e`; analyzer Roslyn 5.9 asset.
- ReactiveUI / ReactiveUI.Reactive **26.0.1** and Binding / Binding.Reactive
  **9.1.0**. Binding includes its analyzer and generator (Roslyn 4.13 asset).
- ReactiveUI.SourceGenerators **4.2.0** (Roslyn 5.0 asset).
- Released matching Runic.DynamicData pair **10.0.0-runic.5**, verified against the
  retained historical release hashes; SDK **10.0.401**, C# 14, actual compiler Roslyn 5.9.

The pinned NuGet inputs are real packages. Each temporary application restores
outside the production project graph into an owned reusable cache, validates
exact flavor and version graphs, rejects upstream DynamicData and System.Reactive
in Primitives, and checks restored Validation package bytes against the verified
input feed. It records actual analyzer item order and SHA-256 hashes and preserves
emitted output from all three generators. Analyzer item order documents compiler
inputs; it does not establish a generator dependency or guaranteed execution order.

## Managed results

All eight configurations were built separately for both flavors on Linux x64,
with warnings as errors and trim/AOT analyzers enabled, without suppressions.
The two positive configurations built with zero warnings/errors and executed
successfully. The isolated negatives produced only the documented generator IDs;
no missing attribute, compiler syntax, restore or unrelated analysis failure was
accepted as negative evidence.

| Configuration | Both flavor results |
| --- | --- |
| `PARTIAL` | Pass: `[Reactive]` declared partial model Email, Address/Postcode, helper/context, view model and string/rich targets; Binding `[ObservableAsProperty]` declared get-only bool property initialized by actual generated `ToProperty`. |
| `FIELD_EMAIL` | `[Reactive] _email` producing Email consumed by predicate ValidationRule: RUVG001 + RUVG005. |
| `FIELD_HELPER` | `[Reactive] _rule` producing Rule consumed by typed BindValidationState: RUVG001 + RUVG005. |
| `FIELD_CONTEXT` | `[Reactive] _selectedContext` producing SelectedContext consumed by BindValidationContext: RUVG001 + RUVG005. |
| `FIELD_TARGET` | `[Reactive] _message` producing target Message consumed by BindValidation: RUVG001 + RUVG005. |
| `COMMAND_METADATA` | `[ReactiveCommand] Submit()` producing SubmitCommand used only as a supplied-observable ValidationRule metadata selector: RUVG001 alone. This records a current rejection even though the overload needs no generated observation. |
| `GENERATED_INTERFACE` | `[IReactiveObject]` generates an owner's interface implementation while Email itself is declared: RUVG001 + RUVG005. |
| `FIELD_OBSERVABLE` | Pass: actual Binding `WhenAnyValue(model => model.Email)` predicts a field-produced property and supplies initial/changed values to safe `AddObservableRule` with explicit `Email` metadata. No Validation expression selector is required. |

The positive corpus checks initially invalid rules and typed/string target
assignments, actual generated setter notifications, full nested metadata paths,
nested replacement/null and detachment, OAPH change notification, selected-context
replacement/null/stale subscription detachment, and view-model replacement/null.
The declared-selector supplied-observable control independently passes initial
invalid metadata, caller-driven validity and subscription cleanup without requiring
Validation interception. The field workaround additionally checks initial invalid
field metadata, validity changes and registration removal. It preserves field-based authoring without a
Validation generator change.

The command case exposes early unresolved-selector diagnostics before the safe
overload can be identified. Its failure is not proof that observing a command is
required or that the command cannot be generated. Emitted real SourceGenerators
output is retained independently of Validation's failure.

Both positive configurations also passed **Linux x64 NativeAOT**, separately for
both flavors: four serial warning-free publishes and successful executions with
`--require-aot`. Native publication used full trimming and warnings as errors,
including native compiler/link analysis. Exact analyzer inputs and graphs were
checked; no Roslyn runtime packages or compiler/analyzer compile/runtime assets
entered the application closure. This records managed and fully trimmed NativeAOT
execution; no separate trimmed managed execution or Windows execution was run for
this investigation.

## Reproduce

The reproduction runner now carries explicit historical `.5`/ReactiveUI 26.0.1
pins and `.5` release digests. It checks DynamicData archive bytes in both the
input feed and the restored cache, and does not inherit the current shipping
central pins. Main's later `.30` DynamicData update does not change this dated
research cohort. Original tested runner/fixture bytes and the 20 historical
outcomes are frozen in commit `9ad0d6a`; those historical reports and their
manifest remain unchanged. A subsequent [reproduction smoke](evidence/reproduction-smoke.json)
records only two managed PARTIAL executions with independent revised runner and
fixture hashes after merging the `.30` main revision. It does not repeat or
relabel the original native or full matrix results.

From this worktree root, provide the locked SDK checkout and exact verified feeds:

```sh
direnv exec "$RUNIC_SDK" python3 investigations/GeneratorInterop/run.py \
  --package-feed "$VERIFIED_VALIDATION_FEED" \
  --dependencies-feed "$VERIFIED_DYNAMICDATA_FEED"
```

Use `--case PARTIAL` or another table configuration for focused managed checks.
Native positive publication is an explicit separate run (repeat with
`--case FIELD_OBSERVABLE` for the other positive path):

```sh
direnv exec "$RUNIC_SDK" python3 investigations/GeneratorInterop/run.py \
  --package-feed "$VERIFIED_VALIDATION_FEED" \
  --dependencies-feed "$VERIFIED_DYNAMICDATA_FEED" \
  --mode native --case PARTIAL --output artifacts/GeneratorInterop-native-partial
```

The runner packs no libraries, patches no analyzer DLLs and performs no release
operation. Its temporary consumers disappear at completion; reports, compiler
inputs, full build/runtime logs, graphs and generated snapshots remain in its
owned output. `--cache` may select a dedicated retained cache; do not point it at
an unrelated active task's writable cache.

[Managed report](evidence/managed-results.json) records exact fixture, package and
analyzer hashes and 16 outcomes. [Native partial report](evidence/native-partial-results.json)
and [native field-workaround report](evidence/native-field-results.json) each record
the two flavors' successful native publication and execution. [Summary](evidence/summary.json)
records the modes, inputs and report hashes. Positive generated-source snapshots and runtime
records are retained beside it, as are compact exact diagnostic reasons for each
flavor. [Evidence manifest](evidence/manifest.json) hashes the retained review
files. Full logs remain in `artifacts/GeneratorInterop` from the recorded run.

Primary API references: [ReactiveUI.SourceGenerators 4.2.0 pinned README](https://github.com/reactiveui/ReactiveUI.SourceGenerators/blob/c8a8c38dd7192073540e187b8e8abc8d53e16b1a/README.md),
[Binding 9.1.0 pinned README](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/README.md).
The table above reports this corpus's measured results rather than inferring
compatibility from those documents.
