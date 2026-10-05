# Binding generator investigation

Date: **2026-10-05**. This opt-in directory does not alter shipped APIs,
dependencies, solutions or release gates. It is a console package-consumer proof
for Runic SDK model/adapter integration, not GUI-platform or WebUI verification.

## Inputs and reproduction

Investigation base: `2ba57c537172d841cdc545ef9cd255f1e41a4af8`. Released validation
pair: **8.1.0-runic.0.790.17**, source
`c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`. SDK **10.0.401**, `net10.0`, C# 14.
Isolated central pins preserve ReactiveUI **26.0.1** and matching released
Runic.DynamicData **10.0.0-runic.5**, without touching `src` pins. Package hashes
and exact restored closures are in [evidence/pins.json](evidence/pins.json) and the
two `*-graph.json` files.

Run the dependency bootstrap in the locked shell, and supply a repository holding
the verified published pair, rather than a new local pack with another version:

```sh
direnv exec /home/viktor/Development/RunicArtifex/runic-sdk python3 eng/restore-fork-dependencies.py
direnv exec /home/viktor/Development/RunicArtifex/runic-sdk python3 investigations/BindingGenerators/verify.py \
  --feed-root /home/viktor/Development/RunicArtifex/ReactiveUI.Validation
```

On other machines, run Python and dotnet from the locked project shell. The
wrapper accepts any `--feed-root` holding `artifacts/packages` and
`artifacts/dependencies`; it creates a temporary mapped NuGet config, restores
exact versions, runs bounded sequential `-m:2` managed builds, and checks graph
identity. Logs/graphs/generated-source snapshots remain under
`artifacts/BindingGenerators`; extracted package DLLs/config are removed afterward.
`--retain-evidence` refreshes this directory's dated evidence for review. No native
publication or shipping build runs in this wrapper.

Direct builds use the adjacent portable `nuget.config`, defaulting to this
repository's feeds. Ordinary proofs reference only released Validation and matching
DynamicData. Optional `-p:UseReactiveUIReference=true -p:UseVmGenerator=true` adds
explicit same-version ReactiveUI and private SourceGenerators references; neither
is needed for this cohort's transitive generator result.

## Actual results

Both managed flavors built with **zero warnings/errors** and passed:

* `[Reactive]` writes model, view and nested/generic properties.
* App-local `WhenAnyValue(x => x.Name)` observes initial/changed values; an
  observable validation rule consumes that generated observation.
* `BindTo(target, x => x.IsValid)` emits a real intercepted setter and receives
  invalid/valid rule states.
* Released `BindValidationState` typed callbacks follow replacement models, detach
  old models, and use their documented valid fallback on null. This custom
  validation call is **not intercepted**; the proof checks managed compatibility.
* Private nested types in a **partial** calling container, a nested property chain,
  and closed/open generic selectors produce generated observation and pass. The
  private chain emits `one,two`; a null link does not synthesize a null terminal
  value in this observation form.

Actual snapshots under [evidence/Primitives](evidence/Primitives) and
[evidence/Reactive](evidence/Reactive) contain `InterceptsLocation`,
`__Intercept_WhenAnyValue`, `__Intercept_BindTo`, direct getters/setters, and hosted
helpers inside the partial container. Unsuffixed Binding runtime methods throw
when no generated claim exists, so successful calls plus emitted interceptors
establish more than analyzer installation.

Generated observation still consults higher-affinity runtime plugins when present;
interception does not imply that every platform/plugin branch avoids reflection.
These probes exercise the core notification mechanism, not a GUI plugin matrix.

`*-compiler.txt` records fresh Csc inputs: Binding **9.1.0 / roslyn4.13** and
SourceGenerators **4.2.0 / roslyn5.0**, with the interceptor namespace enabled by
package targets. Both run with **no direct ReactiveUI/SourceGenerators references**.
The umbrella nuspec's `exclude="Build,Analyzers"` did not prevent these assets being
used in the actual restored graph. Compiler input evidence takes precedence over
an inference from that line alone.

An additional fresh restore/rebuild with `-p:PinVmGenerator=false` removed the
SourceGenerators central-version promotion too, and still loaded both analyzers
and compiled the generated properties. The compact unpromoted compiler record is
retained separately; this rules out the central SourceGenerators pin as the cause
of generator availability.

| Opt-in negative constant | Observed diagnostics |
| --- | --- |
| `NEGATIVE_SELECTOR` | An expression stored in a variable produces **RXUIBIND001** and **RXUIBIND021**; a generated call needs an inline selector. |
| `NEGATIVE_ANONYMOUS` | This anonymous read-only source without notifications produces **RXUIBIND002** and **RXUIBIND015**. This does not characterize every inaccessible/anonymous case. |

The wrapper expects both failed builds and then restores the baseline output.
The failing source forms remain behind conditional compilation; they were not
silently replaced with Unsafe calls.

[InspectShipping](InspectShipping/Program.cs) reads PE metadata/IL without loading
or executing its target assembly. For DLLs extracted from the **published .790.17
pair**, two `ValidationHelper` and two `ValidationContext` constructor calls per
flavor already target `__ReactiveUIGeneratedBindings.__Intercept_ToProperty_*`.
[evidence/released-oaph-il.txt](evidence/released-oaph-il.txt) records all eight
calls. Adding a generator cannot newly fix these already-generated OAPH sites.

## Capabilities and recommendation

| Capability | Current status and value |
| --- | --- |
| `ReactiveUI.Binding` / `.Reactive` runtime | Already used through ReactiveUI 26.0.1. Supplies `IViewFor`, OAPH, observation/converter infrastructure, generated support types and explicit runtime Unsafe twins. No new core dependency is necessary. |
| `ReactiveUI.Binding.SourceGenerators` | Already bundled in Binding 9.1.0 and active transitively. Required machinery for normal unsuffixed Binding calls; useful for app/model inline selectors and `BindTo`. An additional direct package is unnecessary here. |
| `ReactiveUI.SourceGenerators` | Already active transitively at 4.2.0. Optional model/command boilerplate reduction via `[Reactive]`, `[ReactiveCommand]`, `[IReactiveObject]` and collection attributes. Does not generate validation rules or their lifetime semantics. |
| `[ObservableAsProperty]` / view generation | In the current 4.x split these belong to Binding, not SourceGenerators. OAPH generation and VM-property generation are distinct operations. |

Use these capabilities **consumer-first** in Runic SDK models/adapters: concrete
inline selectors, generated properties, observable rules with actual initial
values, and typed callback/direct observable projection at the adapter boundary.
Preserve the existing context owner/dispatcher contract. Generated command
admission should consume current domain validity and existing explicit guards;
property generation does not turn scheduled presentation state into same-turn
validity.

This is optional ergonomic work, not a reason to upgrade the core cohort, add a
shipped dependency, or declare all validation APIs AOT-safe. `ValidationRule`,
`BindValidation`, `BindValidationState`, explicit-context variants and generic
compiled internals are not recognized Binding generator methods. App generators
cannot intercept methods already compiled inside released DLLs. Internal
`WhenAnyValueUnsafe`, `BindToUnsafe`, expression variables and reflection assignment
retain their existing limits. Private partial hosted interception solves a concrete
app-visibility case; it does not solve arbitrary selectors in generic library APIs.

A future supported AOT-oriented path would require separate design/review:
observable-supplied observations, stable property identity without reflective
access, target setter/action delegates, and explicit replacement/null/disposal/
strict-matching contracts. A validation-specific generator could translate public
calls into those factories, at the cost of compiler integration, diagnostics,
cross-generator inference, accessibility handling, generated-source QA and a
two-flavor consumer/AOT matrix. Merely installing the current generators cannot
cover custom validation calls. Broad dynamic-code annotations may overstate some
implementations, but removing them needs separate transitive trim/AOT evidence.

Local IL2026/IL3050 severities are **warnings**, not suppressions.
`--require-aot` rejects managed runtimes and prints `dynamicCodeSupported`. Native
publication was performed sequentially by the separate NativeAot investigation.
Both actual **linux-x64 NativeAOT** executables passed `--require-aot`, reported
`dynamicCodeSupported=False`, and passed the same scenarios. Compact runtime logs,
warning lines and source hash are in `evidence/*-native-*` and
[evidence/native-summary.json](evidence/native-summary.json). Each flavor retained
six IL2026 and four IL3050 warning lines (compiler plus native analysis), at the
consumer's annotated validation-call boundaries. There were no roots or warning
suppressions. Passing these bounded paths is not a general NativeAOT-support claim,
and absent dependency-owned diagnostics do not prove every internal path safe.

Portable publication recipe, after preparing an explicit mapped feed config and
ensuring only one native compile runs at a time:

```sh
# Set task-specific paths to this checkout/project, feed config, and output.
dotnet publish "$probe_project" -c Release -r linux-x64 -m:2 \
  --configfile "$probe_feed_config" -p:PublishAot=true -p:TrimmerSingleWarn=false \
  -p:IlcSingleThreaded=true \
  -p:BaseIntermediateOutputPath="$probe_obj/" \
  -p:CompilerGeneratedFilesOutputPath="$probe_obj/generated" -o "$probe_output"
"$probe_output/BindingGenerators.Primitives" --require-aot
# Repeat for Reactive with separate object/output paths and executable name.
```

Use the locked Runic SDK shell on NixOS. `CompilerGeneratedFilesOutputPath` must not
end in a slash for this compiler. The publisher used isolated object output for
Reactive; Primitives completed before managed verification resumed. Full logs
remain with the NativeAot investigator's retained artifacts.
The recorded native runs passed `IlcMaxDegreeOfParallelism=2`, but the publisher
subsequently verified that NativeAOT 10.0.12's build targets ignore that property. The recipe
above uses the supported `IlcSingleThreaded=true` switch; the recorded publication
was sequential with `-m:2` and used the compiler's default internal parallelism.

## Primary sources checked 2026-10-05

* [Binding 9.1.0 release](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/releases/tag/v9.1.0), source `05c45cec845835d670960fac733bb302bc1c1071`.
* [SourceGenerators 4.2.0 release](https://github.com/reactiveui/ReactiveUI.SourceGenerators/releases/tag/v4.2.0), source `c8a8c38dd7192073540e187b8e8abc8d53e16b1a`.
* [Binding method recognition at the pin](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.SourceGenerators/RoslynHelpers.cs).
* [Binding compiler selection at the pin](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.SourceGenerators/build/ReactiveUI.Binding.SourceGenerators.targets): Roslyn 4.8 minimum, interception slot starts at 4.13 and package targets enable it.
* [SourceGenerators flavor inference](https://github.com/reactiveui/ReactiveUI.SourceGenerators/blob/c8a8c38dd7192073540e187b8e8abc8d53e16b1a/src/ReactiveUI.SourceGenerators.Roslyn/Core/Extensions/ContextExtensions.cs): recognizes `ReactiveUI.Reactive.ReactiveCommand`, without a guessed flavor-switch property.
* [Official generated-member integration](https://www.reactiveui.net/documentation/binding/source-generators/): generators run independently; Binding understands supported VM attributes and provides `ObservedProperty` for generated callers it cannot inspect.

Reactive imports `ReactiveUI.Reactive`, `ReactiveUI.Binding.Reactive`,
`ReactiveUI.Validation.Reactive.*` and System.Reactive operators. Primitives
imports `ReactiveUI`, `ReactiveUI.Binding`, `ReactiveUI.Validation.*` and Primitives
operators. Both share `ReactiveUI.SourceGenerators` attribute names, with flavor
inferred from references. Both graphs exclude upstream DynamicData; Primitives
excludes System.Reactive.
