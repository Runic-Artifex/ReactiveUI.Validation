// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Runic.Validation.Examples;
#if REACTIVE_SHIM
using DynamicData.Reactive;
using ReactiveUI.Primitives.Reactive;
#else
using DynamicData;
using ReactiveUI.Primitives;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Exercises the executable recipe against the released DynamicData flavor.</summary>
public class CollectionValidationRecipeTests
{
    /// <summary>The two possible boolean states.</summary>
    private const int TwoStates = 2;

    /// <summary>Checks initial empty, addition, edits, removal and clear without rebuilding child identities.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task MembershipAndChildEditsMaintainDomainValidity()
    {
        using var model = new CollectionValidationRecipe(ImmediateSequencer.Instance);
        var states = new List<bool>();
        using var observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(model.ValidationContext.Valid, states.Add);
        await Assert.That(states[^1]).IsFalse();
        await Assert.That(model.ValidationContext.GetIsValid()).IsFalse();
        await Assert.That(model.ShowChildrenError).IsFalse();
        await Assert.That(model.GetErrors(nameof(model.Children)).Cast<string>()).Contains(CollectionValidationRecipe.ChildrenMessage);

        var child = new RecipeChild(Guid.NewGuid(), "Ada");
        model.Children.AddOrUpdate(child);
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        child.Name = " ";
        await Assert.That(model.ValidationContext.IsValid).IsFalse();
        await Assert.That(model.Children.Lookup(child.Id).Value).IsSameReferenceAs(child);
        model.Touched = true;
        await Assert.That(model.ShowChildrenError).IsTrue();
        child.Name = "Grace";
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        await Assert.That(model.ShowChildrenError).IsFalse();

        model.Children.RemoveKey(child.Id);
        await Assert.That(model.ValidationContext.IsValid).IsFalse();
        child.Name = string.Empty;
        await Assert.That(states[^1]).IsFalse();
        model.Children.AddOrUpdate(new RecipeChild(Guid.NewGuid(), "Linus"));
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        model.Children.Clear();
        await Assert.That(model.ValidationContext.IsValid).IsFalse();
        await Assert.That(states.Distinct().Count()).IsEqualTo(TwoStates);
    }

    /// <summary>Checks replacement at the same identity unsubscribes the old child, including after disposal.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ReplacementAndDisposalDetachOldChildren()
    {
        var model = new CollectionValidationRecipe(ImmediateSequencer.Instance);
        var oldChild = new RecipeChild(Guid.NewGuid(), string.Empty);
        model.Children.AddOrUpdate(oldChild);
        var states = new List<bool>();
        using var observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(model.ValidationContext.Valid, states.Add);
        var replacement = new RecipeChild(oldChild.Id, "Ada");
        model.Children.AddOrUpdate(replacement);
        await Assert.That(model.ValidationContext.IsValid).IsTrue();
        var count = states.Count;
        oldChild.Name = "old valid";
        oldChild.Name = string.Empty;
        await Assert.That(states.Count).IsEqualTo(count);
        replacement.Name = string.Empty;
        await Assert.That(model.ValidationContext.IsValid).IsFalse();
        model.Submitted = true;
        await Assert.That(model.ShowChildrenError).IsTrue();
        model.Dispose();
        count = states.Count;
        replacement.Name = "after disposal";
        await Assert.That(states.Count).IsEqualTo(count);
    }
}
