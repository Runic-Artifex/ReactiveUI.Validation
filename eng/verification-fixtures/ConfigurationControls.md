# Packed configuration controls

`eng/verify-generated-configuration.py` runs eleven bounded managed controls per
flavor against the main gate's exact package archive. It does not pack or publish.
The final acceptance record must bind the helper, compiler inputs and reports to
the frozen package source. A correction measured against an earlier failed pair
does not complete that pair's final gate.

| Control | Required boundary |
| --- | --- |
| generated | Clean compilation and actual generated typed-rule execution. |
| excluded-analyzer | On locked SDK 10.0.401, restore metadata excludes analyzers but actual compiler inputs still contain the exact bundled generator; generated execution succeeds. |
| removed-analyzer | The exact bundled `Analyzer` item is absent; compilation succeeds and the actual normal runtime call fails with its actionable missing-capability message and supported alternatives. |
| analyzers-disabled | `RunAnalyzers=false` disables diagnostic analysis while source generation and typed execution remain present. |
| excluded-build | Packaged transitive props and the generated namespace allowlist are absent; the selected normal call reports actionable `RUVG003`. |
| wrong-allowlist | A caller's global allowlist excludes the generated namespace; the selected normal call reports actionable `RUVG003`. |
| csharp13 | Current compiler with C# 13 reports actionable `RUVG003`; this does not prove an older compiler load. |
| missing-runtime | Package compile assets are absent; an explicit validation runtime type reference in `Program.cs` fails with `CS0234`. This reference-only fixture removes injected global `Using` items so an unrelated package import cannot satisfy the failure. |
| old-runtime | Current bundled analyzer with an immutable old `.15.10` runtime lacking `Capabilities.ValidationRuntime` reports actionable `RUVG004`. |
| older-compiler | Actual official Toolset 5.0 compiler loads the exact current analyzer and reports `CS9057`; unrelated load, framework and fatal failures are rejected. |
| collision | Independently compiled friend assemblies with identical sanitized names and first bridge IDs receive different generated namespace identities and execute both typed rules. |

`ExcludeAssets=analyzers` is not proof of actual analyzer absence on this locked
SDK. The measured compiler inputs determine that boundary. The official
[SDK 10.0.401 resolver](https://github.com/dotnet/sdk/blob/v10.0.401/src/Tasks/Microsoft.NET.Build.Tasks/ResolvePackageAssets.cs)
reads package analyzer files. The official
[NuGet analyzer-assets notes](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview7/nuget.md)
also distinguish restored exclusion metadata from SDK enforcement. These controls
make no support claim about another SDK version.

The older compiler package is private to its control. Locked MSBuild resolves the
actual sources, references, defines, namespace allowlist and analyzer items. The
helper replays those inputs directly through verified `csc.dll` using the declared
host's exact installed CLR 10 patch and `--roll-forward Disable`. It retains the
original runtimeconfig, archive-entry byte checks, compiler identities, command,
response file, host information and actual CoreCLR trace. It does not execute old
MSBuild tasks or add the toolset to a positive runtime graph.

Input snapshots are retained before parsing compiler capture, including partial
restore assets and props when capture fails. Generated source files and the small
control/peer executables are copied out of disposable work. Expected diagnostics
must have exact codes and provenance; they cannot conceal uncoded errors or fatal
compiler/runtime failures. The old runtime fixture has separate
[immutable provenance](UnsupportedRuntime/provenance.json).
