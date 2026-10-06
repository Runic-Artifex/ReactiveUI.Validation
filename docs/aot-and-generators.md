# NativeAOT and ReactiveUI generators

This page explains how Validation behaves under trimming and NativeAOT, how it
relates to the ReactiveUI generators, and how the native package gates verify it.

## Support summary

| Route | Trimming / NativeAOT |
| --- | --- |
| Generated normal calls (`ValidationRule`, `BindValidation*`, `ValidationBinding.For*`) with an inline lambda | Warning-free. Interceptors replace the call with direct typed getters, notifications and setters. Unreleased. |
| Typed capabilities (`ValidationSelector`, `ValidationTarget`, `ValidationCell`, `ValidationLens`, registered plans) | Warning-free; no member discovery, no expression compilation. Unreleased. |
| Explicit observable APIs (`AddObservableRule`, `BindObservableValidationState`, `BindObservablePropertyValidationState`) | Warning-free; the caller supplies streams and callbacks. Released in `8.1.0-runic.0.790.17.15`. |
| Supplied-observable/metadata-only rule overloads, `ObserveFor`, `ContainsProperty` | Expression used as metadata only; no reflected access. |
| `*Unsafe` methods and expression-based component constructors | Reflected observation/assignment; `RequiresUnreferencedCode` (IL2026). |
| Retained `Expression` argument that needs dynamic construction (for example a params indexer) | IL3050 at the caller; analyzer warning `RUVG011`. Use the `Func` overload. |

The core .NET 10 targets set `IsAotCompatible` and enable the AOT and trim
analyzers. That flag can coexist with correctly annotated Unsafe APIs; it does
not mean every legacy API is safe. `RequiresDynamicCode` warns that runtime code
generation may be needed; `RequiresUnreferencedCode` warns that trimming may
remove members. A successful publish of one program does not remove those
contracts for other inputs. See the
[NativeAOT overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/),
[IL3050 guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/il3050)
and [trim-warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings).

Published releases before the generator split expose the explicit observable
APIs as their safe route; their expression rule and binding APIs carry
`RequiresUnreferencedCode`. The [runtime recipe](examples/native-validation.md)
shows that route. The generated route is described in the
[design](generated-validation-design.md) and
[capability reference](generated-capabilities.md).

## The three ReactiveUI integration points

`ReactiveUI.Binding` (and `ReactiveUI.Binding.Reactive`) is runtime binding
infrastructure already in the ReactiveUI 26.0.1 graph; both Validation projects
use its observation helpers. Its analyzer assets
(`ReactiveUI.Binding.SourceGenerators` 9.1.0) arrive transitively. Its
interceptors rewrite recognized `WhenAnyValue`/`BindTo` calls in the
*consuming* compilation; they cannot rewrite calls inside the compiled Validation
DLL. Validation's context and helper OAPH `ToProperty` calls are themselves
intercepted when Validation is built, so the package does not call
`ToPropertyUnsafe`. Removing those analyzer assets from the Validation build
would leave the unsuffixed runtime stubs.

`ReactiveUI.SourceGenerators` (4.2.0) generates view-model boilerplate:
reactive properties, commands and notification members. In the 4.x split,
`[ObservableAsProperty]` and view generation belong to Binding. Neither generator
implements Validation rule selection, registration, binding lifetime or
assignment. Validation's own generator sees their output through declared
partial members or its private producer projection
(see [peer-generated members](generated-validation-design.md#peer-generated-members)).
Inspect the actual compiler analyzer inputs when changing the graph rather than
inferring availability from a nuspec entry.

Generators run independently: code emitted by one is not intercepted by another.
The Validation generator therefore emits direct runtime operations rather than
fresh `WhenAnyValue` calls. Binding 9.1.0 handles some calls with types its
generated code cannot name; if an anonymous or private selector still fails in
Binding, reproduce the exact selector and inspect its diagnostics before adding a
Validation workaround.

## Dependency boundaries

The Runic.DynamicData pair enables AOT/trim analysis on its own targets.
Validation's context uses `SourceList.Connect`, `AsObservableList`,
`AutoRefreshOnObservable` with a delegate and `QueryWhenChanged`; it does not use
DynamicData's property-expression refresh or grouping APIs. Binding provider
initialization, ReactiveUI notifications and the matching System.Reactive flavor
still participate in native reachability. Attribute warnings from an unrelated
rooted dependency API to that API, not to a Validation rule; trimming away an
unused API does not certify the whole dependency.

The default formatter lookup asks the resolver for `IValidationTextFormatter<string>`
and falls back to `SingleLineFormatter.Default`. Passing an explicit formatter or
registering one with `ValidationTextFormatterRegistration.Register`/`RegisterFactory`
avoids scanning. An application's own DI registrations or assembly scanning have
their own native requirements.

The Primitives graph must stay free of System.Reactive, and neither graph may
contain upstream `DynamicData` packages. These are package contracts independent
of NativeAOT and are checked by the gates.

## Strict package gates

Two gates consume actual packed packages, keep warnings as errors, check exact
flavor/version graphs and source identity, and run the emitted programs
(native binaries also assert dynamic code is unavailable):

- [`eng/verify-native-validation.py`](../eng/verify-native-validation.py) runs the
  explicit-observable [runtime corpus](../examples/NativeValidation/README.md)
  and checks that the released `8.1.0-runic.0.790.17` baseline still fails its
  strict build with the expected IL2026/IL3050.
- [`eng/verify-generated-validation.py`](../eng/verify-generated-validation.py)
  runs the [generated corpus](../examples/GeneratedValidation/README.md), the
  compiler/configuration controls, the precompiled-caller fixtures and the
  `Expression` construction control.

From the repository root, in the locked SDK/native environment, after
`python3 eng/restore-fork-dependencies.py`:

```sh
python3 eng/verify-native-validation.py --rid linux-x64 --mode all
python3 eng/verify-generated-validation.py --rid linux-x64 --mode all
```

Without `--package-feed FEED` each gate packs the current clean source into an
isolated feed; with it, the gate verifies a prepacked same-source pair (CI passes
the Linux shipping artifact). `--mode` selects `managed`, `trimmed`, `native` or
`all`; `--output` chooses the report directory (defaults under
`artifacts/verification/`). `win-x64` must run on a Windows x64 host. The
[native workflow](../.github/workflows/native-validation.yml) runs both gates on
Linux x64 and Windows x64 against the same Linux package artifact.

## Platform scope

NativeAOT binaries target one runtime identifier and need that platform's native
toolchain. Ordinary build/test success does not stand in for a native publish and
run. Supported native hosts are **linux-x64** and **win-x64**. Linux Arm64, macOS
and cross-compilation need matching toolchains and actual host execution.
AndroidX and desktop/mobile samples remain outside core releases; no Android,
iOS, macOS, desktop UI or bridge/browser support is implied. A platform expansion
must define its RID, UI cohort and native consumer gate under the
[maintenance policy](maintenance.md#validation-and-development-environment).
