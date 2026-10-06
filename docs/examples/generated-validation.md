# Generated validation and explicit Unsafe migration

The generated/Unsafe API split is **unreleased**; published packages keep their
earlier runtime behavior. See the [design](../generated-validation-design.md) and
the [capability reference](../generated-capabilities.md) for the full contract.

## Consume the matching core package

Use `Runic.ReactiveUI.Validation` with ReactiveUI/Runic.DynamicData, or
`Runic.ReactiveUI.Validation.Reactive` with ReactiveUI.Reactive/
Runic.DynamicData.Reactive. Keep exactly one matching flavor. Imports are
`ReactiveUI.Validation.Extensions`, `.Contexts`, `.Helpers`, `.Abstractions`,
`.States` and `.Capabilities`, or the same namespaces below
`ReactiveUI.Validation.Reactive`. The dependency cohort is ReactiveUI 26.0.1 and
Runic.DynamicData 10.0.0-runic.30.

Both core packages embed `ReactiveUI.Validation.SourceGenerators.dll` and
flavor-named `buildTransitive` props allowlisting `ReactiveUI.Validation.Generated`.
Keep both analyzer and build assets and compile with SDK 10.0.401 / C# 14. No
additional generator package is needed. If generation is missing, inspect the
real compiler analyzer inputs and imported props.

## Inline normal calls

These fragments use accessible notifying types and inline property paths.
Observed chain owners must be reference types implementing
`INotifyPropertyChanged`; selectors used only as rule metadata need no
observation. Binding view static types must also be notifying reference types:
concrete struct views fail with `RUVG006`, while a stable box referenced through
a notifying view interface works. Store each returned helper/binding under an
application owner and dispose it at the matching lifetime boundary.

```csharp
var nameRule = model.ValidationRule(
    vm => vm.Name,
    static name => !string.IsNullOrWhiteSpace(name),
    "name-required");

var postcodeRule = model.ValidationRule(
    model.Advisory,
    vm => vm.Address!.Postcode,
    static postcode => postcode?.Length == 5,
    "postcode-required");

var nameText = editor.BindValidation(
    editor.ViewModel,
    vm => vm.Name,
    view => view.ErrorText);

var advisoryText = editor.BindValidationContext(
    editor.ViewModel,
    vm => vm.Advisory,
    view => view.AdvisoryText);

var richStatus = editor.BindValidationState(
    editor.ViewModel,
    vm => vm.AddressRule,
    view => view.Status,
    ProjectCompleteState);
```

Inline lambdas bind to the typed `Func` overloads, so no expression tree is built.
A stored `Expression<Func<...>>` variable binds to the original overload and
needs registration (below) or Unsafe.

`ProjectCompleteState` may return an enum or nullable custom struct; it receives
complete custom states including code, severity and revision, so validity need
not be inferred from message text. Context aggregation keeps its aggregate-state
contract, and named advisory contexts stay independent of the default blocking
context and error export.

A null intermediate rule owner emits `default` (null for reference/nullable
values), which the predicate must handle; it does not keep the old value. Null
model/helper/context selections emit valid aggregate state or an empty
property-state list. Normal property text bindings show initial invalid text
immediately; explicit Unsafe property callbacks keep the legacy empty-list
prelude, so migration changes that initial sequence.

Strict property bindings match the exact full root path. `strict: true` also
excludes rules associated with more than that one property. For a combined
Email/Confirmation rule, keep one component with both paths through the explicit
observable API; two independent rules have different membership.

For nested targets such as `view => view.Output!.Text`, parents must be notifying
reference types and the final setter accessible. Replacement receives the latest
output; a null target skips assignment and keeps that output for the next target.
Callbacks and setters run on the source notification thread: dispatch UI
presentation explicitly, while domain validity stays synchronous on its owner.

## Explicit generic observable bindings

The six safe observable methods use traditional `this` extensions to avoid a
C# 14 nullable-generic `CS8714` bug. Inferred calls are unchanged. If you
supplied only the output type, supply source and output type:

```csharp
// Previously: sources.BindObservableValidationState<Presentation?>(...)
sources.BindObservableValidationState<Customer, Presentation?>(
    static customer => customer.AddressRule?.ValidationChanged,
    ProjectCompleteState,
    value => editor.Status = value);
```

`BindObservablePropertyValidationState<TSource, TOut>` needs the same two type
arguments. `AddObservableRule<TValue>` keeps its single type argument.

## Runtime selectors and precompiled consumers

A stored selector, delegate or compiled peer can deliberately select registered
normal dispatch. Attach a finite `ValidationPlanRegistry` to the source/view
owner, register role-specific typed selector/target plans and keep the
attachment and registration leases. Source callers declare
`ValidationRuntimeDispatchAttribute`. Instance entries match one expression;
`RegisterSelectorPattern`/`RegisterTargetPattern` match fresh expressions
against finite schemas and bind each call's current receiver and typed
arguments. Closure fields are never reflected; supply a typed extractor or
provider. Ambiguity or missing registration fails actionably. A native
executable must be relinked to use this; replacing its DLL changes nothing.

```csharp
[ValidationRuntimeDispatch]
static void ValidateConfigured(Customer model, IValidationContext context,
    Expression<Func<Customer, string?>> configured)
{
    ValidationPath[] names = [ValidationPath.Legacy("Name")];
    var typed = ValidationSelector.Create(model, current =>
        ValidationAccessPlan.Create(
            () => ValidationRead.Present(current.Name, names),
            [ValidationDependency.PropertyChanged(() => current, nameof(current.Name))]));
    using var plans = new ValidationPlanRegistry(capacity: 1);
    using var attachment = plans.Attach(model);
    using var registration = plans.RegisterSelector(
        ValidationPlanRole.RuleValue, configured, typed);
    using var rule = model.ValidationRule(context, configured,
        static value => !string.IsNullOrWhiteSpace(value), "name-required");
    model.Name = "Ada";
}
```

The application promises that `configured` denotes the registered Name
operation; the typed body executes it. The helper owns the live rule; the leases
own future lookup availability, so a longer-lived rule must keep its helper
alive beyond this method.

Generation follows a selector only when its value is provably fixed. A static
abstract/virtual interface factory, a readonly field rewritten in a static
constructor, or a mutable local passed through an implicit `in` parameter are
runtime choices and need this registration route. For generated aliases, an
exact registered match takes precedence over the compiled descriptor; only
genuine absence uses the descriptor, and ambiguity or factory failure still fails.

Explicit Unsafe remains available for deliberately reflected expressions:

```csharp
Expression<Func<Customer, string?>> configuredSelector = GetSelector();
var configuredRule = model.ValidationRuleUnsafe(
    configuredSelector,
    static value => !string.IsNullOrWhiteSpace(value),
    "value-required");
```

It keeps `RequiresUnreferencedCode`. Uncovered normal calls get `RUVG005`,
indirect references `RUVG007`; the runtime opt-in does not supply a registration
or hide reflection. Existing compiled managed IL needs preserved-ABI registration
or recompilation.

## Typed recipes for explicit capabilities

Use `ReactiveUI.Validation.Capabilities`, `.Contexts` and `.States` (add
`.Reactive` after `.Validation` for the Reactive flavor).

A silent mutable owner can live in a reference cell. Replacing the cell value or
calling `Invalidate` publishes a new snapshot. `Draft.Name` is a plain field;
the captured minimum is part of the selected value so equality cannot suppress a
configuration change.

```csharp
var storage = ValidationCell.Create(new Draft());
var minimum = 3;
ValidationPath[] paths = [ValidationPath.Legacy("Name")];
var selector = ValidationSelector.Create(storage, cell =>
    ValidationAccessPlan.Create(
        () => ValidationRead.Present(
            (Name: cell.Value.Name, Minimum: minimum), paths),
        [ValidationDependency.PropertyChanged(() => cell, nameof(cell.Value))]));
using var rule = ValidationRuntime.RegisterRule(storage, context, selector,
    value => new ValidationState(
        value.Name?.Length >= value.Minimum, "name-too-short"));
storage.Value.Name = "Ada";
storage.Invalidate();
minimum = 4;
storage.Invalidate();
```

For an immutable record struct `Form(string? Name)`, a typed lens returns the
complete replacement instead of changing a boxed copy. Immutable reference
records work the same way.

```csharp
var form = ValidationCell.Create(new Form(null));
var name = new ValidationLens<Form, string?>(
    value => ValidationRead.Present(value.Name, paths),
    static (value, next) => value with { Name = next });
using var formRule = ValidationRuntime.RegisterRule(
    form, context, name.Selector(),
    static value => new ValidationState(!string.IsNullOrWhiteSpace(value), "name-required"));
var outputTarget = name.Target(); // typed copy-back factory, no subscription yet
form.Value = new Form("Ada");
```

`using var output = outputTarget.Bind(form).Bind(values)` writes an
application-owned `IObservable<string?>` through the current cell. Keep the
transformation pure and return all enclosing struct/immutable values.

For a dynamic selected index, build the value and structural path from that
index in the same read and register the collection/index dependencies. A custom
provider is a typed application subscription, not library discovery:

```csharp
var changes = ValidationDependency.Create(
    () => table,
    static (owner, observer) => owner.SubscribeInvalidation(observer));
var indexed = ValidationSelector.Create(table, owner =>
    ValidationAccessPlan.Create(() =>
    {
        var index = owner.SelectedIndex;
        ValidationPath[] current = [ValidationPath.Structural(
            $"Rows[{index}].Name", index, EqualityComparer<int>.Default)];
        return index >= 0 && index < owner.Rows.Count
            ? ValidationRead.Present(owner.Rows[index].Name, current)
            : ValidationRead<string?>.Missing(current);
    }, [changes]));
```

`table.SubscribeInvalidation` must notify index, item and source changes and
return cleanup. A metadata-only property binding also supplies `readPaths` so
matching does not run the leaf getter. Choose DefaultValue/Suppress/Fallback
deliberately; a present null leaf stays distinct from a missing owner.

Reordered named index arguments in an expression tree are `CS9307`. Use a typed
delegate with an explicit dependency/path plan:

```csharp
// Legal as a Func; the same body in Expression<Func<...>> is CS9307.
Func<Table, string> read = owner => owner[second: owner.Second, first: owner.Key];
var namedIndex = ValidationSelector.Create(table, owner =>
    ValidationAccessPlan.Create(
        () => ValidationRead.Present(read(owner), paths),
        [ValidationDependency.Create(() => owner,
            static (current, observer) => current.SubscribeInvalidation(observer))]));
```

Other explicit options: `ValidationInitialSequence.LegacyEmpty`/`LegacyValid`
restore the legacy presentation prelude; `ValidationOutput` adds output-type-first
factories; `ValidationSnapshot.Read`/`Validate` borrow synchronously (including
ref structs); `GeneratedValidationAccessAttribute` selects existing init/readonly
storage; `ValidationAsync.ForLatest`, `ValidationCollection.Observe` and
`ValidationRowLease` own asynchronous and row lifetimes; `ValidationScheduling`
dispatches presentation while domain admission stays synchronous.

## Choosing a typed route

| Application pattern | Route |
| --- | --- |
| Silent POCO, field or captured configuration | `ValidationSelector.Create` with typed read/dependencies; `ValidationCell.Invalidate` or a `ValidationDependency.Create` adapter reports changes. Silent writes produce no events by themselves. |
| Runtime selector/factory choice, static interface dispatch or rewritten readonly selector | `ValidationPlanRegistry`/`IValidationPlanProvider` with per-call typed factories. |
| Fresh expressions from unchanged precompiled IL | Structural pattern registrations with typed arguments and scoped leases; unknown closure fields need a supplied extractor. |
| Generic, private, file-local or anonymous shape | Typed factory in the caller's legal context or a lexical bridge in a partial type. Members of a private generic class nested in a non-partial type need the enclosing type made partial or a `ValidationLens`/`ValidationCell` factory. |
| Nested mutable structs or immutable replacement | `ValidationCell<T>` and `ValidationLens<TStorage,TValue>` write back from current storage. A by-value stack root cannot become stable storage. |
| Init setter, named readonly field or get-only property | `GeneratedValidationAccessAttribute` selects existing storage; otherwise supply a replacement/target factory. Getter-only backing fields are never guessed. |
| Readable getter with a protected setter | Supply a setter in a legal derived context; a base-typed receiver does not grant setter access. |
| Ref-struct or borrowed stack data | `ValidationSnapshot.Read`/`Validate`; copy into owned storage for live observation. |
| Generated controls/members and UI notification | Producer projection, or `ValidationViewAdapter`/typed provider over the final members. |

`ValidationPath.Legacy("Address.Name")` selects that full ordinal name even
against structural metadata. Two Structural paths with the same display name
keep distinct member/index/conversion identities. A legacy `"Name"` does not
match `"Address.Name"`, and strict matching still requires exclusivity.

## Ownership

Disposing a binding detaches its subscriptions; it does not dispose the selected
model, helper or context. Disposing a rule helper removes its component from the
context captured at registration. Dispose helpers before their contexts.
Collection owners dispose removed row helpers and own request cancellation;
generation does not infer asynchronous work from a selector.

Generated notification sources own their event registrations and rebind complete
chains after reentry through a serialized pending-refresh drain. They detach on
replacement, null, disposal and initial getter/callback failure. Initial failures
keep the original exception; cleanup failures are reported alongside it.
Caller-supplied observables keep their own cleanup obligations.

## Executable corpus

The [GeneratedValidation corpus](../../examples/GeneratedValidation/README.md)
compiles the same source for both flavors against the actual packed packages and
covers every case named in the [capability reference](../generated-capabilities.md).
`Negative.cs` holds the compile-negative configurations. Run it through the
strict gate from the repository root, in the locked SDK environment, after
`python3 eng/restore-fork-dependencies.py`:

```sh
python3 eng/verify-generated-validation.py --rid linux-x64 --mode all
```

Without `--package-feed FEED` the gate packs the current clean source; `--mode`
selects `managed`, `trimmed`, `native` or `all`; `--output` defaults to
`artifacts/verification/generated-gates`. `win-x64` runs on a Windows x64 host.
