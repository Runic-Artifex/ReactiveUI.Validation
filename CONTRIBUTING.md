# Building the Runic fork

For upstream syncs, urgent ports and releases, follow the
[maintenance policy](docs/maintenance.md). Update the
[fork difference register](docs/fork-differences.md) when adding, changing or
retiring an adaptation. The generated validation surface is described in the
[design](docs/generated-validation-design.md), the
[capability reference](docs/generated-capabilities.md) and the
[migration guide](docs/examples/generated-validation.md); trimming and NativeAOT
in [NativeAOT and generators](docs/aot-and-generators.md).

Use .NET SDK 10.0.401 (pinned in `global.json`) and Python 3. On the Runic
desktop, reuse the locked shell with `direnv exec ../runic-sdk <command>` from
the repository root (or `direnv exec ../../runic-sdk <command>` from `src`).

```sh
python3 eng/restore-fork-dependencies.py
cd src
dotnet build ReactiveUI.Validation.slnx -c Release -warnaserror
dotnet test --solution ReactiveUI.Validation.slnx -c Release -- --maximum-parallel-tests 4 --report-trx
dotnet pack ReactiveUI.Validation.slnx -c Release --no-build -o ../artifacts/packages
cd ..
python3 eng/verify-packages.py
python3 -B -m unittest discover -s eng -p 'test_verify_*.py'
python3 -B -m unittest discover -s eng/release -p test_release_assets.py
```

The bootstrap downloads both `Runic.DynamicData` 10.0.0-runic.30 flavors from
their GitHub release, verifies SHA-256 hashes and reuses valid downloads in
`artifacts/dependencies`, the local feed configured in `nuget.config`. Update the
central package versions and bootstrap checksums together. Restore never uses a
sibling checkout.

The core solution builds the generator, both .NET 10 libraries and their tests:

| Validation package | ReactiveUI package | DynamicData package |
| --- | --- | --- |
| `Runic.ReactiveUI.Validation` | `ReactiveUI` 26.0.1 | `Runic.DynamicData` |
| `Runic.ReactiveUI.Validation.Reactive` | `ReactiveUI.Reactive` 26.0.1 | `Runic.DynamicData.Reactive` |

Keep both shared-source flavors working. The Reactive flavor exposes
`DynamicData.Reactive.IObservableList`; Primitives exposes
`DynamicData.IObservableList`. Review API baseline changes explicitly. Use TUnit
assertions and deterministic scheduling for regressions. Always build before
testing; do not add `--no-build` to test commands.

## Focused tests

TUnit class segments are namespace-qualified, so use a leading wildcard and
check discovery before trusting a run (from `src`):

```sh
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -- --treenode-filter '/*/*/*CapabilityCompilerHostTests/*' --list-tests
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -- --treenode-filter '/*/*/*CapabilityCompilerHostTests/*' --report-trx
```

Omitting the leading class wildcard selects zero tests. Verify the executed,
passed and skipped counts in the report; zero discovered or executed tests is
never a pass.

## Package gates

CI runs the steps above on Linux and Windows, plus the strict native and
generated package gates on Linux x64 and Windows x64 against the same Linux
package artifact. Locally, after the bootstrap and on a matching host:

```sh
python3 eng/verify-native-validation.py --rid linux-x64 --mode all
python3 eng/verify-generated-validation.py --rid linux-x64 --mode all
```

Both gates pack the current clean source unless `--package-feed` is given, keep
warnings as errors, check exact flavor/version graphs and run managed, fully
trimmed and NativeAOT consumers. Gate tooling lives in `eng/` (including
`eng/tools/InspectIl` and `eng/restore-validation-feed.py`).

## Generated call sites

The core packages embed `ReactiveUI.Validation.SourceGenerators.dll` under
`analyzers/dotnet/roslyn5.9/cs` and `buildTransitive` props that add
`ReactiveUI.Validation.Generated` to `InterceptorsNamespaces`. Consumers must keep
those assets and compile with SDK 10.0.401 / C# 14; inspect a consumer's
analyzer inputs and imported props when generation is missing. Normal calls that
are neither intercepted nor matched by a registered typed plan fail at build or
run time; `*Unsafe` methods keep reflection and its warnings.

For generator changes, keep both-flavor compiler and diagnostic tests and inspect
the emitted getters, notification sources, paths and setters. Generated output
must not call expression APIs, `WhenAnyValueUnsafe` or the Unsafe methods, or
assume Binding will process newly emitted calls. Check null/default semantics,
subscription handoff and initial-failure cleanup, replacement, strict matching,
complete custom states and ownership. The safe observable rule/binding files use
classic `this` extensions because of a C# 14 `CS8714` bug; do not restore
extension blocks without reproducing a fixed compiler.

## Scope

`ReactiveUI.Validation.Platforms.slnx` keeps AndroidX projects and desktop
samples; it needs extra workloads and is outside core CI and releases. The old
UWP sample and non-.NET-10 API baselines are retained as upstream history.

The manual release workflow attaches the two core `.nupkg` assets to a GitHub
prerelease on this fork; it does not publish to NuGet.org or under upstream IDs.
Preserve upstream MIT licensing and authorship. This fork is maintained for Runic
and will not be proposed upstream.
