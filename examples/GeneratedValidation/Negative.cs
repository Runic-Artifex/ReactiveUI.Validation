// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Linq.Expressions;

internal static class Negative
{
    public static void Unsupported(Customer customer, Editor editor, Expression<Func<Customer, string?>> opaqueSelector)
    {
#if NEGATIVE_OPAQUE_SELECTOR
        using var rule = customer.ValidationRule(opaqueSelector, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if NEGATIVE_UNMARKED_INIT
        var view = new DiagnosticView { ViewModel = customer };
        using var binding = view.BindValidation(customer, model => model.Email, target => target.InitialMessage);
#endif
#if NEGATIVE_UNMARKED_READONLY
        var view = new DiagnosticView { ViewModel = customer };
        using var binding = view.BindValidation(customer, model => model.Email, target => target.ReadonlyMessage);
#endif
#if NEGATIVE_GET_ONLY_TARGET
        var view = new DiagnosticView { ViewModel = customer };
        using var binding = view.BindValidation(customer, model => model.Email, target => target.GetOnlyMessage);
#endif
#if NEGATIVE_INVALID_ASSIGNMENT
        using var binding = editor.BindValidationState<Editor, Customer, object>(customer, model => model.AddressRule, target => target.Message, static state => (object)"invalid storage conversion");
#endif
#if NEGATIVE_VALUE_VIEW
        var view = new ValueView { ViewModel = customer };
        using var binding = view.BindValidation(customer, model => model.Email, target => target.Message);
#endif
    }

#if NEGATIVE_UNMARKED_INIT || NEGATIVE_UNMARKED_READONLY || NEGATIVE_GET_ONLY_TARGET
    private sealed class DiagnosticView : ReactiveObject, IViewFor<Customer>
    {
        public Customer? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
        object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }
        public string InitialMessage { get; init; } = "";
        public readonly string ReadonlyMessage = "";
        public string GetOnlyMessage { get; } = "";
    }

#endif

    private struct ValueView : IViewFor<Customer>
    {
        public Customer? ViewModel { get; set; }
        object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }
        public string? Message { get; set; }
    }
}
