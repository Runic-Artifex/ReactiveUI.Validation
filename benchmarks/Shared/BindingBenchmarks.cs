// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

namespace Runic.Validation.Benchmarks;

/// <summary>Measures synchronous library binding lifetime operations on plain CLR views.</summary>
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "BenchmarkDotNet owns fixture lifetime through GlobalSetup/GlobalCleanup.")]
public class BindingBenchmarks
{
    private BenchmarkViewModel _first = null!;
    private BenchmarkViewModel _second = null!;
    private ValidationHelper[] _firstRules = null!;
    private ValidationHelper[] _secondRules = null!;
    private BenchmarkView _view = null!;
    private IDisposable _binding = null!;

    /// <summary>Gets or sets the number of property rules on each model; this is not a view count.</summary>
    [ParamsSource(nameof(CollectionSizes))]
    public int Count { get; set; }

    /// <summary>Gets the source rule counts, or ten rules for a smoke run.</summary>
    public static IEnumerable<int> CollectionSizes => Environment.GetEnvironmentVariable("RUNIC_VALIDATION_BENCHMARK_SMOKE") == "1" ? [10] : [1, 10, 100, 1000];

    /// <summary>Checks replacement, null transitions, current updates, and idempotent disposal before timing.</summary>
    [GlobalSetup]
    public void Setup()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        _first = new BenchmarkViewModel { Name = "first-a" };
        _second = new BenchmarkViewModel { Name = "second-a" };
        _firstRules = CreateRules(_first);
        _secondRules = CreateRules(_second);
        _view = new BenchmarkView { ViewModel = _first };
        _binding = CreateBinding(_view);
        Correctness.Require(_view.Error == "first-a", "Binding must publish the initial model's error.");

        _view.ViewModel = _second;
        Correctness.Require(_view.Error == "second-a", "Replacement must publish the new model's error.");
        var assignments = _view.Assignments;
        _first.Name = "first-b";
        _first.Name = "first-a";
        Correctness.Require(_view.Assignments == assignments && _view.Error == "second-a", "Replaced models must make no view assignments.");

        _view.ViewModel = null;
        Correctness.Require(_view.Error.Length == 0, "Null model must clear the binding.");
        assignments = _view.Assignments;
        _second.Name = "second-b";
        _second.Name = "second-a";
        Correctness.Require(_view.Assignments == assignments, "Null model must detach the previous model.");
        _view.ViewModel = _first;
        _first.Name = "first-b";
        Correctness.Require(_view.Error == "first-b", "The current model must remain observed.");
        _first.Name = "first-a";

        var detachedView = new BenchmarkView { ViewModel = _first };
        using (var detachedBinding = CreateBinding(detachedView))
        {
            Correctness.Require(detachedView.Error == "first-a", "A fresh binding must publish initial state.");
            detachedBinding.Dispose();
            detachedBinding.Dispose();
            assignments = detachedView.Assignments;
            detachedView.ViewModel = _second;
            _first.Name = "first-b";
            _first.Name = "first-a";
            _second.Name = "second-b";
            _second.Name = "second-a";
            Correctness.Require(detachedView.Assignments == assignments && detachedView.Error == "first-a", "Disposed bindings must stop view and model updates, retaining the last value.");
        }

        ConstructDispose();
        ReplaceModelRoundTrip();
        NullModelRoundTrip();
        Correctness.Require(_view.ViewModel == _first && _view.Error == "first-a", "Round trips must restore the first model and error.");
        Correctness.Require(_first.ValidationContext.Validations.Count == Count && _second.ValidationContext.Validations.Count == Count, "Binding lifetime operations must preserve the source rules.");
    }

    /// <summary>Constructs one plain view and property binding on existing rules, then disposes the binding.</summary>
    [Benchmark]
    public void ConstructDispose()
    {
        var view = new BenchmarkView { ViewModel = _first };
        using var binding = CreateBinding(view);
    }

    /// <summary>Replaces the existing binding's model twice, returning to the original model.</summary>
    [Benchmark]
    public void ReplaceModelRoundTrip()
    {
        _view.ViewModel = _second;
        _view.ViewModel = _first;
    }

    /// <summary>Clears and restores the existing binding's model, including the empty presentation.</summary>
    [Benchmark]
    public void NullModelRoundTrip()
    {
        _view.ViewModel = null;
        _view.ViewModel = _first;
    }

    /// <summary>Disposes the binding before helpers and their owning contexts.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _binding.Dispose();
        foreach (var rule in _firstRules)
        {
            rule.Dispose();
        }

        foreach (var rule in _secondRules)
        {
            rule.Dispose();
        }

        _first.Dispose();
        _second.Dispose();
    }

    private static IDisposable CreateBinding(BenchmarkView view) => view.BindValidationUnsafe(view.ViewModel, static vm => vm.Name, static target => target.Error);

    private ValidationHelper[] CreateRules(BenchmarkViewModel model) => Enumerable.Range(0, Count)
        .Select(_ => model.ValidationRuleUnsafe(static vm => vm.Name, static name => name == "valid", static name => name!))
        .ToArray();
}
