// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.Contexts;
#else
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.Contexts;
#endif

/// <summary>Original input for the actual peer producer; it supplies no manually named generated caller.</summary>
internal sealed class PeerGeneratedCallerModel : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
{
    private PropertyChangedEventHandler? _changed;
    internal int Listeners { get; private set; }
    internal int UnknownReads { get; private set; }
    public string? Text { get; private set; }
    public string? Unknown { get { UnknownReads++; throw new InvalidOperationException("Unknown expression was evaluated."); } }
    public IValidationContext ValidationContext { get; } = new ValidationContext();
    public new event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; Listeners++; }
        remove { _changed -= value; Listeners--; }
    }

    internal void Set(string value)
    {
        Text = value;
        _changed?.Invoke(this, new(nameof(Text)));
    }
}

/// <summary>Executes actual generated peer output, finite registration and deterministic failure boundaries.</summary>
internal static class PeerGeneratedCallerScenario
{
    /// <summary>Runs both generated routes and verifies unknown and unregistered calls fail without subscriptions.</summary>
    internal static void Run()
    {
        var model = new PeerGeneratedCallerModel();
        using var context = model.ValidationContext;
        using var registry = new ValidationPlanRegistry(4);
        using var attachment = registry.Attach(model);
        Expression<Func<PeerGeneratedCallerModel, string?>> expression = current => current.Text;
        using var registration = registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, PeerRegisteredRuleCaller.Selector());
        using var normal = PeerRegisteredRuleCaller.Attach(model, expression);
        using var typed = PeerRegisteredRuleCaller.AttachTyped(model);
        if (normal.IsValid || typed.IsValid || model.Listeners != 2) throw new InvalidOperationException("Peer initial actual state failed.");
        model.Set("peer-ok");
        if (!normal.IsValid || !typed.IsValid || !model.ValidationContext.GetIsValid()) throw new InvalidOperationException("Peer updates failed.");
        Expression<Func<PeerGeneratedCallerModel, string?>> unknown = current => current.Unknown;
        ExpectMissing(model, unknown);
        if (model.UnknownReads != 0 || model.Listeners != 2 || model.ValidationContext.Validations.Items.Count != 2)
            throw new InvalidOperationException("Unknown peer registration acquired effects.");
        normal.Dispose();
        typed.Dispose();
        registration.Dispose();
        ExpectMissing(model, expression);
        if (model.Listeners != 0 || model.ValidationContext.Validations.Items.Any()) throw new InvalidOperationException("Unregistered peer call retained ownership.");
    }

    /// <summary>Requires the finite registration boundary to reject before any selected getter is invoked.</summary>
    /// <param name="model">The borrowed original source.</param>
    /// <param name="expression">The exact current invocation expression.</param>
    private static void ExpectMissing(PeerGeneratedCallerModel model, Expression<Func<PeerGeneratedCallerModel, string?>> expression)
    {
        try
        {
            using var unexpected = PeerRegisteredRuleCaller.Attach(model, expression);
        }
        catch (InvalidOperationException error) when (error.Message.Contains("No typed validation capability is registered", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException("A peer call without a finite registration succeeded.");
    }
}
