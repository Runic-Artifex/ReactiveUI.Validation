// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

using BenchmarkDotNet.Running;
using Runic.Validation.Benchmarks;

// Inherit the reduced parameter set in BenchmarkDotNet's generated child processes.
if (args.Contains("--smoke", StringComparer.Ordinal))
{
    Environment.SetEnvironmentVariable("RUNIC_VALIDATION_BENCHMARK_SMOKE", "1");
    args = args.Where(static arg => arg != "--smoke").ToArray();
}

RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
var summaries = BenchmarkSwitcher.FromAssembly(typeof(ContextBenchmarks).Assembly).Run(args);
// BDN can return normally after a generated-project build or workload failure.
return summaries.Any(static summary => summary.HasCriticalValidationErrors || summary.Reports.Any(static report => !report.Success)) ? 1 : 0;
