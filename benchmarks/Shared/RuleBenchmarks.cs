// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

namespace Runic.Validation.Benchmarks;

/// <summary>Measures public property-rule construction and property changes with active aggregate consumers.</summary>
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "BenchmarkDotNet owns fixture lifetime through GlobalSetup/GlobalCleanup.")]
public class RuleBenchmarks
{
    private BenchmarkViewModel _model = null!;
    private ValidationHelper[] _rules = null!;

    /// <summary>Gets or sets the number of rules observing the same property.</summary>
    [ParamsSource(nameof(CollectionSizes))]
    public int Count { get; set; }

    /// <summary>Gets the matrix or the explicit smoke size.</summary>
    public static IEnumerable<int> CollectionSizes => Environment.GetEnvironmentVariable("RUNIC_VALIDATION_BENCHMARK_SMOKE") == "1" ? [10] : [1, 10, 100, 1000];

    /// <summary>Builds a stable fixture and checks public rule behavior before measurement.</summary>
    [GlobalSetup(Targets = [nameof(ConstructActivateDispose), nameof(PropertyValidityRoundTrip)])]
    public void Setup()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        _model = new BenchmarkViewModel();
        _rules = CreateRules(_model);
        Correctness.Require(_model.ValidationContext.IsValid, "Initial rules must be valid.");
        _model.Name = "invalid-a";
        Correctness.Require(!_model.ValidationContext.IsValid && _model.ValidationContext.Text?.Count == Count, "Each rule must contribute an error.");
        _model.Name = "invalid-b";
        Correctness.Require(_model.ValidationContext.Text is { } text && text.All(static value => value == "invalid-b"), "Error messages must refresh while invalid.");
        InvalidMessageRoundTrip();
        PropertyValidityRoundTrip();
        Correctness.Require(_model.ValidationContext.IsValid && _model.ValidationContext.Validations.Count == Count, "Property updates must restore the stable fixture.");
        ConstructActivateDispose();
        using var checkModel = new BenchmarkViewModel();
        var checkRules = CreateRules(checkModel);
        foreach (var rule in checkRules)
        {
            rule.Dispose();
        }
        checkModel.Name = "invalid-a";
        Correctness.Require(checkModel.ValidationContext.Validations.Count == 0 && checkModel.ValidationContext.IsValid, "Disposing helpers must detach all rules.");
    }

    /// <summary>Leaves the message workload invalid before its first invocation.</summary>
    [GlobalSetup(Target = nameof(InvalidMessageRoundTrip))]
    public void SetupInvalid()
    {
        Setup();
        _model.Name = "invalid-a";
    }

    /// <summary>Constructs a fresh model and rules, activates its context, then disposes every helper and context.</summary>
    [Benchmark]
    public void ConstructActivateDispose()
    {
        using var model = new BenchmarkViewModel();
        var rules = CreateRules(model);
        _ = model.ValidationContext.IsValid;
        foreach (var rule in rules)
        {
            rule.Dispose();
        }
    }

    /// <summary>Changes a shared property to invalid and back, notifying all rules and the aggregate.</summary>
    [Benchmark]
    public void PropertyValidityRoundTrip()
    {
        _model.Name = "invalid-a";
        _model.Name = "valid";
    }

    /// <summary>Changes the shared property between two invalid values and back without changing aggregate validity.</summary>
    [Benchmark]
    public void InvalidMessageRoundTrip()
    {
        _model.Name = "invalid-b";
        _model.Name = "invalid-a";
    }

    /// <summary>Disposes helpers before their owning context.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var rule in _rules)
        {
            rule.Dispose();
        }
        _model.Dispose();
    }

    private ValidationHelper[] CreateRules(BenchmarkViewModel model) => Enumerable.Range(0, Count)
        .Select(_ => model.ValidationRule(static vm => vm.Name, static name => name == "valid", static name => name!))
        .ToArray();
}
