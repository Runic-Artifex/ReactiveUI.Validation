using System.Linq.Expressions;

internal static class Negative
{
    public static void Unsupported(Customer customer, Editor editor)
    {
#if NEGATIVE_STORED_SELECTOR
        Expression<Func<Customer, string?>> selector = model => model.Email;
        using var rule = customer.ValidationRule(selector, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if NEGATIVE_COMPUTED_SELECTOR
        using var rule = customer.ValidationRule(model => model.Email.Trim(), static value => !string.IsNullOrEmpty(value), "required");
#endif
#if NEGATIVE_INDEXER
        using var rule = customer.ValidationRule(model => model.Email[0], static value => value != ' ', "required");
#endif
#if NEGATIVE_NONNOTIFY_RULE
        using var rule = customer.ValidationRule(model => model.Metadata.Value, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if NEGATIVE_STRUCT_OWNER
        using var rule = customer.ValidationRule(model => model.StructInfo.Value, static value => !string.IsNullOrEmpty(value), "required");
#endif
#if NEGATIVE_NESTED_TARGET
        using var binding = editor.BindValidation(customer, model => model.Email, view => view.PlainPanel.Child.Message);
#endif
    }
}
