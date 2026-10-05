# Opt-in validation benchmarks

This separate .NET 10 solution measures the two core validation flavors for the
work proposed in upstream [#117](https://github.com/reactiveui/ReactiveUI.Validation/issues/117).
Both executables compile the same source files, reference their matching core
project, and restore that project's existing ReactiveUI 26.0.1 / released
Runic.DynamicData 10.0.0-runic.5 dependency cohort. No benchmark dependency is
added to the normal core solution, tests, package graph, or release workflow.
The benchmark executables set `IsPackable=false` and `IsTestProject=false`.

BenchmarkDotNet is pinned separately at **0.15.8**, the latest stable package
verified on 2026-10-05 from [NuGet](https://www.nuget.org/packages/BenchmarkDotNet/0.15.8)
and the [official changelog](https://benchmarkdotnet.org/changelog/v0.15.8.html).
It supports .NET 10; this solution uses the repository's SDK **10.0.401** pin.
The tooling pin belongs to `benchmarks/Directory.Build.props`, leaving the core
central dependency pins unchanged.

From the repository root, bootstrap released dependencies and build explicitly:

```sh
python3 eng/restore-fork-dependencies.py
dotnet build benchmarks/ReactiveUI.Validation.Benchmarks.slnx -c Release -warnaserror
```

On this NixOS desktop, prefix each command with
`direnv exec /home/viktor/Development/RunicArtifex/runic-sdk` to reuse the existing
locked SDK shell. Do not install a separate SDK. On other hosts use the .NET SDK
pinned by `global.json`.

List workloads without measuring them:

```sh
dotnet run --project benchmarks/Primitives/ReactiveUI.Validation.Benchmarks.csproj -c Release -- --list flat
dotnet run --project benchmarks/Reactive/ReactiveUI.Validation.Reactive.Benchmarks.csproj -c Release -- --list flat
```

For a bounded smoke run, `--smoke` reduces **only** the collection-size parameters
to ten. Its environment setting is inherited by generated benchmark processes.
`Dry` checks execution, assertions, and cleanup, with a single cold-start sample;
its timings are not a steady-state performance baseline.

```sh
dotnet run --project benchmarks/Primitives/ReactiveUI.Validation.Benchmarks.csproj -c Release -- --smoke --job Dry --filter '*' --artifacts artifacts/benchmarks/primitives-dry
dotnet run --project benchmarks/Reactive/ReactiveUI.Validation.Reactive.Benchmarks.csproj -c Release -- --smoke --job Dry --filter '*' --artifacts artifacts/benchmarks/reactive-dry
```

Select comparable warmed measurements, running flavors sequentially on an idle
machine:

```sh
dotnet run --project benchmarks/Primitives/ReactiveUI.Validation.Benchmarks.csproj -c Release -- --smoke --job short --filter '*ContextBenchmarks.InvalidMessageRoundTrip*' '*RuleBenchmarks.PropertyValidityRoundTrip*' --artifacts artifacts/benchmarks/primitives-short
dotnet run --project benchmarks/Reactive/ReactiveUI.Validation.Reactive.Benchmarks.csproj -c Release -- --smoke --job short --filter '*ContextBenchmarks.InvalidMessageRoundTrip*' '*RuleBenchmarks.PropertyValidityRoundTrip*' --artifacts artifacts/benchmarks/reactive-short
```

For deeper investigation, omit `--smoke` to cover **1, 10, 100, and 1000**
components/rules, keep a focused `--filter`, and use a longer BenchmarkDotNet job.
The all-method/all-size matrix is deliberately opt-in and can be expensive,
especially the rule fan-out and clear/repopulate cases. Check available space,
avoid overlapping CI or other benchmarks, and retain relevant result summaries.
Generated projects live in ignored benchmark `bin` directories; reports/logs live
at `--artifacts`. Remove task-owned generated projects after consuming their
reports, and preserve useful reports and shared NuGet caches.

| Workload | One measured invocation |
| --- | --- |
| `ContextBenchmarks.ValidityRoundTrip` | Change the last controlled component valid → invalid → valid in an otherwise valid active context. |
| `ContextBenchmarks.InvalidMessageRoundTrip` | Change the last error `invalid-a` → `invalid-b` → `invalid-a` while all components and the aggregate remain invalid. |
| `ContextBenchmarks.MembershipLast` | Scan the exposed collection for its last member. |
| `ContextBenchmarks.AddRemoveRoundTrip` | Add and remove one invalid component, including aggregate updates. |
| `ContextBenchmarks.ClearRepopulateRoundTrip` | Batch-remove all components, then add the same components individually, including aggregate updates. This measures the complete replacement recipe, not isolated removal. |
| `RuleBenchmarks.ConstructActivateDispose` | Create a fresh model/context, register `Count` public property rules, activate the aggregate, dispose every helper, then dispose the context. Includes teardown and allocation; not isolated constructor latency. |
| `RuleBenchmarks.PropertyValidityRoundTrip` | Change one property valid → invalid → valid, updating all `Count` rules observing it and the aggregate. |
| `RuleBenchmarks.InvalidMessageRoundTrip` | Change one property between two invalid values and back, updating every rule's message while aggregate validity remains false. |

Context workloads use small controlled components to isolate aggregate handling
from ReactiveUI property observation and rule creation. Public rule workloads
include property observation, rule/helper behavior, and aggregate fan-out. Each
fixture activates its context and verifies validity, message content,
invalid-to-invalid notifications, collection membership, and removal/disposal
behavior during setup, outside measured operations. Round trips restore the
starting state every invocation; rule construction uses fresh fixtures, and
helpers are disposed before their contexts. Controlled component subjects are
owned and disposed by the fixture. No subscriptions or rules accumulate across
iterations. [BenchmarkDotNet's lifetime documentation](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)
explains why this uses global setup/cleanup rather than iteration setup that
would force single-invocation measurements.

`MemoryDiagnoser` reports managed allocations per complete invocation. Contexts
run on each flavor's synchronous immediate scheduler. These are CPU throughput
and managed-allocation workloads: they do not measure UI dispatcher latency,
frame times, asynchronous application response, native rendering, or Native AOT.
Different methods perform different work and have no cross-method ratio baseline.
Compare the same method, size, runtime, and host when investigating changes.

Binding replacement and repeated binding disposal are intentionally deferred:
the starting revision has reproduced lifetime bugs in both flavors. Add those
benchmarks after integrating their correctness fix and permanent regressions;
a faster incorrect binding is not a useful baseline. No core algorithm is changed
by this benchmark project. [Initial measurements](results/2026-10-05.md) record
what was actually run and the limits on interpreting it.
