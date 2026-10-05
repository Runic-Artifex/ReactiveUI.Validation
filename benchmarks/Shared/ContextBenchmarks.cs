// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

namespace Runic.Validation.Benchmarks;

/// <summary>Measures active aggregate contexts with controlled components and synchronous notifications.</summary>
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "BenchmarkDotNet owns fixture lifetime through GlobalSetup/GlobalCleanup.")]
public class ContextBenchmarks
{
    private ValidationContext _valid = null!;
    private ValidationContext _invalid = null!;
    private MutableComponent[] _validComponents = null!;
    private MutableComponent[] _invalidComponents = null!;
    private MutableComponent _extra = null!;
    private IDisposable _subscription = null!;
    private readonly StateObserver _observer = new();

    /// <summary>Gets or sets the number of registered components.</summary>
    [ParamsSource(nameof(CollectionSizes))]
    public int Count { get; set; }

    /// <summary>Gets the normal matrix, or a single representative size for an explicit smoke run.</summary>
    public static IEnumerable<int> CollectionSizes => Environment.GetEnvironmentVariable("RUNIC_VALIDATION_BENCHMARK_SMOKE") == "1" ? [10] : [1, 10, 100, 1000];

    /// <summary>Creates stable fixtures and verifies each measured operation before timing it.</summary>
    [GlobalSetup]
    public void Setup()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        _valid = new ValidationContext(ImmediateScheduler.Instance);
        _invalid = new ValidationContext(ImmediateScheduler.Instance);
        _validComponents = Enumerable.Range(0, Count).Select(static _ => new MutableComponent(true)).ToArray();
        _invalidComponents = Enumerable.Range(0, Count).Select(static _ => new MutableComponent(false)).ToArray();
        foreach (var component in _validComponents)
        {
            _valid.Add(component);
        }
        foreach (var component in _invalidComponents)
        {
            _invalid.Add(component);
        }
        _extra = new MutableComponent(false);
        _subscription = _invalid.ValidationStatusChange.Subscribe(_observer);
        Correctness.Require(_valid.IsValid && !_invalid.IsValid && _invalid.Text.Count == Count, "Initial aggregate state.");
        ValidityRoundTrip();
        Correctness.Require(_valid.IsValid && _valid.Text.Count == 0, "Validity round trip must restore validity.");
        var notifications = _observer.Notifications;
        InvalidMessageRoundTrip();
        Correctness.Require(!_invalid.IsValid && _invalid.Text.Count == Count && _invalid.Text[Count - 1] == "invalid-a", "Invalid message update must preserve aggregate invalidity and refresh text.");
        Correctness.Require(_observer.Notifications >= notifications + 2 && _observer.Latest?.Text[Count - 1] == "invalid-a", "Invalid-to-invalid changes must notify observers.");
        Correctness.Require(MembershipLast(), "Membership must find the last component.");
        AddRemoveRoundTrip();
        Correctness.Require(_valid.Validations.Count == Count && _valid.IsValid, "Add/remove must restore membership.");
        ClearRepopulateRoundTrip();
        Correctness.Require(_valid.Validations.Count == Count && _valid.IsValid, "Clear/repopulate must restore membership.");
        // Verify removal detaches observation before leaving the fixture ready for measurement.
        _valid.Add(_extra);
        Correctness.Require(!_valid.IsValid && _valid.Text.Count == 1, "Added invalid component must contribute an error.");
        _valid.Remove(_extra);
        _extra.Set(false, "detached");
        Correctness.Require(_valid.IsValid && _valid.Text.Count == 0, "Removed component must not affect context.");
    }

    /// <summary>Updates the last component from valid to invalid and back, forcing a full all-valid scan.</summary>
    [Benchmark]
    public void ValidityRoundTrip()
    {
        _validComponents[^1].Set(false, "invalid-a");
        _validComponents[^1].Set(true, string.Empty);
    }

    /// <summary>Updates an error message twice while every component and the aggregate remain invalid.</summary>
    [Benchmark]
    public void InvalidMessageRoundTrip()
    {
        _invalidComponents[^1].Set(false, "invalid-b");
        _invalidComponents[^1].Set(false, "invalid-a");
    }

    /// <summary>Scans the exposed list for its last member.</summary>
    [Benchmark]
    public bool MembershipLast() => _valid.Validations.Items.Contains(_validComponents[^1]);

    /// <summary>Adds and removes one invalid component, including active aggregate updates.</summary>
    [Benchmark]
    public void AddRemoveRoundTrip()
    {
        _valid.Add(_extra);
        _valid.Remove(_extra);
    }

    /// <summary>Removes all components in a batch, then restores them individually, including aggregate updates.</summary>
    [Benchmark]
    public void ClearRepopulateRoundTrip()
    {
        _valid.RemoveMany(_validComponents);
        foreach (var component in _validComponents)
        {
            _valid.Add(component);
        }
    }

    /// <summary>Releases observations, contexts, and each independently owned controlled component.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _valid.Dispose();
        _invalid.Dispose();
        foreach (var component in _validComponents)
        {
            component.Dispose();
        }
        foreach (var component in _invalidComponents)
        {
            component.Dispose();
        }
        _extra.Dispose();
    }
}
