# DynamicData cohort adoption: 2026-10-06

This is the local adoption record for the published
[DynamicData v10.0.0-runic.30](https://github.com/Runic-Artifex/DynamicData/releases/tag/v10.0.0-runic.30)
pair. It updates the active Validation dependency cohort; it is neither a
Validation release nor a revision of the dated upstream investigation.

## Inputs and cohort boundary

The topic started from Validation `origin/main`
`4ca1ee98dd04a8998be3da54da2595cc735cf4a3`. DynamicData release source is
`9e3039a597f912c6ddc2628e38bb1162ad9c4563`.

| Package | Version | SHA-256 |
| --- | --- | --- |
| `Runic.DynamicData` | `10.0.0-runic.30` | `cf0d369f43774c2ba1535db8c468c0214d9a82c7f3d1a3d6fa1bf74918f8cbbc` |
| `Runic.DynamicData.Reactive` | `10.0.0-runic.30` | `23cc3f33c029157585a2521cc1c04afbaba21efc6dc6659a855ecafc67217fc5` |

The active pins, verified-download bootstrap, safe native consumers and
generated consumers all use this pair. The bootstrap defaults to it and checks
both hashes before retaining a downloaded asset.

The `8.1.0-runic.0.790.17` Validation package is an immutable native
`ExpectedFailure` baseline. Its historical DynamicData `.5` pair remains
separate: `Runic.DynamicData` is
`ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366` and
`Runic.DynamicData.Reactive` is
`49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268`.
`eng/verify-native-validation.py` restores, verifies and reports that legacy
cohort independently; it must never resolve the released baseline against the
active `.30` pins. The explicit bootstrap `--legacy` mode exists only for that
baseline and dated reproductions.

## Compatibility assessment

Validation exercises DynamicData through collection validation, including
`AutoRefreshOnObservable`/`MergeMany` and `QueryWhenChanged`. In `.30`, child
errors and terminal signals are forwarded and shared delivery is serialized.
This adoption does not add a Validation error-recovery policy or a new
collection operator contract. Existing scheduling, reentrancy, collection
lifecycle, error/export, disposal and generated-consumer checks are retained to
detect an incompatible change in those paths.

## Local evidence

All commands used the locked SDK `10.0.401` through `direnv exec
../runic-sdk`. The runtime/package evidence below was produced from clean source
`18e94882ca15dac5bfec64f6ad450e03401f3d43`; this later documentation record
does not relabel the tested source.

| Gate | Result |
| --- | --- |
| Bootstrap and Python guards | 21 guard tests passed; current and explicit legacy downloads verified their separate hashes. |
| Core build and tests | Release build passed with warnings as errors. Primitives: 323/323 passed; Reactive: 323/323 passed; source generators: 37/37 passed. Total: 683 passed, zero failed or skipped. |
| Packed package consumers | Both flavor consumers passed outside the source project graph. Local packages `8.1.0-runic.0.790.17.15.13`: Primitives `45f89bc9bc976e68c94da7ec043d1a4ebe3d907b7ff9f229b56eac256a60d51e`; Reactive `7447d1038a25fee526355e4ece400215d9074c67a297d34875738d0561d3e3cc`. |
| Ordinary collection recipes | Both Primitives and Reactive executable recipes passed for empty, add, edit, replacement and clear transitions. |
| Strict native package gate | 8/8 passed on Linux x64: six active `.30` safe managed/trimmed/native cases and two immutable `.5` expected-failure baseline checks. |
| Generated package gate | 18/18 passed on Linux x64: both flavors in managed, trimmed and native execution, plus twelve expected generator-diagnostic cases. |

Machine-readable results and command logs remain under
`artifacts/verification/dynamicdata-upgrade-2026-10-06/` for the task worktree,
including `native-gates/results.json` and `generated-gates/results.json`. The
native and generated results each identify source `18e94882`, the SDK, package
hashes and exact restored DynamicData hashes.

Linux local validation does not replace the required hosted Linux/Windows CI at
the final integrated revision. This topic does not publish a new Validation
package or claim broader NativeAOT/platform support.
