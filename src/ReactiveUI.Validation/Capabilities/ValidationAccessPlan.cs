// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Infers bound access plans from legal typed getter delegates.</summary>
public static class ValidationAccessPlan
{
    /// <summary>Creates an inferred cold plan with independently owned snapshot caches.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="factory">Creates a fresh observation plan for each independent subscription.</param>
    /// <param name="options">The policies declared by each inner observation plan.</param>
    /// <returns>The cold factory plan.</returns>
    public static ValidationAccessPlan<TValue> CreateObservation<TValue>(Func<ValidationAccessPlan<TValue>> factory, ValidationObservationOptions<TValue> options) =>
        new(factory, options);

    /// <summary>Creates an inferred cold plan with independent caches and default policies.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="factory">Creates a fresh observation plan using default policies.</param>
    /// <returns>The cold factory plan.</returns>
    public static ValidationAccessPlan<TValue> CreateObservation<TValue>(Func<ValidationAccessPlan<TValue>> factory) =>
        new(factory, ValidationObservationOptions<TValue>.Default);

    /// <summary>Creates an inferred value observation plan.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads a complete selection snapshot.</param>
    /// <param name="dependencies">The owned notification registration contracts.</param>
    /// <param name="options">Explicit policies, or the default missing-value and equality policies.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        ValidationObservationOptions<TValue> options) => new(read, dependencies, options);

    /// <summary>Creates an inferred value plan with default observation policies.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads the current value and identities.</param>
    /// <param name="dependencies">The typed notification registration contracts.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(Func<ValidationRead<TValue>> read, IEnumerable<ValidationDependency> dependencies) =>
        new(read, dependencies, ValidationObservationOptions<TValue>.Default);

    /// <summary>Creates an inferred plan with separate metadata access.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads a complete value snapshot.</param>
    /// <param name="dependencies">The owned notification registration contracts.</param>
    /// <param name="readPaths">Reads current identities without accessing the selected leaf.</param>
    /// <param name="options">Explicit policies, or default policies.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        Func<IReadOnlyList<ValidationPath>> readPaths,
        ValidationObservationOptions<TValue> options) => new(read, dependencies, options, readPaths);

    /// <summary>Creates an inferred metadata-capable plan with default policies.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads current values and identities.</param>
    /// <param name="dependencies">The typed notification registration contracts.</param>
    /// <param name="readPaths">Reads current identities without accessing the selected leaf.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        Func<IReadOnlyList<ValidationPath>> readPaths) => new(read, dependencies, ValidationObservationOptions<TValue>.Default, readPaths);

    /// <summary>Creates an inferred plan with independent value and metadata notifications.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads current values and identities.</param>
    /// <param name="dependencies">The value notification contracts.</param>
    /// <param name="readPaths">Reads identities without accessing selected values.</param>
    /// <param name="metadataDependencies">The metadata-only notification contracts.</param>
    /// <param name="options">The explicit observation policies.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        Func<IReadOnlyList<ValidationPath>> readPaths,
        IEnumerable<ValidationDependency> metadataDependencies,
        ValidationObservationOptions<TValue> options) => new(read, dependencies, options, readPaths, metadataDependencies);

    /// <summary>Creates an inferred plan with independent metadata notifications and default value policies.</summary>
    /// <typeparam name="TValue">The inferred selected type.</typeparam>
    /// <param name="read">Reads current values and identities.</param>
    /// <param name="dependencies">The value notification contracts.</param>
    /// <param name="readPaths">Reads identities without accessing selected values.</param>
    /// <param name="metadataDependencies">The metadata-only notification contracts.</param>
    /// <returns>The bound plan.</returns>
    public static ValidationAccessPlan<TValue> Create<TValue>(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        Func<IReadOnlyList<ValidationPath>> readPaths,
        IEnumerable<ValidationDependency> metadataDependencies) => new(read, dependencies, ValidationObservationOptions<TValue>.Default, readPaths, metadataDependencies);
}
