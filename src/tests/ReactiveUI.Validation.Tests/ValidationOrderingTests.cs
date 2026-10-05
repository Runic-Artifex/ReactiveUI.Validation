// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Runic.Validation.Examples;
#if REACTIVE_SHIM
using DynamicData.Reactive;
using ReactiveUI.Reactive;
#else
using DynamicData;
using ReactiveUI;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Tests explicit validation notification and command admission boundaries.</summary>
public class ValidationOrderingTests
{
    /// <summary>Checks the bridge-readable error contract in the callback that tells a consumer to read it.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ErrorsChangedExposesCurrentPropertyErrorsAndHasErrors()
    {
        using var model = new CollectionValidationRecipe(ImmediateSequencer.Instance);
        var snapshots = new List<(string? Property, bool HasErrors, string[] Errors)>();
        model.ErrorsChanged += (_, args) => snapshots.Add((args.PropertyName, model.HasErrors, model.GetErrors(args.PropertyName).Cast<string>().ToArray()));
        var child = new RecipeChild(Guid.NewGuid(), "Ada");
        model.Children.AddOrUpdate(child);
        snapshots.Clear();
        child.Name = string.Empty;
        await Assert.That(snapshots).IsNotEmpty();
        foreach (var snapshot in snapshots)
        {
            await Assert.That(snapshot.Property).IsEqualTo(nameof(model.Children));
            await Assert.That(snapshot.HasErrors).IsTrue();
            await Assert.That(snapshot.Errors).Contains(CollectionValidationRecipe.ChildrenMessage);
        }

        snapshots.Clear();
        child.Name = "Ada";
        foreach (var snapshot in snapshots)
        {
            await Assert.That(snapshot.HasErrors).IsFalse();
            await Assert.That(snapshot.Errors).IsEmpty();
        }

        await Assert.That(snapshots).IsNotEmpty();
    }

    /// <summary>Checks existing rules gate commands before queued property observations settle and a domain guard protects direct execution.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task QueuedValidationAndCommandGateSettleAfterModelMutation()
    {
        var scheduler = new ValidationManualScheduler();
        using var model = new CollectionValidationRecipe(scheduler);
        var executed = false;
        using var command = ReactiveCommand.Create(
            () =>
        {
            // Availability is a presentation signal. Work must still enforce
            // current domain invariants when mutations share the same turn.
            if (model.IsCurrentlyValid)
            {
                executed = true;
            }
            },
            model.ValidationContext.Valid,
            scheduler);
        var gate = (ICommand)command;
        scheduler.Drain();
        await Assert.That(gate.CanExecute(null)).IsFalse();

        var child = new RecipeChild(Guid.NewGuid(), "Ada");
        model.Children.AddOrUpdate(child);
        await Assert.That(model.IsCurrentlyValid).IsTrue();
        scheduler.Drain();
        await Assert.That(model.ValidationContext.GetIsValid()).IsTrue();
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        await Assert.That(gate.CanExecute(null)).IsTrue();

        child.Name = string.Empty;
        await Assert.That(model.IsCurrentlyValid).IsFalse();

        // Raw Valid gates an existing rule before the queued OAPH IsValid updates.
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        await Assert.That(model.ValidationContext.GetIsValid()).IsFalse();
        await Assert.That(model.HasErrors).IsTrue();
        await Assert.That(gate.CanExecute(null)).IsFalse();
        gate.Execute(null);
        await Assert.That(executed).IsFalse();
        scheduler.Drain();
        await Assert.That(model.ValidationContext.IsValid).IsFalse();
        await Assert.That(gate.CanExecute(null)).IsFalse();

        child.Name = "Ada";
        scheduler.Drain();
        gate.Execute(null);
        scheduler.Drain();
        await Assert.That(executed).IsTrue();
    }

    /// <summary>Checks an explicitly earlier property observer is not a validation barrier while current state is correct after the setter.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task EarlierPropertyObserverDoesNotDefineValidationCompletion()
    {
        using var model = new IndeiTestViewModel { Name = "Ada" };
        var observedHasErrors = true;
        ((INotifyPropertyChanged)model).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(model.Name))
            {
                observedHasErrors = model.HasErrors;
            }
        };
        using var rule = model.ValidationRuleUnsafe(value => value.Name, static name => !string.IsNullOrWhiteSpace(name), "Name required");
        model.Name = string.Empty;
        await Assert.That(observedHasErrors).IsFalse();
        await Assert.That(model.HasErrors).IsTrue();
        await Assert.That(model.ValidationContext.GetIsValid()).IsFalse();
        await Assert.That(model.GetErrors(nameof(model.Name)).Cast<string>()).Contains("Name required");
    }
}
