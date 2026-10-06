# Producer declaration profiles

The frontend predicts declarations for semantic analysis. It does not execute a
peer generator, load analyzer assemblies, or emit predicted declarations. The
original syntax trees remain in the private compilation, so encoded interception
locations still identify the user's original calls. A final diagnostic analyzer
compares recorded type/member contracts with actual producer output.

The reference contracts are MIT licensed:

| Producer | Version | Source revision |
| --- | --- | --- |
| ReactiveUI.SourceGenerators | 4.2.0 | `c8a8c38dd7192073540e187b8e8abc8d53e16b1a` |
| ReactiveUI.Binding.SourceGenerators, including Reactive flavor | 9.1.0 | `05c45cec845835d670960fac733bb302bc1c1071` |
| Avalonia.Generators (Avalonia package) | 12.1.3 | `8eeda4f6f546165b3f72e63c9f42247abb306905` |
| Microsoft.Maui.Controls.SourceGen | 10.0.110 | `6c379bf1dc24461a985a093be3fd8a3e9bc6fc00` |

`ProducerMemberExtractor` adapts Binding's
`Helpers/SourceGeneratorsMemberExtractor.cs`; `ProducerProjection` adapts the
declaration-only architecture in `Generators/SourceGeneratorsCompilation.cs`.
`ProducerXamlProjection` retains attribution for Binding's XAML reader/resolver.
The inherited copyright notices and the repository MIT license apply. Semantic
details also follow the actual 4.2 producer executors: nullable type rendering,
private-protected access, field inheritance/required/init settings, notification
inheritance, command return/parameter/naming rules, collection getters/setters,
declared partial properties and generated host contracts.

Resolved versions come from restored analyzer assets via the packaged
`buildTransitive` props. Missing or mismatched metadata produces `RUVG009` when a
profile is consumed by normal Validation generation. Applications using only
explicit typed descriptors can set `ReactiveUIValidationProducerProjectionEnabled`
to `false`; this turns off declaration prediction and never relaxes the final
normal-dispatch guard. The final check (`RUVG008`) compares declaring owner identity,
member kind, type/nullability, accessors/init, required/virtual/override/ref/index
contracts and interface/base inheritance. The real producer still owns its
validity diagnostics, implementation, notification and initialization behavior.
The checks do not certify arbitrary producer versions from a matching property
name.

The pinned 4.2.0 `[IReactiveObject]` executor emits its implementation at namespace
scope and does not wrap nested containing types. A nested attributed class that
depends on that generated interface therefore fails the final contract check
with `RUVG008`. Use a top-level generated class, or explicitly declare the
`ReactiveObject` base or `IReactiveObject` implementation. Nested `[Reactive]`
properties remain supported when their containing class has an actual declared
notification base. The frontend does not repair the external producer's output
by emitting a substitute implementation.

MAUI/Avalonia named-field prediction uses marked AdditionalTexts and build
options, resolves CLR assembly qualifications and unambiguous XML namespace
definitions, preserves accessibility and handles generic type arguments.
The platform profiles follow the pinned actual name producers, including MAUI
root-base/name/type-argument rules and Avalonia fields versus get-only properties,
control eligibility, enum options and name property elements. Malformed or
unresolvable marked input produces `RUVG010`. WPF/WinUI members
already supplied before Csc are used directly. Compiler fixtures for typed XAML
fields are not evidence of executing a native UI framework or its workload.

WinForms ViewModelControlHost generates a nongeneric `IViewFor` and an
`object? ViewModel`; RoutedControlHost generates routing properties without a
generic `IViewFor<T>`. The frontend preserves these actual contracts. A typed
view adapter or explicit descriptor supplies a typed model route; prediction
never invents a generic interface or initialized target control.

Generated calls emitted by an unrelated peer cannot be intercepted retroactively.
Peers can emit explicit typed runtime calls or opt into the finite typed runtime
dispatch contract. Ordinary uncovered normal calls and method groups retain
final errors. Runtime dispatch opt-in does not promise that a registration exists;
unregistered execution fails actionably, without reflective discovery.
