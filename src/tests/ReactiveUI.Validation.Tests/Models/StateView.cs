// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests.Models;
#else
namespace ReactiveUI.Validation.Tests.Models;
#endif

/// <summary>A native view with enum and nested property targets.</summary>
internal sealed class StateView : ReactiveObject, IViewFor<TestViewModel>
{
    /// <inheritdoc/>
    public TestViewModel? ViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TestViewModel?)value;
    }

    /// <summary>Gets or sets the native enum target.</summary>
    public DayOfWeek State { get; set; }

    /// <summary>Gets or sets the replaceable nested target.</summary>
    public StateTarget? Target
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();
}
