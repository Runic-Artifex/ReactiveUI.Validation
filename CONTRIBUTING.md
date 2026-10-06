# Building the Runic fork

For monthly upstream reviews, integration ownership, urgent ports and immutable
releases, follow the [maintenance policy](docs/maintenance.md). Review and update
the [fork difference register](docs/fork-differences.md) when adding, changing or
retiring an adaptation. The [upstream investigation](docs/upstream/README.md) is
dated research; the [implementation ledger](docs/upstream/implementation-2026-10.md),
review records and the register track actual adoption.

The [NativeAOT status](docs/aot-and-generators.md) distinguishes the released
runtime and examples first implementation from dated warning-bearing
investigations. The strict
[actual-package gate](eng/verify-native-validation.py) publishes and runs the
explicit safe subset; it retains warning-as-error analysis and exact flavor,
version and source checks. The generated/Unsafe API split is implemented and verified,
but **UNRELEASED** until publication; see the [generator contract](docs/generated-validation-design.md)
and [migration recipe](docs/examples/generated-validation.md). The expanded
[capability ledger](docs/generated-capabilities-progress.md) tracks new typed,
registered and producer routes separately; final flagged-package/native acceptance
is pending. No retained UI-platform support is implied.

Use .NET SDK 10.0.401, pinned in the root `global.json`, and Python 3 for the
dependency bootstrap. On the Runic desktop, reuse the locked development shell
with `direnv exec ../runic-sdk <command>` from this repository root (or
`direnv exec ../../runic-sdk <command>` from `src`).

```sh
python3 eng/restore-fork-dependencies.py
cd src
dotnet build ReactiveUI.Validation.slnx -c Release -warnaserror
dotnet test --solution ReactiveUI.Validation.slnx -c Release -- --maximum-parallel-tests 4 --report-trx
dotnet pack ReactiveUI.Validation.slnx -c Release --no-build -o ../artifacts/packages
cd ..
python3 eng/verify-packages.py
```

The bootstrap downloads both `Runic.DynamicData` 10.0.0-runic.30 flavors from
their GitHub release, verifies SHA-256 hashes, and reuses valid downloads in
`artifacts/dependencies`. This local NuGet feed is configured in `nuget.config`.
Update the central package versions and bootstrap checksums together when
upgrading DynamicData. Restore uses released packages, not a sibling checkout.

The default solution builds and tests the two .NET 10 core libraries:

| Validation package | ReactiveUI package | DynamicData package |
| --- | --- | --- |
| `Runic.ReactiveUI.Validation` | `ReactiveUI` 26.0.1 | `Runic.DynamicData` |
| `Runic.ReactiveUI.Validation.Reactive` | `ReactiveUI.Reactive` 26.0.1 | `Runic.DynamicData.Reactive` |

Keep both shared-source flavors working. The System.Reactive flavor exposes
`DynamicData.Reactive.IObservableList` in its public API; the Primitives flavor
exposes `DynamicData.IObservableList`. Review API baseline changes explicitly.
Use TUnit assertions and deterministic scheduling for regressions. Build before
testing; the commands above compile current sources.

For focused compiler-host checks, the current TUnit class segment is namespace
qualified. Use a suffix wildcard and inspect discovery before accepting a run:

```sh
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -- --treenode-filter '/*/*/*CapabilityCompilerHostTests/*' --list-tests
dotnet run --project tests/ReactiveUI.Validation.SourceGenerators.Tests/ReactiveUI.Validation.SourceGenerators.Tests.csproj -c Release -- --treenode-filter '/*/*/*CapabilityCompilerHostTests/*' --report-trx
```

The measured current host lists 279 tests, with six selected by this class
filter; omitting its leading class wildcard selects zero. Set the expected
minimum for the group, then verify executed/passed and skipped counts in the
report. Zero discovery/execution is never a pass. These commands run from `src`
and build current sources; do not add `--no-build` for tests.

The retained `ReactiveUI.Validation.Platforms.slnx` includes AndroidX projects
and desktop samples. It requires additional workloads/platform tools and is
outside this fork's core CI and release scope. AndroidX package IDs are branded
`Runic.*`, but AndroidX binaries are not included in the release workflow.
The old UWP sample and non-.NET-10 API baselines are retained as upstream history.

CI verifies both flavors and independent package consumers on Linux and Windows,
and runs strict safe-subset NativeAOT consumers, with trimming enabled, against
the same Linux shipping package artifact on matching Linux x64 and Windows x64
hosts. After bootstrapping, the local native gate is:

```sh
direnv exec "$RUNIC_SDK" python3 eng/verify-native-validation.py --rid linux-x64 --mode all
```

Reuse the locked native toolchain and run on the matching host. The dated opt-in
investigative runner is not the strict gate. Recorded results identify the exact
source/packages and executed platforms; CLI support alone proves no platform.
The manual release workflow
attaches the two core `.nupkg` assets to a prerelease on this fork. It does not
publish under upstream package IDs or push to NuGet.org. MinVer uses an 8.1
floor, `runic-v` tag prefix and `runic.0` prerelease identifiers. Preserve upstream MIT licensing and
authorship. This fork is maintained for Runic and will not be proposed upstream.

## Generated call-site verification

The core packages embed `ReactiveUI.Validation.SourceGenerators.dll` under
`analyzers/dotnet/roslyn5.9/cs`, plus their matching `buildTransitive` props.
Those props add `ReactiveUI.Validation.Generated` to `InterceptorsNamespaces`.
Build with the pinned SDK 10.0.401/C#14 (actual Roslyn 5.9.0); inspect a packed
consumer's analyzer inputs and imported props when diagnosing missing generation. The normal
expression calls require recompilation with these assets for interception.
Explicit finite registered compatibility dispatch is a separate typed runtime
contract for stored/delegate/precompiled callers, with preserved ABI and owned
registration leases. Do not exclude
`analyzers` or `buildTransitive` assets from a project that uses them. No separate
Validation generator package reference is needed.

For generator changes, preserve both-flavor compilation/diagnostic checks and
inspect emitted direct getters, notification sources, full paths and setters.
Generated output must not call expression APIs, `WhenAnyValueUnsafe` or the
Validation `Unsafe` methods, or assume Binding will process newly emitted calls.
Retain actionable diagnostic errors for unsupported inputs and tests that
unregistered normal calls fail, registered fresh-expression/capture/ambiguity
routes execute safely, and ordinary missing dispatch remains a build error.
Explicit Unsafe calls retain their warning
contract. Check null/default semantics, synchronous subscription handoff and
initial-failure cleanup, source replacement, strict matching, complete custom
states and subscription ownership. The realistic GeneratedValidation consumers
must use actual packed packages and execute under managed/full-trim/native modes
with warnings as errors; record exact source/package/host identities separately
from earlier released runtime consumers. The generator split is verified at
`2550376b230ccfb78beb8d3b4ced8866b65d809e`, with all four Linux/Windows core
and consumer CI jobs passing. It remains unreleased until publication; the
[verification record](docs/upstream/reviews/2026-10-implementation.md#unreleased-generated-api-follow-up)
identifies exact source/packages and preserves historical release evidence.
Repeat the required gates for subsequent implementation changes.

The safe observable rule/binding files deliberately use traditional `this`
extensions: the locked C#14 compiler's nullable-generic synthesized static bridge
produces CS8714. Their narrow SST1703 exception records this measured compiler
issue; do not restore extension blocks without reproducing the corrected
compiler behavior. CLR signatures/inferred calls remain compatible, but explicit
observable binding source calls now supply `<TSource, TOut>` instead of only
`<TOut>`. `AddObservableRule<TValue>` keeps its arity. Review both API baselines
and fresh compiler-aligned regressions; do not suppress IL diagnostics or add
tuple-based signature workarounds.
