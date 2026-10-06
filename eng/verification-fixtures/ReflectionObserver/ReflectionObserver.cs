// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
internal static class ReflectionObserver
{
    internal static void Exercise(Customer customer)
    {
        using var rule = customer.ValidationRuleUnsafe(model => model.Email, static value => !string.IsNullOrEmpty(value), "reflection-control");
    }
}
