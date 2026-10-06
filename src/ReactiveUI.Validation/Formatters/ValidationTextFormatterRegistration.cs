// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Splat;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Formatters;
#else
namespace ReactiveUI.Validation.Formatters;
#endif

/// <summary>Registers formatters through typed service contracts and explicit construction delegates.</summary>
/// <remarks>
/// These registrations apply to bindings and error presentation that do not receive an explicit formatter.
/// Validation does not dispose formatters; the resolver determines the lifetime of registered services.
/// A custom resolver and the supplied factory must support the application's trimming and native requirements.
/// </remarks>
public static class ValidationTextFormatterRegistration
{
    /// <summary>Registers an existing formatter for default string validation presentation.</summary>
    /// <param name="resolver">The resolver used by the application's locator.</param>
    /// <param name="formatter">The formatter instance to register.</param>
    /// <remarks>
    /// Registers the interface contract, even when the supplied instance has a concrete formatter type.
    /// The resolver controls instance ownership. Splat's instance resolver disposes disposable constant services.
    /// </remarks>
    public static void Register(IMutableDependencyResolver resolver, IValidationTextFormatter<string> formatter)
    {
        ArgumentExceptionHelper.ThrowIfNull(resolver);
        ArgumentExceptionHelper.ThrowIfNull(formatter);
        resolver.RegisterConstant(formatter);
    }

    /// <summary>Registers a formatter factory without discovering or activating an implementation type.</summary>
    /// <param name="resolver">The resolver used by the application's locator.</param>
    /// <param name="factory">Constructs a formatter when the resolver requests this registration.</param>
    /// <remarks>
    /// Registration does not invoke the factory. Subsequent construction and ownership follow the resolver's
    /// factory contract; Splat's instance resolver invokes transient factories per resolution and does not dispose
    /// their results. Return a caller-owned shared instance when that lifetime is desired.
    /// Factory exceptions propagate to the caller resolving a formatter.
    /// </remarks>
    public static void RegisterFactory(IMutableDependencyResolver resolver, Func<IValidationTextFormatter<string>> factory)
    {
        ArgumentExceptionHelper.ThrowIfNull(resolver);
        ArgumentExceptionHelper.ThrowIfNull(factory);
        resolver.Register(factory);
    }
}
