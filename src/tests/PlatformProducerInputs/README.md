# Actual platform producer compiler inputs

This restore-only .NET 10 project supplies real framework metadata and real name
generators to `PlatformProducerCompilerTests`. It does not build a MAUI workload,
an Avalonia application, a native UI host, or a new shipping platform package.
The core test project's Roslyn driver co-runs these packaged producers with
ReactiveUI.SourceGenerators 4.2.0, matching Binding 9.1.0 and Validation.
WPF and WinUI members supplied before C# compilation continue through the
existing declared-symbol route; these fixtures do not add their workloads or a
new producer frontend.

The finite audited profiles are:

| Profile | Actual generator asset | Official source pin |
| --- | --- | --- |
| Avalonia 12.1.3 | `analyzers/dotnet/cs/Avalonia.Generators.dll` | [8eeda4f6](https://github.com/AvaloniaUI/Avalonia/tree/8eeda4f6f546165b3f72e63c9f42247abb306905/src/tools/Avalonia.Generators) |
| Microsoft.Maui.Controls.SourceGen 10.0.110 | `lib/netstandard2.0/Microsoft.Maui.Controls.SourceGen.dll` | [6c379bf1](https://github.com/dotnet/maui/tree/6c379bf1dc24461a985a093be3fd8a3e9bc6fc00/src/Controls/src/SourceGen) |

Avalonia's generator is included in the current `Avalonia` package. The separately
published historic `Avalonia.NameGenerator` package is not this profile.
MAUI's generator package is downloaded at its exact version and its DLL is
loaded directly. Its 10.0.110 nuspec names an unrelated
`Microsoft.Maui.Controls.BindingSourceGen` dependency absent from NuGet.org;
`PackageDownload` allows the real XAML generator to run independently, without
inventing that dependency. The real `Microsoft.Maui.Controls.Core` and
`Microsoft.Maui.Controls.Xaml` packages provide matching .NET 10 framework types.

`packages.lock.json` fixes the restore cohort. `immutable-inputs.json` records
the raw SHA-256/SHA-512 of every selected package archive, NuGet's restore content
hash, and the SHA-256
of every selected metadata/generator asset. The host rejects a differing restored
graph, changed cached archive, or changed extracted DLL before loading a
generator. Each DLL is also checked against its entry in the pinned archive.
NuGet's lock content hash excludes signature metadata, while the raw archive hash
includes it; those distinct values are verified against their respective inputs.
See the [NuGet package metadata contract](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File).

The regressions distinguish actual producer/compiler proof from runtime provider
behavior and from UI/native-host execution. They use original XAML additional
files, assembly-qualified CLR namespaces, root and generic control names, both
runtime flavors, generator orders, changed/removed inputs and Avalonia's
`OnlyProperties` mode. Typed provider/adapter integration uses the real framework
types; generic runtime ownership behavior is covered separately in core tests.
Successful producer and combined-driver cases retain original inputs, all real
generated files, producer DLL identities and source hashes under the test
output's `TestResults/PlatformProducerEvidence`. Each record has a fresh invocation
directory and explicitly records that UI and native hosts were not executed.

The acceptance run on 2026-10-06 used source commit
`0aaf329f3b5a9ade3f9dded6bb52164d0d6f1ff6`, the locked .NET SDK 10.0.401,
Roslyn 5.9.0.0 and C# 14. The compiler test project built with warnings as errors:
zero warnings and zero errors. Discovery selected exactly 33 actual-platform
cases; all 33 executed and passed, with zero skipped cases. The 12 combined
profiles compiled typed provider and host-adapter declarations against the real
framework metadata, emitted managed PE output and passed final dispatch/member
checking. The remaining cases cover producer contracts, options, template scopes,
private controls, recursive type arguments, property-element names, control-type
edits, collisions, malformed/missing inputs and explicit profile drift.

Run these checks from `src` in the locked project environment:

```sh
dotnet build tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -warnaserror -m:1
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -p:TreatWarningsAsErrors=true -- --treenode-filter '/*/*/*PlatformProducerCompilerTests/*' --list-tests --progress off
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -p:TreatWarningsAsErrors=true -- --treenode-filter '/*/*/*PlatformProducerCompilerTests/*' --maximum-parallel-tests 1 --report-trx --progress off
```

The captured manifest SHA-256 is
`29d4d14428d613237de55e9c669e43cc3d199db4f0e7230589a7865a672215f4`.
The exact generator DLL SHA-256 values are
`679670ed7db4d478bfcb2ec4c341fa281fb4a5fff6b0d694209b98b2431a18c0`
for MAUI and
`4cad53a499bc59b776817a0fd124eea810cd8aed114f0d10ceb2961a4638e987`
for Avalonia. Retained local evidence is under
`artifacts/platform-producer-verification/`: the `actual33-replay-*` build,
discovery and run logs, `actual33-passed.trx`, and
`actual33-successful-evidence-index.json`. Earlier failed attempts remain
separate from the passing run.
