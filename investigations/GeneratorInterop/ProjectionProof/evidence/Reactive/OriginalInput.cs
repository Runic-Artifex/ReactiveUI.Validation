#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Reactive.Builder;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Binding.Reactive;
using System.Reactive.Linq;
namespace ProjectionInput;
public sealed partial class Model : ReactiveValidationObject
{
    [Reactive] private string? _name = null;
}
public static class Runner
{
    public static string Run()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        using var model = new Model();
        var seen = new List<string?>();
        using var observation = model.WhenAnyValue(x => x.Name).Subscribe(seen.Add);
        using var rule = model.ValidationRule(x => x.Name, value => !string.IsNullOrWhiteSpace(value), "required");
        Require(!rule.IsValid && model.Name is null, "initial nullable invalid");
        model.Name = "Ada";
        Require(rule.IsValid, "generated setter delivers valid update");
        model.Name = null;
        Require(!rule.IsValid, "nullable reset invalid");
        rule.Dispose();
        Require(model.ValidationContext.Validations.Count == 0, "rule disposal unregisters");
        model.Name = "Detached";
        Require(seen.SequenceEqual(new string?[] { null, "Ada", null, "Detached" }), "Binding privately projects same field and observes updates");
        return "initial-invalid -> Ada-valid -> null-invalid -> disposed; Binding=[null,Ada,null,Detached]";
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}