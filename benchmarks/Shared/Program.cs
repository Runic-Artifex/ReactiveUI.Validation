// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

using BenchmarkDotNet.Running;
using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.ConsoleArguments.ListBenchmarks;
using BenchmarkDotNet.Loggers;
using Runic.Validation.Benchmarks;

// Inherit the reduced parameter set in BenchmarkDotNet's generated child processes.
if (args.Contains("--smoke", StringComparer.Ordinal))
{
    Environment.SetEnvironmentVariable("RUNIC_VALIDATION_BENCHMARK_SMOKE", "1");
    args = args.Where(static arg => arg != "--smoke").ToArray();
}

// Parse before switching: BDN returns no summaries for both bad arguments and valid information modes.
// Its parser reports help/version as unsuccessful parses, so validate any accompanying options separately.
var helpOrVersion = args.Any(static arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("--version", StringComparison.OrdinalIgnoreCase));
var validationArgs = args.Where(static arg => !arg.Equals("--help", StringComparison.OrdinalIgnoreCase) && !arg.Equals("--version", StringComparison.OrdinalIgnoreCase)).ToArray();
var (parsed, _, options) = ConfigParser.Parse(validationArgs, ConsoleLogger.Default);
if (!parsed)
{
    return 1;
}

var informationOnly = helpOrVersion || options.PrintInformation || options.ListBenchmarkCaseMode != ListBenchmarkCaseMode.Disabled;
RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
var summaries = BenchmarkSwitcher.FromAssembly(typeof(ContextBenchmarks).Assembly).Run(args).ToArray();
if (summaries.Length == 0 && !informationOnly)
{
    Console.Error.WriteLine("No benchmark measurements ran. Check the selection filters and command-line options.");
    return 1;
}

// BDN can return normally after a generated-project build or workload failure.
return summaries.Any(static summary => summary.HasCriticalValidationErrors || summary.Reports.IsEmpty || summary.Reports.Any(static report => !report.Success)) ? 1 : 0;
