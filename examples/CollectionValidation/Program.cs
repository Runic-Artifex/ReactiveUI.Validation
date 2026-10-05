// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using DynamicData.Reactive;
using ReactiveUI.Reactive.Builder;
using Scheduler = System.Reactive.Concurrency.ImmediateScheduler;
#else
using DynamicData;
using ReactiveUI.Builder;
using Scheduler = ReactiveUI.Primitives.Concurrency.ImmediateSequencer;
#endif

namespace Runic.Validation.Examples;

/// <summary>Runs the collection recipe with an explicit synchronous demonstration scheduler.</summary>
internal static class Program
{
    /// <summary>Exercises initial domain state, editing and same-key replacement.</summary>
    private static void Main()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        using var model = new CollectionValidationRecipe(Scheduler.Instance);
        using var validity = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(model.ValidationContext.Valid, valid => Console.WriteLine($"Observed validity: {valid}"));
        Console.WriteLine($"Empty: current={model.IsCurrentlyValid}, display={model.ShowChildrenError}");
        var child = new RecipeChild(Guid.NewGuid(), "Ada");
        model.Children.AddOrUpdate(child);
        child.Name = string.Empty;
        model.Touched = true;
        Console.WriteLine($"Edited: current={model.IsCurrentlyValid}, display={model.ShowChildrenError}");
        model.Children.AddOrUpdate(new RecipeChild(child.Id, "Grace"));
        child.Name = "Detached child no longer participates";
        Console.WriteLine($"Replaced: current={model.IsCurrentlyValid}, display={model.ShowChildrenError}");
        model.Children.Clear();
        model.Submitted = true;
        Console.WriteLine($"Cleared: current={model.IsCurrentlyValid}, display={model.ShowChildrenError}");
    }
}
