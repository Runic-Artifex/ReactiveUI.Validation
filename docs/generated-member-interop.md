# ReactiveUI generated-member interoperability investigation

This 2026-10-06 investigation compares actual ReactiveUI generation patterns with Validation's
existing generated API. It starts from Validation documentation/source
`4ca1ee98dd04a8998be3da54da2595cc735cf4a3`; it does not implement interoperability,
change dependency pins, adopt DynamicData changes, or publish a package. The
generated/Unsafe split's previous verification remains separate evidence.

The tested cohort is ReactiveUI.SourceGenerators **4.2.0** at
`c8a8c38dd7192073540e187b8e8abc8d53e16b1a`, ReactiveUI.Binding and its Reactive
flavor **9.1.0** at `05c45cec845835d670960fac733bb302bc1c1071`, and ReactiveUI
**26.0.1**. Validation probes use retained `8.1.0-runic.0.790.17.15.10` candidate packages,
SDK **10.0.401**, C#14 and Roslyn **5.9**. These are versioned contracts, not
promises about arbitrary generators or future producer versions.

The original executed investigation snapshot is `9ad0d6a`, which preserves the
fixture sources, runner provenance and measured hashes; it is an investigation
snapshot, not a production interoperability implementation. Its released
DynamicData **10.0.0-runic.5** / Validation **.790.17.15.10** cohort remains
historical after main adopted DynamicData **10.0.0-runic.30**. These results do
not establish interoperability for that newer dependency cohort.

## Visibility and the immediate convention

Ordinary semantic/source-output generation inspects the consumer compilation,
which can include fixed post-initialization attribute/helper declarations. The
Reactive attributes are available; peer property implementations emitted through
normal source output are not inputs to Validation's generator. Those outputs
appear in the final compilation. Packaging both analyzers together or reversing
their registration order does not create a producer/consumer phase. Roslyn's
[pipeline documentation](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
describes that boundary.

Declare the property contract in consumer source when possible:

```csharp
internal sealed partial class Customer : ReactiveValidationObject
{
    [Reactive] public partial string? Name { get; set; }
}
```

SourceGenerators implements the property body, while Validation can already see
its name, type, nullable annotation and accessors. The same principle applies to
Binding's `[ObservableAsProperty]` partial get-only declaration, whose generated
helper is initialized with `ToProperty`. SourceGenerators 4.2 does not supply the
old `InitializeOAPH` pattern; OAPH moved to Binding in the 4.0 migration. A model
in a separately compiled assembly also exposes its generated properties as
ordinary metadata. Neither case needs prediction.

By contrast, `[Reactive] private string? _name` has no original `Name` property
symbol. A second, independent gap occurs when `[IReactiveObject]` supplies the
notification interfaces: a declared property alone does not make its owner
statically notifying before generation. Model rules also require their original
`IReactiveObject` contract, which carries INPC; views require `IViewFor<T>` and
INPC. Deriving an appropriate ReactiveObject base or explicitly declaring all
required API/notification interfaces solves that visibility question today.
It does not widen Validation's accessibility, closed type or reference-owner rules.

## What Binding actually does

Binding's pinned
[SourceGeneratorsCompilation](https://github.com/reactiveui/ReactiveUI.Binding.SourceGenerators/blob/05c45cec845835d670960fac733bb302bc1c1071/src/ReactiveUI.Binding.SourceGenerators/Generators/SourceGeneratorsCompilation.cs)
collects recognized producer attributes and builds a private synthetic declaration
tree. It adds that tree to a copy of the original compilation, then reads
original call sites against the augmented semantic model. **It never emits the
declaration tree.** The real producer supplies runtime implementations; original
source locations remain available for interception.

Its internal extractor covers Reactive fields, commands, collection/list
properties, notification interfaces and some XAML members. That is a concrete
compatibility architecture, not a public orchestration API Validation can call.
The extractor's comment targets SourceGenerators 4.0; an actual 4.2 package run
does not establish parity for every mapping. The
[producer implementation](https://github.com/reactiveui/ReactiveUI.SourceGenerators/blob/c8a8c38dd7192073540e187b8e8abc8d53e16b1a/src/ReactiveUI.SourceGenerators.Roslyn/Reactive/ReactiveGenerator.Execute.cs)
also controls nullable types, prefix naming, accessor access, inheritance,
required/init behavior and containing declarations. Prediction must reproduce
the supported subset exactly and reject uncertain cases.

For example, `_name`, `__name` and `m_name` predict `Name`; field visibility does
not make that generated property private. Command generation separately maps
synchronous, Task and observable methods, command names, input types and
cancellation tokens. SourceGenerators selects the Primitives or System.Reactive
command factory from referenced runtime symbols. Those command contracts deserve
their own tests rather than being inferred from a successful Reactive field.

## Evidence matrix

The package probes live in `investigations/GeneratorInterop/`; the separate
`ProjectionProof/` compiler host investigates one private declaration projection.
Its adapter runs unchanged Validation in an **inner** driver over the private
synthetic compilation; the actual ReactiveUI and Binding producers remain in the
outer same-input driver. Only Validation output is forwarded. This tests original
interception and feasibility in reversed outer registration orders, not a
production incremental pipeline, general prediction parity or native support.
Execution rows below distinguish package support from prototype feasibility.

| Pattern / question | Current observation | Final evidence status |
| --- | --- | --- |
| Declared Reactive partial properties and Binding partial OAPH | Both flavors pass initial validation, text/rich targets, nested/model/context replacement/null paths, OAPH notification and disposal checks. | Two managed and two full-trim NativeAOT passes. |
| Generated-only Reactive fields: rule, helper, context and writable target selectors | Each position rejects with `RUVG001` + `RUVG005` in both flavors. | Eight managed negative builds match. |
| Declared property with generated-only IReactiveObject interface | Original owner lacks the notification contract; `RUVG001` + `RUVG005`. | Two managed negative builds match. |
| Observable ValidationRule plus generated SubmitCommand metadata selector | Early false-positive `RUVG001`; final safe observable overload correctly gets no `RUVG005`. | Two managed negative builds match; candidate false positive confirmed. |
| Explicit AddObservableRule fed by Binding WhenAnyValue over a generated field | Binding observes the producer's field-generated property; Validation consumes the supplied stream. | Two managed and two full-trim NativeAOT passes. |
| One private nullable Name projection, actual producer + Binding + Validation | Original Validation and Binding calls are intercepted; real producer property executes; projected tree is absent from output. | Four managed passes: both flavors × both outer orders; unprojected controls reject with `RUVG001` + `RUVG005`. |
| Roslyn early-output API | Available in Roslyn 5.9; syntax/semantic input restriction prevents this attribute-based shortcut. | Primary API/source documentation inspected. |

The unchanged [read-only source audit](../investigations/GeneratorInterop/evidence/source-generator-contracts.txt)
is retained with the investigation (SHA256
`abe4f50a3679f37a79571b2110a642e5c5a29fc275b49dcdee982033a5dc85a2`). It records
pinned source-file/line contracts and source-derived risks; it is not runtime or
NativeAOT proof. The [managed package report](../investigations/GeneratorInterop/evidence/managed-results.json)
records 16 matched configurations: four executing positives and twelve negative
builds, with no warnings. It retains restored package/analyzer hashes, fixture
hashes, compiler inputs, generated sources and exact diagnostics. The
[partial-member native report](../investigations/GeneratorInterop/evidence/native-partial-results.json)
and [field-observable native report](../investigations/GeneratorInterop/evidence/native-field-results.json)
record four full-trim `linux-x64` NativeAOT publishes/executions, with zero
warnings/errors and runtime AOT assertions. Both positive configurations include
a passing declared-selector safe metadata control, distinguishing the generated
command's early false positive. No separate full-trim managed or Windows interop
execution is claimed. The [summary](../investigations/GeneratorInterop/evidence/summary.json)
and [manifest](../investigations/GeneratorInterop/evidence/manifest.json) identify
retained inputs and proof hashes. The Validation package source is `2550376b…`;
the local pair hashes are `ffea7c20…` (Primitives) and `1f1cb6b0…` (Reactive),
distinct from the earlier CI package pair.

After the dependency merge, the revised runner's
[focused reproduction record](../investigations/GeneratorInterop/evidence/reproduction-smoke.json)
records two clean managed `PARTIAL` executions, retaining the historical
`.5`/`.10` cohort. It identifies the revised runner/fixture hashes separately;
the original 90 evidence-manifest entries remain unchanged. This is not a rerun
of the native or full configuration matrix.

The [projection manifest](../investigations/GeneratorInterop/ProjectionProof/evidence/manifest.json)
records four sources per run, zero final-dispatch diagnostics and the trace
`initial-invalid → Ada-valid → null-invalid → disposed`; Binding continues with
`[null,Ada,null,Detached]`. Strict host builds pass with zero warnings/errors.
The private compiler matches SDK defaults `NoWarn=1701;1702`; initial raw
System.Reactive framework-identity warnings are retained in the
[prototype evidence](../investigations/GeneratorInterop/ProjectionProof/README.md).
No trim/AOT warnings are suppressed; this prototype has no native execution.

The separate [portable projection replay](../investigations/GeneratorInterop/ProjectionProof/portability-replay.json)
passes four managed flavor/order cases with clean strict builds and zero final
dispatch diagnostics, using verified historical feeds and unchanged original
fixture bytes. Original evidence/logs remain unchanged. It distinguishes the
tested compiler snapshot from three later output-guard checks and the current
runner hash; it adds no native or newer-dependency proof.

## Why early output is not a simple switch

Roslyn 5.9 exposes experimental `RegisterPreCompilationSourceOutput`. Its
[documented contract](https://github.com/dotnet/roslyn/blob/main/docs/features/pre-compilation-source-outputs.md)
permits early output derived from non-compilation inputs such as additional
files, parse options and analyzer configuration. It forbids dependencies on
`SyntaxProvider` or `CompilationProvider`. Reactive attributes, field types,
interfaces and semantic accessor checks need precisely those forbidden inputs.
Post-initialization output has no such inputs either. Reworking the producer to
consume a separate declarative input would be a different contract, not an
ordering fix for existing model source.

## Recommended bounded stages

1. Refine unresolved-call candidate classification before adding projection.
   The command-metadata failure comes from accepting any normal-stub candidate
   during incomplete overload resolution, although the final selected overload
   is safe and needs no interceptor. Add an actual-package regression and retain
   the final dispatch analyzer; this isolated fix does not make generated-only
   normal selectors visible. No production fix is made in this investigation.
2. Document and test the declared partial-property convention. Keep explicit
   notification bases/interfaces, original source selectors and existing
   Validation ownership/null/disposal rules. Retain generated-only failure cases
   so documentation does not imply broader support.
3. If existing field-based models must remain source-compatible, implement a
   versioned private descriptor/projection for a narrow Reactive field subset.
   Begin with accessible closed notifying owners and nullable property types.
   Preserve original call locations; emit only Validation operations. Validate
   actual producer members and interception in the final compilation, rejecting
   mismatches rather than falling back to reflection.
4. Treat generated notification interfaces as an additional explicit capability.
   Expand only with differential tests for real producer contracts: access,
   naming collisions, inheritance, nullability and supported containing types.
   Commands, collections, XAML, private hosting and generic hosting require
   independent scope decisions and proofs.

A shared, supported extraction component with Binding would reduce duplicate
contract maintenance. Today its helpers are internal. Copying a small extractor
requires license attribution, source/version pins and differential tests; copying
the whole Binding analyzer imports unrelated plugins and platform obligations.
Forking or unifying the producer pipeline offers control at a much higher
maintenance cost. Co-bundling independent DLLs offers neither shared extraction
nor ordering. No upstream coordination or adoption is claimed here.

Explicit supplied observables remain a useful route where their calls actually
compile. Binding's public typed `ObservedProperty` API is intended for generated
code, but provider selection, null-parent behavior and lifetime can differ from
Validation's direct observation. Any adapter needs its own semantic tests.
Generating `WhenAnyValue` calls and expecting Binding to process them later
would repeat the ordering problem.

The safe observable/callback APIs avoid reflection-based observation/assignment;
safe metadata overloads may inspect selector metadata without compiling it.
Explicit Unsafe retains its warning-bearing runtime-expression behavior. A
managed co-run proves neither arbitrary providers nor NativeAOT compatibility.
Only actual trimmed/native executions can establish those claims for the tested
cohort. This investigation changes no production support boundary, published
release or DynamicData dependency.
