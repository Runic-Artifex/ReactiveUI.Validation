// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
// The current pinned Binding setter uses reflection without dynamic compilation.
// This dedicated negative intentionally has no source expression observation.
internal static class ReflectionSetter
{
    internal static void Invoke(Editor editor)
    {
        var values = new Current<string>("setter-only");
#if REACTIVE_SHIM
        using var setter = global::ReactiveUI.Binding.Reactive.ReactiveUIBindingExtensions.BindToUnsafe<string, Editor, string>(values, editor, static view => view.Message);
#else
        using var setter = global::ReactiveUI.Binding.ReactiveUIBindingExtensions.BindToUnsafe<string, Editor, string>(values, editor, static view => view.Message);
#endif
    }
}
