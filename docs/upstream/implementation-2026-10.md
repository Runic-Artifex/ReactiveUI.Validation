# Validation implementation ledger: 2026-10

Started **2026-10-05** from Runic base `48a7bde8e4fdbb34f85d700597c87d60c24aff69`.
This is the live decision and adoption record for the worthwhile work identified
by the [dated assessment](README.md). The [implementation review](reviews/2026-10-implementation.md)
records integrated inputs and verification. The [original October review](reviews/2026-10.md)
and [diagnostic evidence](evidence/README.md) remain historical records.

The initial implementation is **implemented, integrated and released; tag and both assets verified** at source
`c9fa501c4d2e9442d85693dae77bb6f727e9eb7a`. Shipping code is pinned at
`5d433128e0692af112f43a7e09409a43b7210edc`; workflow follow-up `667978c` adds
negative-graph checks and ordinary collection example smoke. The integration
owner supplied the logical adopting commits below. All shipping topics are
implemented in the released source. The local build, 594 tests and both ordinary
examples passed. Both expanded packaged consumers and their full graphs also
passed at `c9fa501`. Cross-platform CI passed in attempt 2 at the same source;
release identity and assets are verified.
Historical test counts are kept separate from current verification. The [difference register](../fork-differences.md) remains
the source of stable fork contracts.

## Worthwhile implementation topics

| Topic / sources | Decision and contract | Status / adoption | Required evidence |
| --- | --- | --- | --- |
| Binding lifetime: #23, #152, #659/#660, #665/#879, reproduced diagnostics | Switch to the current ViewModel/helper/context; detach replaced and null sources; make binding disposal idempotent. Preserve property, aggregate, helper and callback behavior in both flavors. | Adopted: `925e344e0ed1d8b26ff8f128167a2fa476b35067`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Replacement/null/helper cases, stale-source detachment, double disposal and post-disposal updates; both flavor suites. |
| Multiple contexts: #511; existing #35/#474 abstraction | Add explicit-context rule registration and context-selected binding while retaining the default context APIs. The selected registration context owns helper removal. Advice stays independent of blocking validity. | Adopted: API `e116e8bf808ebbf37a7b4a5177729bf3d89b8193`, default ownership `64542c88e03951f0158de0e36874549a0be1b80f`; combined at `8dc90961e3b6e5dfd326004749e6b81447c5d093`. Local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Blocking/advisory independence, clearing/disposal, context replacement/null, both API baselines and both flavors. |
| Typed state binding: #463 | Add distinct `BindValidationState` projections for typed targets and actions. Define empty and multi-rule state explicitly; preserve text formatter overloads. | Adopted: `acf8d7178b639d68ea3b5eaf474fdf5d521d4321`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Bool/enum projection, mixed rules, empty state, replacement/null/disposal and both API baselines. |
| Collection recipes: #173, #450, #433 | Compile fully imported examples for both flavors with an explicit initial empty snapshot, stable identity, child refresh and same-key child replacement. Separate domain validity from touched/submitted presentation. | Adopted: `004a17c84c264f50ece8f65400fe5373668bfa4f`; local and both OS example/suite gates passed; released in `8.1.0-runic.0.790.17`. | Empty/add/edit/remove/clear/replacement scenarios, current validity and property/entity errors. |
| Command and notification scheduling: #19, #31, #34, #70, #92/#95/#97, #515/#879 | Document and test serialized model mutation, validation and command admission using an explicit flavor scheduler. Preserve the distinction between synchronous current validity and deferred notifications. | Adopted: correction `0a94cea64958de8221059dbcef03d791d05544fc`, ordering example/regressions `004a17c84c264f50ece8f65400fe5373668bfa4f`; local and both OS gates passed; released in `8.1.0-runic.0.790.17`. | Same-turn invalidation, queued work, cross-field/membership changes, asynchronous completion and notification-order regressions. |
| Benchmarks: #117; inherited #878 baseline | Add an opt-in .NET 10 BenchmarkDotNet solution for both flavors outside shipping solution/release gates. Measure representative construction, change, collection and replacement/disposal workloads before optimizing. | Adopted: initial `a23693e7990c9ba2539237db8ca5533ca8d54102`, binding workloads `587c1b0bf11c1b02b6adb0df34d85e7b8eeecd48`; CLI/source-pin fix `8fd9847716916d9436ebfab908ec74b9bb922a89`, report topic `40b3a45`. Selected final measurements/CLI checks passed; no full timing matrix. | Both projects compile; bounded smoke execution and workload coverage. Measurements are scenario evidence, not input-to-paint or blanket performance claims. |
| Dependency and package boundaries: #500/#679/#696/#829/#874/#967/#979, #691/#933/#990 | Retain the released matching DynamicData pair and formatter fallback. Strengthen independent consumer checks for both upstream DynamicData IDs, opposite flavor, exact resolved versions and Primitives' System.Reactive exclusion. | Adopted: `df7b48de7a2191171c7243484a785a36850e749c`, generated-consumer correction `bd58ee3e052670a4e320388d61d7b368735a7c04`; actual .804 pair passed both integrated consumers/full graphs at `c9fa501`; both OS CI gates passed. | Package verification plus deliberate negative graphs; independent packed consumers on the final integrated SHA. |
| Catalog correction: #378 | The reported selector calls `view.FindControl<TextBlock>("UsernameError").Text`; move control lookup behind a property and bind `view.UsernameError.Text`. The reporter confirmed this fix. | Adopted documentation: `ac6e6ed`; included in candidate `e9ab48a`. | [Suggested property-only selector](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-957330539) and [reporter confirmation](https://github.com/reactiveui/ReactiveUI.Validation/issues/378#issuecomment-960270058); issue table and JSON agree. |

## Implemented behavior and scheduling correction

The released source includes permanent [binding lifetime regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationBindingLifetimeTests.cs),
[typed state regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationStateBindingTests.cs)
and [context regressions](../../src/tests/ReactiveUI.Validation.Tests/MultipleValidationContextTests.cs).
These cover model/helper/context replacement, null selection, cleanup ownership
and idempotent disposal in shared source for both flavors. The
[context example](../examples/multiple-contexts.md) and
[typed binding example](../examples/typed-state-bindings.md) describe the adopted APIs.
Typed helper/property converters preserve custom class and struct rule metadata;
ordinary aggregate context states retain their existing aggregation contract.

Legacy `BindValidation` property callbacks retain an empty-state-list prelude
on selection. A consumer using `All(IsValid)` may interpret that empty list as
valid. New typed and selected-context property bindings seed actual membership,
do not invent valid states for existing invalid rules, and wait for each active
rule to supply an initial state. Both surfaces are presentation observations;
command admission requires its own current-validity/pending policy.

The explicit SDK model-turn probe reproduced a separate scheduling bug: adding
an invalid rule left `GetIsValid()`, `HasErrors` and command admission stale
until queued membership publication. Correction `0a94cea` now updates raw
context membership, `Valid` and `ValidationStatusChange` synchronously on
the owning model context; `IsValid` and `Text` OAPH presentation properties
remain scheduled. **Public `Validations` observable delivery changes to the
model owner**. Native/UI consumers must dispatch at the presentation boundary
when they need another scheduler. This does not change global RxApp defaults or
promise arbitrary concurrent mutation. Preserve the
[scheduling regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationContextSchedulingTests.cs)
and [ordering regressions](../../src/tests/ReactiveUI.Validation.Tests/ValidationOrderingTests.cs).

The [collection recipe](../examples/collection-validation.md) compiles shared
source for both flavors and covers initial empty/add/edit/remove/clear, same-key
child replacement, old-child detachment and disposal. Its stable cache itself is
not replaced; entire-source replacement requires a deliberate inner switch and
ownership policy. Touched/submitted flags affect presentation, not validity.
The [ordering recipe](../examples/validation-ordering.md) uses actual SDK model
context/adapter source pinned at `e878a4f361a7c4b9326f54663defa7debe41adb9`
through opt-in `RunicSdkRoot` projects. This is source-level SDK scheduling
coverage, **not bridge/browser end-to-end verification**. CI runs Python negative
graph checks and both ordinary collection examples; it does not run these
SDK-source probes.

The API parity review at shipping pin `5d433128e0692af112f43a7e09409a43b7210edc`
confirmed preservation of all original public API lines, with **14 new methods**
in each flavor and matching namespace/annotation contracts. All runtime gates passed; release identity and assets are verified.

The [benchmark report](../../benchmarks/results/2026-10-05.md) distinguishes the
initial warmed baseline at `48a7bde` from the later binding workload smoke at
`73bedbb`. Those measurements are not relabeled as final-candidate timings. The separate
[final report](../../benchmarks/results/2026-10-05-final.md) records committed
workload source `8fd9847716916d9436ebfab908ec74b9bb922a89`, whose runtime tree
matches shipping `5d433128e0692af112f43a7e09409a43b7210edc`: six selected
ShortRun measurements at ten rules, 18 CLI checks and a clean Release
warnings-as-errors benchmark build. These shared-host observations establish
no speed ranking, historical improvement or UI-latency claim.

## Coverage and retained behavior

The catalog contains all **53 issues and 936 pull requests** captured on
2026-10-05. Its categories include **43 Runtime and API PRs**, **9 Testing and
analysis PRs**, and **803 dependency-maintenance PRs**. The integration owner confirms that the scope review covers all 53 issues,
the relevant runtime requests and all 12 nondependency closed-unmerged PRs.
The 43 runtime/API category count is the current refined classification; an
earlier count of 47 is not a separate implementation set. No record disappears
because its proposal was closed, superseded or deferred.

Existing custom-state, immutable-text, null-text, full-path matching,
INotifyDataErrorInfo, multi-rule, rule membership, formatter resolver and
observation-ownership behavior is retained. Inherited implementations require
regression preservation, rather than a second historical patch import. The
53 issue dispositions and 936 PR records remain discoverable through the
[issue catalog](issues.md), [PR catalog](pull-requests.md) and [JSON catalog](catalog.json).

## Explicit deferrals and superseded proposals

NativeAOT/generator follow-up dated **2026-10-05** is recorded in the
[separate investigation](../aot-and-generators.md). Its dated released-package executions and generated consumer output retain
the historical warnings. The subsequent released runtime/examples-first follow-up below
adds explicit streams and narrows audited annotations; it does not relabel the
immutable release or establish broader platform/cohort support.

| Source | Decision / reason | Reconsider when |
| --- | --- | --- |
| #356 virtual `RaiseErrorsChanged` hook | Deferred: no concrete Runic SDK adapter requires overriding event delivery. Existing `ErrorsChanged` subscription covers the stated bridge use. | A concrete adapter needs the hook and defines base-call/ownership semantics. |
| #474 / #35 context abstraction | Useful interface already exists; do not import old namespace moves or obsolete tests. | A concrete API requirement is missing from current `IValidationContext`. |
| #41 helper `BindTo` patch | Superseded by current read-only OAPH/helper contracts; modern binding lifetime receives its own fix. | A new reproduced helper/activation issue requires an implementation change. |
| #66 / #67 legacy scheduler default | Do not restore platform-conditioned task-pool defaults; use explicit serialized model scheduling. | A supported consumer demonstrates a scheduler contract that needs a scoped adapter. |
| #833, #992 / .NET 11, #4/#9/#135/#414 and other native platform proposals | AndroidX, desktop/mobile samples, obsolete UWP/Xamarin and .NET 11 remain outside current .NET 10 core CI/release support. | Explicit platform support decision, workload/cohort and consumer/API/release gates. |
| #990 binding source-generator caveat | Anonymous/private selector issue is unreproduced against the resolved 9.1 generator generation; no speculative workaround. | Exact selectors reproduce the generator diagnostic/output issue. |
| Broader NativeAOT support | The immutable `.790.17` release retains historical annotations. The `.15` native follow-up supports the verified explicit-stream console paths on Linux/Windows x64; whole-library and untested-platform support remain deferred. That released pass deferred a Validation generator; the later unreleased split is recorded below. | A concrete additional path/cohort/host passes accurate producer analysis and strict actual-package publish/run gates. |
| Remaining abandoned repository/platform proposals and routine updates | Preserve dated disposition; no historical badge, coverage uploader, version bump, analyzer cleanup, platform downgrade or dependency-update queue is replayed. | Current supported scenario or intentional tested dependency cohort requires it. |

## Dependency decision

The implementation retains SDK **10.0.401**, **.NET 10**, ReactiveUI and
ReactiveUI.Reactive **26.0.1**, and the released Runic.DynamicData pair
**10.0.0-runic.5**. Release source is
`edd2d175794afc86e964a06cb55329f44793009f`; asset hashes remain in the
[historical cohort record](reviews/2026-10.md#dependency-cohort-and-existing-verification)
and [bootstrap](../../eng/restore-fork-dependencies.py). Ordinary new validation
features do not require a DynamicData cohort change or sibling checkout.

Unreleased sibling DynamicData improvements are future-cohort candidates.
DynamicData list `Switch` changesets are distinct from ReactiveUI `SwitchTo`
used in validation binding lifetimes. The finite/erroring list
`QueryWhenChanged` change `bd22ab8` is not a dependency of these ordinary
features; reconsider it through a released cohort if terminal parity becomes a
supported promise. Do not substitute unreleased sibling source for packages.
Publication-time read-only watch found the latest released pair still at `.5`;
the sibling checkout at `dab721ed0f54025d1cd6e65b77847f2220a5281c` on
`integration/upstream-2026-10` remains an unreleased future cohort. This release
consumes none of those later sibling changes; no SDK/DynamicData source was edited
as part of Validation implementation.

## Verification and release status

Candidate/logical adopting SHAs and API parity review are recorded above. The
local gate at `d6d634e43d109b848a06b960a29704aede2b100f` passed the Release
warnings-as-errors build, **594 tests (297 per flavor; zero failed/skipped)**,
eight Python negative graph checks and both ordinary collection examples.
Expanded consumers initially hit CA1050 in generated global helper types.
Verifier correction `bd58ee3e052670a4e320388d61d7b368735a7c04` compiled the
helpers inside a named namespace; its owner and integration runner verified both expanded consumers and full
exact-version/flavor graphs with the actual local `8.1.0-runic.0.804` package
pair at clean `c9fa501`. Eight negative graph tests reran successfully. Retained
local evidence and both package hashes appear in the
[implementation review](reviews/2026-10-implementation.md#verification).
[Linux/Windows CI run 37363825486](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37363825486)
passed at exact `c9fa501` in attempt 2. Windows attempt 1 and the Linux retry
passed all gates: **594 tests per OS (297 per flavor), zero failed/skipped**;
eight negative checks each, clean build, examples, packing and consumers. Windows produced
verification version
`8.1.0-runic.0.790.17`, as did Linux. It differs from local `.804` because CI had the published
version tag metadata. The review records each identity/hash separately; no byte
equality is claimed. Attempt 1 failed overall when Ubuntu never acquired a
hosted runner and executed no work. Only Linux was rerun at the same source;
attempt 2 passed, carrying the unchanged Windows success. This recovered
infrastructure acquisition failure did not indicate a shipping-code test
failure. Runic main is integrated; release identity and assets are verified.

The reviewed SDK source input remains `e878a4f361a7c4b9326f54663defa7debe41adb9`
for earlier focused probe evidence. During the local gate its checkout moved to
clean `e6de9ca3c6073aae33dc5fadbe6ebd5ec3de58df`; the unpinned Primitives
probe pass is excluded from final evidence and Reactive was intentionally not
run. There is no final pinned-SDK or bridge/browser E2E result.

The historical **402 passing tests** at the earlier unchanged baseline are not
verification of this implementation. Benchmarks, retained platforms, source
generators and Native AOT have their separately stated limits.

Runic main fast-forwarded to `c9fa501`, preserving upstream/topic histories.
[Release workflow 37365832750](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37365832750)
passed at that exact source and published
[`8.1.0-runic.0.790.17`](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17) under tag `runic-v8.1.0-runic.0.790.17` on **2026-10-05 at 19:50:16 UTC**.
The lightweight tag targets exact `c9fa501`; both published asset SHA-256 values
match downloaded bytes. Both nuspecs declare the correct version, .NET 10,
ReactiveUI 26.0.1, matching released DynamicData `.5` and repository commit `c9fa501`.
The [implementation review](reviews/2026-10-implementation.md#release) records
release hashes separately from local/CI verification packages. The release
workflow also passed 594 tests, eight guard checks, clean build, both examples
and both expanded consumers. The prior `.790` tag still targets `509dd45`.
The subsequent documentation-only update reuses unchanged code/CI evidence;
its own commit is not the released shipping source. Published tags and assets
remain immutable. No upstream message or contribution is part of this work.


## Released native runtime and examples first implementation

The follow-up starts at `e653e52eb6abba95d3a5787771df01c4e570e309` on
**2026-10-05**, after the immutable release recorded above. The released [8.1.0-runic.0.790.17.15](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17.15) uses source
`f22d2bb42c30d66df19a59333ed4fa633b241940`. It adds explicit observable rule
registration and outer-selection typed callbacks, enables producer AOT/trim
analysis and uses realistic released-package strict failures with safe runtime
counterparts. The [runtime recipe](../examples/native-validation.md),
[NativeAOT status](../aot-and-generators.md) and
[follow-up review](reviews/2026-10-implementation.md#native-runtime-and-examples-follow-up)
define the contract and separate preliminary, final, integrated and published
identities.

Audit `4cdf89ab1d2de08dc3b85c48db2aee7418aa85c1` removes false RDC requirements,
narrows blanket RUC on generated OAPH and supplied-observable paths, and preserves
RUC on legacy reflection. Both producers pass warnings-as-errors analysis and
48 focused tests per flavor. API `67fdd937cc1aef85bb6b42ca9cd3a31d0e06f195`
adds six methods and 21 baseline lines per flavor, with 12 new runtime regressions
per flavor passing. Registration rollback, original exception preservation and
reentrant initial membership/source selection have focused coverage.

Preliminary `eae4984` passed 618 core tests (309 per flavor), 14 Python guards
(six native plus eight existing package checks), zero build warnings/errors and
30 strict managed/full-trim/Linux-native scenario executions. Its four-job CI
passed both core OS jobs and actual Linux/Windows native consumers. It remains
historical after the application-adapter correction; those bytes do not verify
the final fresh package pair. Library and library-test trees are byte-identical
at final `f22d2bb`, so the local core result is explicitly reused.

The final application adapters install pending subscription ownership before
initial delivery and clean up attached handlers on initial getter/callback
failure. Their bounded handoff/null/disposal/snapshot assertions execute within
`generic-field`. Fresh final actual-package verification passes all five cases
in both flavors across managed/full-trim/actual Linux-native modes: **30 scenario
executions**, zero positive warnings/errors. The release workflow targets this
fork explicitly. Final four-job CI 37375282317 passes at the same source: 618 core tests per OS
and 20 actual native case checks across Linux x64/Windows x64, both flavors,
zero candidate warnings/errors. Native jobs use the same exact Ubuntu artifact;
standalone full-trim managed execution remains local Linux evidence. Release run 37375833531 repeats all four verification jobs, then publishes the
exact Ubuntu artifact pair validated by both native RID reports. The new tag,
nuspec source and independently downloaded assets are verified; the [structured evidence](evidence/native-runtime-implementation.json)
records exact local versions/hashes and the separately retained preliminary
package identities.

No Validation generator package or MVP shipped in this released pass. Its
then-future design mapped realistic cases to direct runtime operations and
retained a managed synthetic interceptor proof. Generated-adapter acceptance was
deferred at that release; the [current contract](../generated-validation-design.md)
and unreleased follow-up below supersede that decision.
No dependency SDK/DynamicData update, blanket `IsAotCompatible`, retained UI
platform or bridge/browser end-to-end support follows from this implementation.


The final published Primitives SHA-256 is
`17be4c3b5f46e9ff237b5f89bf97c4ff1f47b5dfef9abd250516cc454c57e95f`; Reactive is
`08d6087dbd86e0ae92f1230275351240d8b2e32b7765e5b6c2b31e8ae47b4fd2`.
These differ from local and CI verification package hashes. The `.790` and
`.790.17` tags/assets remain unchanged. A later documentation-only review stamp
reuses unchanged released code/workflow evidence; its own commit is not the
shipping package source. Version/tag/asset immutability is fork policy, not a
GitHub-enforced immutable-release claim.

## Unreleased generated and Unsafe API split

Decision **2026-10-06**, following immutable runtime release source
`f22d2bb42c30d66df19a59333ed4fa633b241940`: implement the primary supported
inline-lambda API with a Validation interceptor generator, replacing the earlier
partial-method/attribute deferral. This working-branch change is **UNRELEASED**
until current-source integration CI passes. Historical release/probe evidence
above remains pinned to its original source and assets.

Normal predicate `ValidationRule`, `BindValidation`, `BindValidationContext` and
`BindValidationState` calls require generation; their ungenerated bodies throw.
Matching explicit Unsafe methods retain runtime reflection and trimming warnings.
Generated normal property text bindings expose actual initial rule states without
the legacy synthetic empty prelude, which Unsafe property callbacks retain.
Previously compiled normal callers must be rebuilt with the matching core
package analyzer/configuration assets, or explicitly migrate to Unsafe. Both
core packages embed the analyzer DLL and their matching allowlist props. Safe
observable/metadata-only overloads remain supported without new interception.

The [design](../generated-validation-design.md),
[migration/corpus recipe](../examples/generated-validation.md),
[maintenance boundary](../maintenance.md#generated-and-unsafe-api-boundary) and
[RUV-018](../fork-differences.md#unreleased-generated-api-contract) record literal
selector constraints, diagnostics, null/default policy, contexts, strict paths,
complete custom states and ownership/disposal. Current-source generated package
consumer results must be recorded independently of the existing runtime gates;
focused current-code verification reports 29 passing compiler-fixture tests
using matched Roslyn 5.9 against both runtime flavors, covering all four rule and
14 binding overloads, with zero build/emitted warnings or errors. Generated
runtime infrastructure passes 14 tests per flavor with strict producer analysis
and zero warnings. Twenty Python guard checks passed across focused runs (eight
package, six runtime-native and six generated). These are working-tree results;
full core, final clean-source package/native gates and Linux/Windows integration
CI remain pending.

The dependency cohort remains SDK 10.0.401/.NET 10, ReactiveUI 26.0.1 and the
matching released DynamicData 10.0.0-runic.5 pair. No sibling adoption or blanket
package `IsAotCompatible`, all-reflection-removed, UI-platform or bridge/browser
support follows.

The generator/tooling cohort is aligned to the locked SDK's actual Roslyn 5.9.0
compiler and `analyzers/dotnet/roslyn5.9/cs` package path. A reproduced
nullable-generic CS8714 in C#14's synthesized static bridge requires the six safe
observable methods to use traditional `this` extensions. CLR signatures and
inferred calls remain compatible; explicit observable binding source calls now
use `<TSource, TOut>`, while `AddObservableRule<TValue>` keeps its arity. The
narrow SST1703 exception addresses that measured compiler error, without IL
suppression or tuple-based signatures. The unchanged direct-static nullable
callers reproduce eight CS8714 errors
before the correction and zero warnings/errors after it; inferred receiver calls
passed before it. Retained proof/source/log inputs are under
`artifacts/verification/generator-nullability`, including `results.json`.
Compiler/runtime focused checks pass as recorded above; final clean-source gates
remain pending. Historical proof/release source and package inputs are unchanged.
