// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

#if REACTIVE_SHIM
using DynamicData.Reactive;
using ReactiveUI.Primitives.Reactive;
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using ModelScheduler = System.Reactive.Concurrency.IScheduler;
#else
using DynamicData;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ModelScheduler = ReactiveUI.Primitives.Concurrency.ISequencer;
#endif

namespace Runic.Validation.Examples;

/// <summary>A collection rule whose domain state is independent of error presentation.</summary>
internal sealed class CollectionValidationRecipe : ReactiveValidationObject
{
    /// <summary>The collection validation message.</summary>
    internal const string ChildrenMessage = "Add at least one child and give every child a name.";

    /// <summary>Owns the registered collection rule.</summary>
    private readonly ValidationHelper _childrenRule;

    /// <summary>Initializes a new instance of the <see cref="CollectionValidationRecipe"/> class.</summary>
    /// <param name="scheduler">Scheduler compatible with the owning model context.</param>
    internal CollectionValidationRecipe(ModelScheduler scheduler)
        : base(scheduler)
    {
        // StartWithEmpty makes empty membership a real initial snapshot. Refresh
        // child edits before querying: ToCollection alone is a membership stream.
        var validity = Children.Connect()
            .StartWithEmpty()
            .AutoRefresh(child => child.Name)
            .QueryWhenChanged(static query => ChildrenHaveNames(query.Items));
        _childrenRule = this.ValidationRule(model => model.Children, validity, ChildrenMessage);
    }

    /// <summary>Gets children indexed by immutable domain identity.</summary>
    internal SourceCache<RecipeChild, Guid> Children { get; } = new(static child => child.Id);

    /// <summary>Gets or sets whether the collection editor has been touched.</summary>
    internal bool Touched { get; set; }

    /// <summary>Gets or sets whether submission has been attempted.</summary>
    internal bool Submitted { get; set; }

    /// <summary>Gets whether current domain data permits submission, even before queued notifications settle.</summary>
    internal bool IsCurrentlyValid => ChildrenHaveNames(Children.Items);

    /// <summary>Gets whether the view should display the domain error.</summary>
    internal bool ShowChildrenError => (Touched || Submitted) && !IsCurrentlyValid;

    /// <summary>Releases rule subscriptions before child storage.</summary>
    /// <param name="disposing">Whether managed resources should be disposed.</param>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _childrenRule.Dispose();
        base.Dispose(disposing);
        Children.Dispose();
    }

    /// <summary>Evaluates current collection domain validity.</summary>
    /// <param name="children">Children to validate.</param>
    /// <returns>Whether there is at least one child and every name is present.</returns>
    private static bool ChildrenHaveNames(IEnumerable<RecipeChild> children)
    {
        var any = false;
        foreach (var child in children)
        {
            any = true;
            if (string.IsNullOrWhiteSpace(child.Name))
            {
                return false;
            }
        }

        return any;
    }
}
