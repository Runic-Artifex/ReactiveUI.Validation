// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using Splat;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Formatters;
using ReactiveUI.Validation.Reactive.Formatters.Abstractions;
#else
using ReactiveUI.Validation.Formatters;
using ReactiveUI.Validation.Formatters.Abstractions;
#endif

internal static class FormatterRegistrationScenario
{
    internal static void Run()
    {
        var owned = new Formatter("constant:");
        var transient = new List<Formatter>();
        var constructions = 0;
        using (var resolver = new InstanceGenericFirstDependencyResolver())
        using (var scope = resolver.WithResolver(false))
        {
            Check(ReferenceEquals(GeneratedValidationBindingSupport.ResolveFormatter(null), SingleLineFormatter.Default), "An actual empty custom resolver uses the default formatter.");
            var first = new SingleLineFormatter("first");
            ValidationTextFormatterRegistration.Register(AppLocator.CurrentMutable, first);
            ValidationTextFormatterRegistration.Register(AppLocator.CurrentMutable, owned);
            Check(ReferenceEquals(GeneratedValidationBindingSupport.ResolveFormatter(null), owned), "Closed interface registrations use the last formatter.");
            Check(ReferenceEquals(GeneratedValidationBindingSupport.ResolveFormatter(first), first), "An explicit formatter bypasses default resolution.");
            Check(!resolver.GetServices<Formatter>().Any(), "Registration does not scan or infer a concrete formatter service.");

            using var customer = new Customer();
            using var rule = customer.ValidationRule(model => model.Email, static value => value?.Contains('@') == true, "required");
            var editor = new Editor { ViewModel = customer };
            using var binding = editor.BindValidation(customer, model => model.Email, view => view.Message);
            Check(editor.Message == "constant:required", "Normal packaged binding executes the registered formatter.");
            binding.Dispose();
            Check(owned.Disposals == 0, "A binding does not own a resolver-supplied formatter.");

            ValidationTextFormatterRegistration.RegisterFactory(resolver, () =>
            {
                constructions++;
                var created = new Formatter($"factory{constructions}:");
                transient.Add(created);
                return created;
            });
            Check(constructions == 0, "Typed factory registration is deferred.");
            var one = GeneratedValidationBindingSupport.ResolveFormatter(null);
            var two = GeneratedValidationBindingSupport.ResolveFormatter(null);
            Check(constructions == 2 && !ReferenceEquals(one, two) && ReferenceEquals(two, transient[^1]), "The pinned resolver invokes explicit transient factories per resolution.");
        }
        Check(owned.Disposals == 1 && transient.All(static value => value.Disposals == 0), "The resolver owns constants and leaves transient results caller-owned.");
        foreach (var formatter in transient) formatter.Dispose();

        using var missing = new InstanceGenericFirstDependencyResolver();
        using var missingScope = missing.WithResolver(false);
        ValidationTextFormatterRegistration.RegisterFactory(missing, static () => null!);
        Check(ReferenceEquals(GeneratedValidationBindingSupport.ResolveFormatter(null), SingleLineFormatter.Default), "A null service result falls back without reflected construction.");
        var failure = new InvalidOperationException("formatter-construction");
        ValidationTextFormatterRegistration.RegisterFactory(missing, () => throw failure);
        try
        {
            GeneratedValidationBindingSupport.ResolveFormatter(null);
            throw new InvalidOperationException("Factory failure was swallowed.");
        }
        catch (InvalidOperationException error) when (ReferenceEquals(error, failure))
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Formatter(string prefix) : IValidationTextFormatter<string>, IDisposable
    {
        internal int Disposals { get; private set; }
        public string Format(IValidationText? text) => prefix + (text?.ToSingleLine() ?? "");
        public void Dispose() => Disposals++;
    }
}
