# Separate blocking rules from advice

`IValidatableViewModel.ValidationContext` remains the default context. Existing
`ValidationRule`, `IsValid`, `BindValidation` and `ReactiveValidationObject`'s
`INotifyDataErrorInfo` behavior continues to use that context. An additional
context does not automatically join it. Keep command admission tied to blocking
rules, and display advice through a separately selected context.

The new overloads accept an `IValidationContext` as their first argument:

```csharp
using var advice = new ValidationContext(modelScheduler);
using var blockingRule = model.ValidationRule(
    vm => vm.Name,
    name => !string.IsNullOrWhiteSpace(name),
    "A name is required.");
using var adviceRule = model.ValidationRule(
    advice,
    vm => vm.Name,
    name => name?.Length >= 8,
    "A longer name is easier to recognize.");

// Advice can be invalid while the default context still permits Save.
using var saveGate = model.ValidationContext.Valid.Subscribe(
    valid => saveButton.IsEnabled = valid);
```

Use the scheduler for your serialized model updates. The Primitives package
accepts an `ISequencer`; the Reactive flavor accepts a System.Reactive
`IScheduler`. Choose the corresponding namespaces and scheduler for the package
in use. `Valid` is an ongoing observation; `GetIsValid()` checks the current rule
values synchronously. Pending asynchronous rules still need an explicit domain
policy for command admission.

## Selecting a context in a view

Expose an observable context property on a reactive view model. Its owner must
retain and dispose all contexts it creates, including contexts replaced later:

```csharp
public IValidationContext? AdviceContext
{
    get;
    set => this.RaiseAndSetIfChanged(ref field, value);
}
```

Assign the advisory context to that property, then bind it explicitly:

```csharp
model.AdviceContext = advice;

using var nameAdvice = view.BindValidationContext(
    model,
    vm => vm.AdviceContext,
    vm => vm.Name,
    v => v.NameAdviceText);

using var allAdvice = view.BindValidationContext(
    model,
    vm => vm.AdviceContext,
    v => v.AllAdviceText);

using var adviceState = view.BindValidationContext(
    model,
    vm => vm.AdviceContext,
    state => adviceIndicator.IsVisible = !state.IsValid);

using var propertyStates = view.BindValidationContext(
    model,
    vm => vm.AdviceContext,
    vm => vm.Name,
    states => nameAdviceIndicator.IsVisible = states.Any(state => !state.IsValid));
```

`model` is used for type inference. The binding observes `view.ViewModel`, follows
both view model and selected-context property replacement, and detaches previous
subscriptions. A null model or selected context clears text, delivers an empty property-rule
list and delivers a valid aggregate state. String targets display aggregate text or the first nonempty property-rule
message. They support an optional text formatter; property bindings also support
`strict: false` to include rules that validate additional properties. Both modes
match the exact property path; `strict: true` includes only rules that validate
that property exclusively. Callbacks receive validation states directly, so
native controls or browser projections can map validity and text without deriving
validity from an empty message. Callbacks run on the
source notification thread; marshal UI assignment explicitly when necessary.

Property callbacks receive the actual matching rule states. No matching rules
or a null selection produces an empty list; existing invalid rules do not emit
a synthetic valid or empty prelude. A newly selected empty context resets
display, and rules added later are observed. A rule source that has not supplied
an initial state must emit before a populated list can be combined. These
callbacks are presentation observations, not a substitute for synchronous
blocking command admission.

## Rule shapes and ownership

The explicit-context surface supports property predicates with static or
value-dependent messages, observable booleans with static messages, and
`IObservable<IValidationState>` streams. Both observable forms have whole-model
and property-associated overloads. Observable sources should provide an initial
value. Project a richer observable into `IValidationState` when you need dynamic
messages or multi-property logic; this intentionally avoids copying every
existing rule overload into the new surface.

Each rule helper captures the exact context passed during registration.
Replacing `AdviceContext` changes bindings, but does not move registered rules.
Disposing a helper removes and disposes only its own rule in its captured context;
it does not dispose that context or another rule. Binding disposal only releases
subscriptions. Dispose helpers before their contexts, and dispose all owned
contexts when the model is released. Repeated binding/helper disposal is safe.

Keep named contexts separate unless you deliberately want their rules to block
commands. The default `INotifyDataErrorInfo` channel does not export advice merely
because a view model has another context property. A browser adapter should
publish explicit advice text/state groups and keep its Save gate tied to the
default blocking context.

The design adopts the explicit-context/selected-binding idea from
[upstream issue #511](https://github.com/reactiveui/ReactiveUI.Validation/issues/511)
and its older prototype, using the current `IValidationContext` abstraction and
captured cleanup ownership. Existing signatures are preserved in both flavors;
no dependency or scheduler defaults change.
