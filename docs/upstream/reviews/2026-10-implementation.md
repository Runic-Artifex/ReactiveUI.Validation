# Implementation review: 2026-10

Started **2026-10-05**. Outcome: **implementation in progress; integration and
final verification pending**. This record is separate from the
[historical no-change review](2026-10.md). The
[implementation ledger](../implementation-2026-10.md) records full scope,
contracts, decisions and deferrals.

## Inputs and integration

| Field | Value |
| --- | --- |
| Runic topic base SHA | `48a7bde8e4fdbb34f85d700597c87d60c24aff69` |
| Previously integrated upstream SHA | `cde3062937752abeb4216a92c15f5b3b37be9a34` |
| New upstream merge | None proposed by this implementation; integrated upstream point is unchanged. |
| Exact adopting topic commits | Pending integration-owner record. |
| Exact tested integrated SHA | Pending. |
| Integration SHA / maintained branch | Pending. |

This work implements the authorized worthwhile assessment topics as local Runic
adaptations. It does not advance upstream ancestry merely by adding APIs or
adopting a catalog disposition.

## Accepted and deferred work

Accepted work comprises binding lifetime/disposal, explicit contexts, typed
state bindings, compiled collection examples, command/scheduler regressions,
opt-in benchmarks and package graph hardening. All adoption remains pending.
The ledger records existing implementations and explicit deferrals for #356,
#474, #41, #66, #833/.NET 11/platforms, source generators, Native AOT and routine
historical dependency/repository changes. #378's selector account is corrected
consistently in the issue table and JSON with the confirming upstream comments.

## Difference register and conflicts

Existing **RUV-001 through RUV-009** remain protected. New stable IDs and amended
verification contracts are recorded in the [difference register](../../fork-differences.md)
only after topic adoption. Conflict decisions, final source references and any
rerere resolution reviews are pending integration-owner evidence. The historical
binding diagnostics remain unchanged and gain a live status link.

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
| Release warnings-as-errors build / both API baselines | Final integrated SHA pending | Pending. |
| Focused regressions / both flavors | Adopting topic SHAs pending | Pending. |
| Full shipping tests / both flavors | Final integrated SHA pending | Pending. |
| Compiled examples / benchmark smoke | Final integrated SHA pending | Pending. |
| Pack / independent consumers / negative graph checks | Final integrated SHA pending | Pending. |
| Linux / Windows core gates | Final integrated SHA or CI run links pending | Pending. |
| Preserved upstream ancestry | Final integrated SHA pending | Pending. |

The prior baseline's 402 passing tests and earlier CI run are historical
evidence. They do not verify later implementation. Platform workloads, Native
AOT, source-generator behavior and input-to-paint performance are not established
by core or benchmark compilation.

## Release

No new release is recorded. Published versions, tags and assets are unchanged.
Final integration/release status must be recorded by the integration owner.
