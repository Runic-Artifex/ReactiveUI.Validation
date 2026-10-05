# Upstream review: YYYY-MM

Copy this template to `YYYY-MM.md`; use a dated suffix if multiple reviews are needed in one month. Follow [maintenance policy](../../maintenance.md). Replace placeholders and distinguish proposed changes, tested code and completed integration.

## Inputs and decision

| Field | Value |
| --- | --- |
| Review date / integration owner | |
| Runic base SHA (`origin/main`) | |
| Previous integrated upstream SHA | |
| Pinned reviewed upstream SHA / release | |
| Commits examined | |
| Sync/topic branch | |
| Outcome | No change, deferred, tested candidate, or integrated |
| Tested source SHA(s) | |
| Integration/merge SHA | Leave pending until integration actually completes. |
| New integrated upstream SHA | Advance only after an ancestry-preserving integration. |

Explain why the upstream point was selected. For a no-change/deferred review, state the reason and leave the integrated point unchanged.

## Accepted and deferred changes

| Source commit / issue / PR | Decision and reason | Prerequisites / Runic adaptation | Adopting commit |
| --- | --- | --- | --- |
| | | | |

Include selectively ported changes already present, superseded implementations and unsupported platform/dependency changes. Do not infer adoption from issue closure or merge labels.

## Conflicts and difference register

| Files / difference IDs | Conflict or clean-merge risk | Resolution, reused rerere review and verification |
| --- | --- | --- |
| | | |

List added, changed or retired register IDs with source/removal commits and evidence. Verify the selected upstream SHA remains an ancestor of the tested/integrated result. Record the result and any pending integration step.

## Dependency cohort

| Item | Pin / source / evidence |
| --- | --- |
| .NET SDK / targets | |
| ReactiveUI and ReactiveUI.Reactive | |
| Runic.DynamicData and Reactive pair | |
| DynamicData release tag and source SHA | |
| Both asset SHA-256 values / bootstrap | |

## Verification and limitations

| Check | Exact tested SHA / command or run link | Result |
| --- | --- | --- |
| Bootstrap / restored dependency graph | | |
| Warnings-as-errors build / both API baselines | | |
| Focused regressions / both flavors | | |
| Full shipping tests / both flavors | | |
| Pack / independent consumers / graph boundaries | | |
| Linux CI | | |
| Windows CI | | |
| Pinned upstream ancestry | | |

State which checks were not run, reused unchanged-code results for documentation-only work, known failing diagnostics, platform/AOT exclusions and storage-related failures. Retain useful evidence and remove task-owned disposable inputs after use.

## Integration and release

Record whether Runic main moved, how the candidate incorporated it, checks repeated and the final integration SHA. If released, include a unique immutable version/tag, source/cohort, assets and release link. Otherwise state that no release was made. Record follow-up ownership without treating a research recommendation as completed implementation.
