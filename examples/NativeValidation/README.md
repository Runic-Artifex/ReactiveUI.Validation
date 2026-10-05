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

From the repository root, inside the locked SDK shell:

```sh
python3 investigations/NativeAot/restore-validation-feed.py examples/NativeValidation/artifacts/baseline-feed
python3 eng/restore-fork-dependencies.py
python3 examples/NativeValidation/run.py baseline-managed examples/NativeValidation/artifacts/baseline-feed artifacts/dependencies
python3 examples/NativeValidation/run.py baseline-strict examples/NativeValidation/artifacts/baseline-feed artifacts/dependencies
```

The runner validates immutable baseline hashes, exact package identities and
flavor boundaries, complete case output, and diagnostic codes with Validation
method provenance. It rejects unrelated compile failures. Logs remain under
`artifacts/`; generated binaries can be removed after verification.
