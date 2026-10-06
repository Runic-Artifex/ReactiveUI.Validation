# Private semantic projection feasibility proof

This bounded, nonshipping prototype passes for both Validation flavors and both
outer generator orders. A private declaration lets the unchanged packaged
Validation generator analyze a `[Reactive]` field-generated property. The final
assembly uses the real ReactiveUI generated property and notifications, the real
Binding `WhenAnyValue` interceptor, and Validation's generated rule interceptor.
The projected declaration never enters the final compilation. The retained
evidence and four original logs describe the tested snapshot committed as
`9ad0d6a`. Their source hashes refer to that original snapshot. The current runner
adds path parameterization and writes new reproduction records separately; it
does not rewrite those historical files or claim tests against the current
production dependency graph.

## Inputs

- Locked .NET SDK **10.0.401**, compiler host Roslyn **5.9.0**.
- ReactiveUI.SourceGenerators **4.2.0**, packaged `roslyn5.0` analyzer.
- ReactiveUI.Binding / ReactiveUI.Binding.Reactive **9.1.0**, packaged
  `roslyn4.13` analyzer.
- Validation candidate pair **8.1.0-runic.0.790.17.15.10**, retained from source
  `2550376b230ccfb78beb8d3b4ced8866b65d809e`, packaged `roslyn5.9` analyzer.
- ReactiveUI runtime **26.0.1**, released DynamicData pair
  **10.0.0-runic.5**. No production package pins change.
- Binding private-projection implementation inspected at commit
  `05c45cec845835d670960fac733bb302bc1c1071`:
  `SourceGeneratorsCompilation.cs` and `SourceGeneratorsMemberExtractor.cs`.

[evidence/manifest.json](evidence/manifest.json) records package/archive hashes,
analyzer hashes, inspected source hashes, proof source hashes, revisions, and all
four results. The runner verifies each loaded analyzer matches its real packaged
archive bytes. It uses one flavor's MSBuild reference set at a time.

## Stages

1. Parse the original consumer once. It declares only `[Reactive] private
   string? _name = null;`, with calls selecting the missing `Name` property.
2. A single outer driver contains the real packaged ReactiveUI generators,
   Binding generator, and `ProjectionAdapter` from [Program.cs](Program.cs).
3. The adapter privately adds a `public string? Name` declaration to a copy of
   that compilation. It checks the one known field's semantic type and attribute.
4. A **prototype-only private inner driver** runs the unchanged packaged
   Validation generator against the private copy. Only its generated interceptor
   sources and diagnostics are forwarded to the outer driver. Neither the
   projected tree nor the private output compilation is forwarded.
5. The outer output contains the real ReactiveUI setter and Binding dispatch.
   The final compiler checks the actual generated property's public nullable
   contract and both original calls' `GetInterceptorMethod` results. The original
   syntax tree retains identity, path, text and encoded-location content.
6. The packaged final Validation dispatch analyzer runs on that final output.
   The compiler emits a DLL and the host executes its assertions.

This is not ordinary generator order chaining. The adapter's explicit private
inner driver is investigation plumbing. The same proof passes with the adapter
first and last in the outer driver. The unprojected control, using only the
ordinary real generators, produces `RUVG001` and final `RUVG005` even though its
final source now has a real `Name` property and otherwise compiles successfully.

## Results and evidence

Each of four managed runs passes:

```text
initial-invalid -> Ada-valid -> null-invalid -> disposed;
Binding=[null,Ada,null,Detached]
```

Rule disposal removes its registration. Subsequent property updates remain
observable through Binding. Each final compilation has four generated sources:
the real reactive property, Binding's attributes and observation dispatch, and
Validation's interceptor. No emitted tree contains the projection marker.

- [Primitives results](evidence/Primitives/result.json)
- [Reactive results](evidence/Reactive/result.json)
- [Primitives ordinary-driver control](evidence/Primitives/unprojected-control-diagnostics.txt)
- [Reactive ordinary-driver control](evidence/Reactive/unprojected-control-diagnostics.txt)
- [Primitives strict host build](evidence-build-primitives.log)
- [Reactive strict host build](evidence-build-reactive.log)
- [Primitives runtime trace](evidence-run-primitives.log)
- [Reactive runtime trace](evidence-run-reactive.log)

Each flavor/order directory retains its generated sources, emitted `Consumer.dll`,
empty compiler and final-dispatch diagnostic files, and a clearly labeled
`PRIVATE-PROJECTION-NOT-EMITTED.txt` copy for inspection. This text evidence is
outside the emitted compilation.

The private compiler matches the locked SDK's observed `NoWarn=1701;1702`
defaults. A raw Roslyn compiler without those SDK defaults reported only
System.Reactive's net8-to-net10 framework identity warnings; the initial messages
are retained in
[initial-raw-framework-identity-warnings.txt](evidence/Reactive/initial-raw-framework-identity-warnings.txt).
All remaining compiler, generator, and final dispatch warnings/errors are checked.
No trim or AOT diagnostic is suppressed. Both strict host builds have zero
warnings/errors.

## Reproduce

From this directory, supply the approved locked SDK project, the retained
candidate package feed, the released DynamicData `.5` feed, and an existing cache:

```sh
proof_sdk=/path/to/runic-sdk
direnv exec "$proof_sdk" python3 run.py \
  --sdk "$proof_sdk" \
  --package-feed /path/to/retained/candidate-packages \
  --dependencies-feed /path/to/released/dynamicdata-packages \
  --cache /path/to/existing/nuget-cache
```

The runner checks the exact historical `.10` Validation and `.5` DynamicData
package hashes before restore. It creates a proof-owned NuGet configuration with
explicit source mappings and passes that configuration and cache to MSBuild;
production feeds and the current `.30` dependency pins do not determine this
experiment. The checked-in project retains the original versions.

By default, all new reports, generated sources, consumer DLLs and logs go into a
fresh ignored `reproductions/<UTC timestamp>-<id>/` directory. `--output` selects
another fresh, nonexistent directory outside this proof tree or beneath its
excluded `reproductions/` directory. Existing outputs, historical evidence and
other directories inside the proof tree are rejected. `--binding-source` optionally verifies the pinned inspected
checkout and file hashes; otherwise the historical source inspection is labeled
as historical and no local Binding checkout is required. Every reproduction
manifest records the new proof source hashes separately from the original
manifest hash and snapshot.

[portability-replay.json](portability-replay.json) records the four successful
managed checks of the parameterized runner after merge base `8698467`, using the
same historical `.10`/`.5` package bytes. It confirms the original fixture text,
evidence and logs are unchanged. A subsequent runner-only output guard has three
separate rejection checks and its own hash; the record distinguishes those
checks from the measured compiler source snapshot. This replay does not repeat
or replace the original NativeAOT measurements.

Runs are sequential. `bin/` and `obj/` are reusable task-owned compiler-host
outputs. Generated reproduction directories are disposable after review;
`evidence/` and the four original logs remain historical verification artifacts.

## Limits

The adapter recognizes one specific public nongeneric partial model, one
`[Reactive] string? _name` field, and its default public `Name` property mapping.
It does not implement Binding's general extraction rules, commands, collection or
derived-list generators, generated `IReactiveObject`, XAML, access modifiers,
generic/nested models, or a version matrix. Declaration equivalence beyond this
one contract remains unproven. NativeAOT publishing was not performed.

This result establishes technical feasibility for this narrow pinned contract.
It does not add field-generated member support to production Validation or alter
the existing declared-partial-property/explicit-observable guidance.
