# Building the Runic fork

For monthly upstream reviews, integration ownership, urgent ports and immutable
releases, follow the [maintenance policy](docs/maintenance.md). Review and update
the [fork difference register](docs/fork-differences.md) when adding, changing or
retiring an adaptation. The [upstream investigation](docs/upstream/README.md) is
dated research; the [implementation ledger](docs/upstream/implementation-2026-10.md),
review records and the register track actual adoption.

The [NativeAOT and generator investigation](docs/aot-and-generators.md) records
the separately scoped native consumer evidence and proposed migration path.
Its opt-in investigations do not expand core CI, platform support or the
production package's NativeAOT contract.

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

The bootstrap downloads both `Runic.DynamicData` 10.0.0-runic.5 flavors from
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

The retained `ReactiveUI.Validation.Platforms.slnx` includes AndroidX projects
and desktop samples. It requires additional workloads/platform tools and is
outside this fork's core CI and release scope. AndroidX package IDs are branded
`Runic.*`, but AndroidX binaries are not included in the release workflow.
The old UWP sample and non-.NET-10 API baselines are retained as upstream history.

CI verifies both flavors and independent package consumers on Linux and Windows. The manual release workflow
attaches the two core `.nupkg` assets to a prerelease on this fork. It does not
publish under upstream package IDs or push to NuGet.org. MinVer uses an 8.1
floor, `runic-v` tag prefix and `runic.0` prerelease identifiers. Preserve upstream MIT licensing and
authorship. This fork is maintained for Runic and will not be proposed upstream.
