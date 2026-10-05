// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Runic.Application.Views;
using Runic.Validation.Examples;
#if REACTIVE_SHIM
using DynamicData.Reactive;
using ReactiveUI.Reactive;
using ReactiveUI.Reactive.Builder;
using ReactiveUI.Validation.Reactive.Components;
using Runic.Application.Views.ReactiveUI.Reactive;
#else
using DynamicData;
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Validation.Components;
using Runic.Application.Views.ReactiveUI;
#endif

// Opt-in source probe of actual SDK model-context/adapters, not a bridge host.
RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
await using var modelContext = new RunicModelContext();
var scheduler = new RunicReactiveSchedulerProvider().For(modelContext);
CollectionValidationRecipe? model = null;
RecipeChild? child = null;
ICommand? command = null;
IDisposable? observation = null;
var enabled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var disabled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var expectingInvalid = false;
var directExecutionRejected = false;

await modelContext.InvokeAsync(() =>
{
    model = new CollectionValidationRecipe(scheduler);
    child = new RecipeChild(Guid.NewGuid(), "Ada");
    model.Children.AddOrUpdate(child);
    var created = ReactiveCommand.Create(() =>
    {
        if (!model.IsCurrentlyValid || !model.ValidationContext.GetIsValid())
        {
            directExecutionRejected = true;
            return;
        }

        Console.WriteLine("Domain guard admitted work.");
    }, model.ValidationContext.Valid, scheduler);
    command = created;
    observation = ReactiveUI.Primitives.SubscribeExtensions.Subscribe(created.CanExecute, valid =>
    {
        if (valid) enabled.TrySetResult();
        else if (expectingInvalid) disabled.TrySetResult();
    });
});
await enabled.Task.WaitAsync(TimeSpan.FromSeconds(10));

await modelContext.InvokeAsync(() =>
{
    using var lateRule = new ObservableValidation<CollectionValidationRecipe, bool>(
        model!, ReactiveUI.Primitives.Signals.Signal.Return(false), static valid => valid, "Late blocking rule");
    model!.ValidationContext.Add(lateRule);
    Console.WriteLine($"Added rule: GetIsValid={model.ValidationContext.GetIsValid()}, HasErrors={model.HasErrors}, CanExecute={command!.CanExecute(null)}");
    if (model.ValidationContext.GetIsValid() || !model.HasErrors || command.CanExecute(null))
        throw new InvalidOperationException("Dynamic membership did not close command admission in the same model turn.");
    model.ValidationContext.Remove(lateRule);
    if (!model.ValidationContext.GetIsValid() || model.HasErrors || !command.CanExecute(null))
        throw new InvalidOperationException("Rule removal did not restore current admission.");

    expectingInvalid = true;
    child!.Name = string.Empty;
    Console.WriteLine($"Edited child: current={model.IsCurrentlyValid}, IsValid={model.ValidationContext.IsValid}, GetIsValid={model.ValidationContext.GetIsValid()}, HasErrors={model.HasErrors}, CanExecute={command.CanExecute(null)}");
    if (model.IsCurrentlyValid || model.ValidationContext.GetIsValid() || !model.HasErrors || command.CanExecute(null))
        throw new InvalidOperationException("Child edit did not close command admission in the same model turn.");

    // ICommand.Execute does not enforce CanExecute. The command body must guard
    // current domain invariants even for an internal/programmatic caller.
    command.Execute(null);
    if (!directExecutionRejected)
        throw new InvalidOperationException("The domain guard failed to reject direct execution.");
});
await disabled.Task.WaitAsync(TimeSpan.FromSeconds(10));
await modelContext.InvokeAsync(() =>
{
    Console.WriteLine($"Settled: IsValid={model!.ValidationContext.IsValid}, HasErrors={model.HasErrors}, CanExecute={command!.CanExecute(null)}, direct execution rejected={directExecutionRejected}");
    observation!.Dispose();
    ((IDisposable)command).Dispose();
    model.Dispose();
});
