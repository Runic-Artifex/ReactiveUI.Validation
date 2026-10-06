# Real application validation under trimming and NativeAOT

These package-only consumers are opt-in and outside the shipping solution. Shared
application models use real property notifications. The `ExpectedFailure` pair
pins released Validation `8.1.0-runic.0.790.17` (source `c9fa501`) and matching
released DynamicData `10.0.0-runic.5`. No sibling project reference is used.

| Case | Application contract | Released baseline |
| --- | --- | --- |
| generic-field | Reusable generic field receives a selector held in a variable; property errors and disposal | Managed passes; strict IL2026/IL3050 on `ValidationRule` |
| nullable-editor-rich-target | Replace nested nullable address, helper and editor model; project to nullable enum-bearing struct; detach stale updates | Managed exposes retained valid state when address becomes null; strict IL2026/IL3050 on rules and typed binding |
| blocking-advisory-cross-field | Two notifying fields, per-property error metadata, separate advisory context, captured ownership | Managed passes; strict IL2026/IL3050 on property and explicit-context rules |
| rows-async-uniqueness | Dynamic stable row IDs, pending/duplicate/completed server-result states, rich struct metadata and late result after removal | Managed passes; strict IL2026/IL3050 even on the released observable overload |

The row source is a deterministic server-result delivery boundary, not a network
service or timing test. Invalid pending state is supplied before registration.
The baseline editor target setter is referenced only by binding reflection. It
has no static writes or metadata roots. Actual native publication/execution is
owned by the serialized native gate; strict compile failures must not be reported
as native runtime failures.

The `Safe` pair defines `SAFE_API`, uses the current released DynamicData
10.0.0-runic.30 pair, and accepts a fresh candidate package version.
Its implementation uses `AddObservableRule`, `BindObservableValidationState` and
`BindObservablePropertyValidationState`. Application INPC streams provide initial
values and explicit replacements. Nested null emits an actual null value, which
this application interprets as missing required data. Static callback assignment
keeps the nullable typed target setter reachable. A rich invalid state with empty
text still projects to `Blocking`, retaining its revision.

The safe cross-field counterpart intentionally combines the baseline's two
single-property rules into one state with two full property paths. Both error
exports still receive `emails-differ`; strict property selection excludes that
multi-property rule. Advisory context ownership remains independent. This is an
explicit metadata adaptation, not a claim that two registrations equal one rule.

A fifth safe smoke, `existing-observable-foundation`, exercises audited existing
constructors, generated OAPH presentation updates, the supplied-observable
`ValidationRule` overload whose literal selector is only metadata, per-property
errors, and current context events/removal. No validation generator is used.

The safe `generic-field` case also checks the application adapters' synchronous
initial delivery: reentrant parent replacement/null, ownership disposed before
`Subscribe` returns, and initial getter/callback failures. Pending ownership slots
reject obsolete tokens and suppress detached notifications; failed subscription
setup removes its registrations. These checks assume one serialized model owner.
The compact managed before/after reproduction and source hashes are retained in
`evidence/adapter-lifecycle-results.json`; they do not establish concurrent or
transport cancellation behavior.

From the repository root, inside the locked SDK shell:

```sh
python3 investigations/NativeAot/restore-validation-feed.py examples/NativeValidation/artifacts/baseline-feed
python3 eng/restore-fork-dependencies.py
python3 eng/restore-fork-dependencies.py --legacy
python3 examples/NativeValidation/run.py baseline-managed examples/NativeValidation/artifacts/baseline-feed artifacts/dependencies
python3 examples/NativeValidation/run.py baseline-strict examples/NativeValidation/artifacts/baseline-feed artifacts/dependencies
python3 examples/NativeValidation/run.py candidate-managed /absolute/candidate/feed artifacts/dependencies --version FRESH_VERSION
python3 examples/NativeValidation/run.py candidate-strict /absolute/candidate/feed artifacts/dependencies --version FRESH_VERSION
```

The runner validates immutable baseline hashes, exact package identities and
flavor boundaries, complete case output, and diagnostic codes with Validation
method provenance. It rejects unrelated compile failures. Logs remain under
`artifacts/`; generated binaries can be removed after verification.

`manifest.json` is the runner's assertion contract. The safe fixtures are verified
at released source `f22d2bb42c30d66df19a59333ed4fa633b241940`, version
[`8.1.0-runic.0.790.17.15`](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17.15).
Local Linux execution passes all five cases in both flavors across managed,
standalone full-trim and NativeAOT modes. Final CI passes actual Linux x64 and
Windows x64 NativeAOT using its own Ubuntu package pair; the release matrix
independently verifies both native RIDs against the exact pair it publishes.
Both runs have zero positive warnings/errors. The handoff/initial-failure adapter
checks execute within `generic-field`. The [implementation review](../../docs/upstream/reviews/2026-10-implementation.md#native-runtime-and-examples-follow-up)
records distinct local/CI/published hashes and the strict baseline failures.
The baseline report in `evidence/baseline-results.json` records actual focused
managed and strict-build evidence, including source hashes. The focused `.14`
package report and adapter `.12` before/after report remain
historical managed evidence; the final `.15` native results belong to the gate's
separate exact-source report.
