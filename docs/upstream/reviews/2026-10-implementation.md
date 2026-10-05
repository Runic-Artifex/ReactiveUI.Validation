# Implementation review: 2026-10

Started **2026-10-05**. Outcome: **all shipping topics implemented in the candidate; final local/CI
verification and release metadata pending**. This record is separate from the
[historical no-change review](2026-10.md). The
[implementation ledger](../implementation-2026-10.md) records full scope,
contracts, decisions and deferrals.

## Inputs and integration

| Field | Value |
| --- | --- |
| Runic topic base SHA | `48a7bde8e4fdbb34f85d700597c87d60c24aff69` |
| Previously integrated upstream SHA | `cde3062937752abeb4216a92c15f5b3b37be9a34` |
| New upstream merge | None proposed by this implementation; integrated upstream point is unchanged. |
| Exact adopting topic commits | Lifetime `925e344`; typed `acf8d71`; contexts `e116e8b` plus ownership `64542c8`; graph guards `df7b48d`; benchmarks `a23693e` plus binding `587c1b0`; scheduler correction `0a94cea`; collection/ordering examples `004a17c`. Full SHAs in the ledger. |
| Source candidate SHA | `d6d634e43d109b848a06b960a29704aede2b100f`; shipping pin `5d433128e0692af112f43a7e09409a43b7210edc`; workflow example smoke follow-up `667978c`. Final gate is running at `d6d634e`; results pending. |
| Integration SHA / maintained branch | Pending. |

This work implements the authorized worthwhile assessment topics as local Runic
adaptations. It does not advance upstream ancestry merely by adding APIs or
adopting a catalog disposition.

## Accepted and deferred work

Accepted work comprises binding lifetime/disposal, explicit contexts, typed
state bindings, compiled collection examples, command/scheduler regressions,
opt-in benchmarks and package graph hardening. The supplied logical topic SHAs
are adopted into the source candidate, including the new synchronous raw
membership/current-validity correction and compiled collection/ordering recipes.
Final maintained-branch integration and all final gates remain pending.
The ledger records existing implementations and explicit deferrals for #356,
#474, #41, #66, #833/.NET 11/platforms, source generators, Native AOT and routine
historical dependency/repository changes. #378's selector account is corrected
consistently in the issue table and JSON with the confirming upstream comments.

## Difference register and conflicts

Existing **RUV-001 through RUV-009** remain protected. **RUV-007** now records
exact/flavor/upstream-ID guard hardening. New **RUV-010 through RUV-015** record
binding lifetime, explicit contexts, typed projections, opt-in benchmarks,
synchronous raw domain streams and executable recipes
with adopting commits and permanent regression/source links in the
[difference register](../../fork-differences.md). Final gate results remain pending.
Combined context adaptation is recorded at `8dc9096`; final conflict/reused-rerere
review evidence remains with the integration owner. Historical diagnostics
at `509dd45` retain their eight executions/four failing expectations.

## Dependency cohort

| Item | Pin / evidence |
| --- | --- |
| SDK / target | 10.0.401 / .NET 10 |
| ReactiveUI / ReactiveUI.Reactive | 26.0.1 / 26.0.1 |
| Runic.DynamicData / Reactive pair | Released 10.0.0-runic.5 / 10.0.0-runic.5 |
| DynamicData source | `edd2d175794afc86e964a06cb55329f44793009f` |
| Asset integrity | [Recorded bootstrap hashes](2026-10.md#dependency-cohort-and-existing-verification); unchanged. |
| Unreleased sibling improvements | Future cohort only; no sibling project/source substitution. |

## Verification

| Check | Exact source / evidence | Result |
| --- | --- | --- |
| Documentation consistency / local links | Initial documentation topic; 83 local links/anchors, JSON counts 53/936 and consistent #378 correction | Passed; documentation only. |
| Scope reconciliation | Integration-owner confirmation: all 53 issues, relevant runtime requests and all 12 nondependency closed-unmerged PRs | Scope covered; adoption remains pending. |
| Bootstrap / exact dependency graphs | Final integrated SHA pending | Pending. |
| API parity review | Shipping pin `5d433128e0692af112f43a7e09409a43b7210edc` | Approved: all original API lines retained; 14 new methods per flavor with matching contracts. |
| Release warnings-as-errors build / both API baselines | Local final gate at `d6d634e` | Running; no result yet. |
| Focused regressions / both flavors | Permanent lifetime/context/typed classes at the adopting SHAs above | Final combined-source results pending. |
| Full shipping tests / both flavors | Final integrated SHA pending | Pending. |
| Compiled examples / benchmark smoke | Shared collection/ordering source adopted; [benchmark baseline and binding smoke](../../../benchmarks/results/2026-10-05.md) record exact earlier sources | Final candidate example/benchmark results pending. Benchmark CLI/reproduction follow-up is in progress; no broad timing claim. |
| Pack / independent consumers / negative graph checks | Final integrated SHA pending | Pending. |
| Linux / Windows core gates | Final integrated SHA or CI run links pending | Pending. |
| Preserved upstream ancestry | Final integrated SHA pending | Pending. |

The prior baseline's 402 passing tests and earlier CI run are historical
evidence. They do not verify later implementation. Platform workloads, Native
AOT, source-generator behavior and input-to-paint performance are not established
by core or benchmark compilation.

## Scheduling correction and verification boundaries

Correction `0a94cea64958de8221059dbcef03d791d05544fc` addresses the newly
reproduced SDK model-turn stale `GetIsValid()`, `HasErrors` and command
admission after adding an invalid rule. Raw membership, `Valid` and
`ValidationStatusChange` now update synchronously on the owner. Public
`Validations` delivery therefore moves to that owner; native/UI consumers
must dispatch at their presentation boundary. `IsValid` and `Text` OAPH
properties remain scheduled. This is separate from the original passing
property-notification probe and is an intentional observable behavior change.

Shared collection/ordering examples and regressions are adopted at
`004a17c84c264f50ece8f65400fe5373668bfa4f`. Optional SDK scheduler probes
compile actual SDK source at `e878a4f361a7c4b9326f54663defa7debe41adb9`;
they do not verify bridge routing or browser rendering end to end. Workflow
follow-up `667978c` adds Python negative graph checks and both ordinary
collection examples, without running SDK-source probes.

Legacy text-property callback bindings retain an empty prelude; typed and
selected-context property bindings seed actual rule states. Custom class and
struct rule metadata reaches typed converters, and helper replacement has
permanent coverage. See the ledger and adopted examples for exact absence,
initial-emission, ownership and scheduler contracts.

## Release

No new release is recorded. Published versions, tags and assets are unchanged.
Final integration/release status must be recorded by the integration owner.
