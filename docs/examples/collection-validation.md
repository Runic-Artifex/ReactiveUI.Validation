# Child collection validation

The executable [.NET 10 recipe](../../examples/CollectionValidation/CollectionValidationRecipe.cs)
uses ReactiveUI 26.0.1 and the matching **released** Runic.DynamicData
10.0.0-runic.5 flavor. It covers the collection goals from the dated review of
[#173](https://github.com/reactiveui/ReactiveUI.Validation/issues/173) and
[#450](https://github.com/reactiveui/ReactiveUI.Validation/issues/450).
Neither example participates in the core solution or normal package output.

From the repository root, bootstrap the released packages and run either flavor:

```sh
python3 eng/restore-fork-dependencies.py
dotnet run --project examples/CollectionValidation/Primitives/CollectionValidation.Primitives.csproj -c Release
dotnet run --project examples/CollectionValidation/Reactive/CollectionValidation.Reactive.csproj -c Release
```

On the Runic NixOS desktop, prefix each command with
`direnv exec /absolute/path/to/runic-sdk`. The root `global.json` selects SDK
10.0.401; the example project references the matching Validation flavor, which
supplies the existing pinned dependency cohort. No sibling DynamicData build is
used.

The [model](../../examples/CollectionValidation/CollectionValidationRecipe.cs),
[child](../../examples/CollectionValidation/RecipeChild.cs) and
[runner](../../examples/CollectionValidation/Program.cs) include all imports.
The same source is compiled twice with these namespaces and scheduler types:

| Concern | Primitives | System.Reactive |
| --- | --- | --- |
| Child storage/operators | `DynamicData` | `DynamicData.Reactive` |
| Reactive object | `ReactiveUI` | `ReactiveUI.Reactive` |
| Observable operators | `ReactiveUI.Primitives` | `ReactiveUI.Primitives.Reactive` |
| Validation | `ReactiveUI.Validation.*` | `ReactiveUI.Validation.Reactive.*` |
| Injected model scheduler | `ReactiveUI.Primitives.Concurrency.ISequencer` | `System.Reactive.Concurrency.IScheduler` |

The rule requires at least one child and a nonblank name on every child:

```csharp
var validity = Children.Connect()
    .StartWithEmpty()
    .AutoRefresh(child => child.Name)
    .QueryWhenChanged(static query => ChildrenHaveNames(query.Items));
_childrenRule = this.ValidationRule(model => model.Children, validity, ChildrenMessage);
```

`StartWithEmpty()` deliberately supplies an initial snapshot, including when the
source has no children. Empty means invalid for this domain. For a domain where
empty is allowed, change the domain predicate, rather than skip its initial
emission. `AutoRefresh(Name)` turns a child edit into a query refresh. A
membership-only `ToCollection()` chain is insufficient for that edit. The rule
keeps observing after initial validation; there is no `Take(1)` or cached initial
answer.

`SourceCache<RecipeChild, Guid>` uses immutable child identity. Editing a name
preserves the child instance and its key; replacing the instance at the same key
removes the old instance's observation. Removing or clearing children also
detaches them. The model owns the rule helper and child storage, disposing rule
subscriptions before storage. Call all mutations and child notifications on the
owning serialized model context. Do not use mutable names as cache keys. This recipe keeps its cache instance
stable. If a model replaces the entire collection source, explicitly switch its
inner observation (for example with `Select(...).SwitchTo()`) and define ownership
of the old source; same-key child replacement is the scope exercised here.

`IsCurrentlyValid` evaluates the same predicate against current domain data.
`ValidationContext.Valid` supplies ongoing aggregate observations;
`ValidationContext.GetIsValid()` checks current registered rules. The scheduled
`IsValid` property is a presentation value and can update later. The
[ordering recipe](validation-ordering.md) explains command admission and this
boundary. The standalone console runner injects an immediate scheduler explicitly;
applications should inject their own model-compatible scheduler.

Untouched invalid data is still invalid. `Touched` and `Submitted` affect only
`ShowChildrenError`, so hiding the initial error never enables submission. These
console presentation flags are simple reads; a view model that binds them should
add its usual observable property notifications. Commands must guard current
domain invariants when invoked directly or after asynchronous work resumes.

Associating the collection rule with `Children` makes its message available from
`INotifyDataErrorInfo.GetErrors("Children")`. That is a collection-level error,
not a generated error path for every child. To export individual child errors,
give each presented child its own error contract and bind it to the model context.
For a generated frontend contract, expose a supported public collection shape
through generated metadata. The internal cache is storage for this standalone
recipe. A bare context rule without property association can block aggregate validity
without appearing in `ReactiveValidationObject.GetErrors`; explicitly provide an
entity/property error contract when the bridge must render it.

[CollectionValidationRecipeTests](../../src/tests/ReactiveUI.Validation.Tests/CollectionValidationRecipeTests.cs)
compile the example model against both flavors. They cover initial empty state,
add, edit, remove, clear, same-key replacement, stale-child detachment and
subscription disposal. They use the released package pair and do not certify
concurrent mutation or unreleased DynamicData operator fixes.
