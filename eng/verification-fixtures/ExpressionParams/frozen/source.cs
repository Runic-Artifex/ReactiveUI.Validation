// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
#else
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
#endif

namespace RuleExpressionParamsNegative;

/// <summary>Uses the retained expression API so original params-array construction stays a truthful Native AOT negative.</summary>
public static class Program
{
    /// <summary>Executes the preserved expression caller from a separately compiled host.</summary>
    public static void Run()
    {
        using var context = new ValidationContext();
        var model = new IndependentModel(context);
        Expression<Func<IndependentModel,string?>> selector = source => source[source.Key,1,2];
        using var rule = model.ValidationRule(selector, static value => value == "2/1,2", "params");
        Console.WriteLine(rule.IsValid);
    }
}

/// <summary>The fixed original-expression caller model.</summary>
public sealed class IndependentModel(IValidationContext context) : ReactiveObject, IValidatableViewModel
{
    private int _key = 2;

    /// <summary>Gets the concrete instance index argument.</summary>
    public int Key => _key;

    /// <summary>Gets the explicit registration context.</summary>
    public IValidationContext ValidationContext { get; } = context;

    /// <summary>Gets the params indexer output whose expression argument constructs an array.</summary>
    /// <param name="first">The first index argument.</param>
    /// <param name="rest">The params values.</param>
    public string this[int first,params int[] rest] => $"{first}/{string.Join(",",rest)}";
}
