# Typed validation-state bindings

`BindValidationState` converts validation **states**, including `IsValid`, into
native property values or callback values. A rule can be invalid with no message;
message length does not determine validity. Existing `BindValidation` string and
formatter overloads retain their signatures.

Use the imports matching your package:

```csharp
// Runic.ReactiveUI.Validation (Primitives)
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
```

```csharp
// Runic.ReactiveUI.Validation.Reactive (System.Reactive)
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.Binding.Reactive;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
```

The view implements the matching `IViewFor<TViewModel>`, and the model implements
`IReactiveObject` and `IValidatableViewModel`. As with existing view extensions,
the `viewModel` argument supplies type inference. The binding observes the view's
current `ViewModel`, including later replacement, rather than pinning that
argument's instance.

A helper has one state, so its converter takes `IValidationState`:

```csharp
using var enabled = view.BindValidationState(
    view.ViewModel,
    vm => vm.NameRule,                 // ValidationHelper? property
    v => v.IsNameValid,                // bool property
    static state => state.IsValid);

using var tone = view.BindValidationState(
    view.ViewModel,
    vm => vm.NameRule,
    static state => state.IsValid ? FieldTone.Neutral : FieldTone.Error,
    value => nativeControl.Tone = value);
```

`FieldTone` and `nativeControl` belong to the application; the library has no
platform color or control dependencies. The property form also supports enums,
custom value types, and nullable values, with the converter's output type
matching the target property. The callback form supports a custom dispatcher or
another presentation adapter.

A property can have several rules. Its converter receives
`IList<IValidationState>` containing their current states, in rule order. Treat
that list as read-only. Choose the aggregation explicitly:

```csharp
using var propertyValidity = view.BindValidationState(
    view.ViewModel,
    vm => vm.Name,
    v => v.IsNameValid,
    static states => states.All(static state => state.IsValid),
    strict: true);

using var propertyTone = view.BindValidationState(
    view.ViewModel,
    vm => vm.Name,
    static states => states.Any(static state => !state.IsValid)
        ? FieldTone.Error
        : FieldTone.Neutral,
    value => nativeControl.Tone = value,
    strict: false);
```

The `strict` argument is required: `true` includes rules validating only that
property; `false` also includes rules that validate that property alongside
other properties. No matching rules, an initially empty context, and a null view
model produce an **empty list**. Therefore `All(IsValid)` returns true and
`Any(!IsValid)` returns false for absence. An application that requires a rule
before accepting input can use `states.Count != 0 && states.All(...)` instead.

Existing rules are seeded from the context's actual current membership. The
binding never inserts a synthetic valid state for an active invalid rule.
Each active rule must emit an initial state; the combined projection waits for
all matching rules to emit. A rule that never emits can leave the last projected
value unchanged, so asynchronous validation needs an explicit pending policy in
the application's rule state. Rule addition, removal and clearing recompute the
list. Helper absence or a null view model produces `ValidationState.Valid` for
helper bindings.

Both forms detach replaced models and helpers; disposal stops updates and is
idempotent. Binding disposal owns the observation, not the model, context or
helper. Source states reach converters without text formatting or comparison
that discards custom metadata. Aggregate `ValidationContext` states remain the
context's ordinary aggregate state; they do not retain custom rule metadata.

For a browser bridge, project supported primitives or a public DTO and map them
to CSS/ARIA/frontend components in the frontend:

```csharp
public sealed record FieldStatus(bool IsValid, string[] Messages);

using var bridgeProjection = view.BindValidationState(
    view.ViewModel,
    vm => vm.NameRule,
    static state => new FieldStatus(state.IsValid, state.Text.ToArray()),
    value => bridgeModel.NameStatus = value);
```

The application owns DTO serialization and bridge publication. No DOM binding
or platform object serialization is added to this library. Custom state
converters must also handle the documented valid/empty absence values.

Assignments and callbacks execute on the source notification thread; use the
application's dispatcher when required by a native control. Expression-based
observation still carries dynamic-code and trimming annotations. Typed output
and callback assignment do not make expression observation Native AOT-safe.
