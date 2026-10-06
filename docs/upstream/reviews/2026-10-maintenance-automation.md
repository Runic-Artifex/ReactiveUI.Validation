# Validation maintenance automation: 2026-10-06

This record covers maintenance preparation, package evidence and guarded release
promotion. It does not adopt upstream implementation code, publish a Validation
package or change the Generated API's **UNRELEASED** status. Earlier October
research, release receipts and RUV-001–018 remain historical evidence.

## Inputs and decisions

The audit and maintenance topics started from Runic main
`e9d853935403d5c1ec14660722a225e50e7ee3ea`, containing the released DynamicData
`.30` adoption. Concurrent authorized generator interoperability work later
advanced main to `90a44fcbcb627f6496767181f9adbbb5db74aa5a`; its files and
worktree are separate from this maintenance scope. Integration must preserve it
and verify the resulting revision.

The comparator was DynamicData main
`27346e4a9f5ec77343f856c10760ea04165c5ae5`, including reviewed monthly preparation,
release asset guarding and packaged acceptance. Validation already has stronger
matching-host NativeAOT and Generated application coverage; that coverage is
retained rather than duplicated.

| Gap at the audited Validation base | Maintenance decision |
| --- | --- |
| Monthly cadence is manual, with no preparation workflow. | Prepare pinned review inputs and immutable same-fork review branches/PRs. Do not execute upstream code, integrate it or contribute upstream. |
| Ordinary consumers can resolve shared global-cache bytes without proving they match the selected packages or current source. | Use gate-owned caches, isolated XML feeds and input/restored hash/source checks before execution. Preserve ordinary consumer behavior. |
| Native/Generated reruns can leave an older successful report after an early failure. | Invalidate success before prerequisite checks, retain partial/failure evidence and require explicit current completion. |
| Release dispatch can select another ref and directly publish without verifying new tag/draft asset bytes. | Promote the accepted pair from the latest trusted exact-main Build, validate reports/metadata, refuse existing release state, verify draft downloads and new tag before publication. Explicit `verify_only` creates no release state. |
| Artifact transfer and ordinary failure retention are weaker than the comparator. | Verify uploaded/downloaded contents and retain logs, graphs and current reports, including failures. |

The stale-report finding was reproduced independently without a build: seed a
temporary output with a previous success, invoke each strict verifier with a
mocked incorrect SDK, and observe rejection while the old `results.json` remains
unchanged. Fresh hosted jobs and the existing release workflow's required
verification dependency already reject failed jobs; the finding concerns rerun
and evidence reliability, not a demonstrated publication bypass.

## Contracts to retain

Preparation records inherited upstream
`cde3062937752abeb4216a92c15f5b3b37be9a34` separately from a later proven real
merge. It records absent/ambiguous ancestry honestly. The fresh inventory is
unassessed for that review and never copies historical completion decisions.
Month/base/upstream identify immutable snapshots and branches; retries recover
matching state. For the inherited linear baseline, the manifest reports
`inherited-shared-merge-base` and explicitly says no real upstream merge was
found. Later imported merge parents provide the separate real-merge evidence.
Snapshots live under `eng/upstream/reviews/YYYY-MM-base12-upstream12`; a retry
retains the first branch snapshot and archives the selected snapshot with its
branch status. Collection failures retain artifacts. If repository token
settings block a PR, retain the branch and exact manual compare URL. Human review,
a real merge and implementation verification remain subsequent decisions.

Release verification consumes an existing exact-main Build rather than deriving
a new package version through another build. Both branded assets must have the
same version, correct source/dependencies, MIT attribution and Generated payloads.
The latest eligible trusted run and current core/native/generated jobs must have
succeeded. A verification-only dispatch does not create a tag, draft or uploaded
release assets; set `verify_only: true` explicitly because the workflow default
is publication. Publication requires no existing tag/release,
verified draft bytes and a new tag resolving to the accepted source. No new
Validation release is part of this task.

The supported cohort remains .NET 10, SDK `10.0.401`, ReactiveUI/ReactiveUI.Reactive
`26.0.1` and the SHA-verified released Runic DynamicData `.30` pair. The immutable
`.5` pair remains solely the released `.790.17` expected-failure baseline.
Primitives remains free of System.Reactive and neither flavor may restore
upstream DynamicData or the opposite flavor.

Core CI retains Linux/Windows builds and tests plus ordinary/collection examples.
Both native host jobs consume the same accepted Linux shipping package pair.
Package artifacts include a source-bound manifest of both SHA-256 hashes and
sizes. Core jobs verify uploaded/downloaded contents; native jobs verify the
downloaded manifest and pair. Preserve the existing eight artifact names and
always-upload diagnostic reports/logs/graphs, with missing-file failures explicit.
Ordinary evidence lives under `artifacts/verification/package-smoke/`, with
`results.json` and flavor restore/runtime logs and graphs. Native and Generated
retain their respective `artifacts/verification/native-gates/` and
`artifacts/verification/generated-gates/` evidence directories. All reports
begin incomplete and retain partial checks; only normal successful completion
sets `completed: true`.
Hosted runtime Native mode produces four checks: one positive native executable
and one legacy expected-failure check per flavor, with five application cases in
each positive executable. Generated all-mode produces eighteen checks: six
managed/full-trim/native positives plus twelve expected diagnostic checks, with
seven application cases per positive executable and the intercepted overload
inventory preserved. These gates do not establish blanket package AOT support,
Android/native samples, actual Terra UI execution or ReactiveUI 25 compatibility.

## Evidence and adoption status

Observation **2026-10-06**: the integration owner reports audited-base Build
[37392395506](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37392395506)
successful across its four jobs. That result identifies `e9d8539`, not the later
maintenance implementation or concurrent main revision. Detailed topic commits,
guard results and integrated hosted evidence will be recorded after independent
review. No new workflow run, package publication or server-side immutable-release
setting is claimed by this record.

The integration owner must record the final source and exact successful hosted
run, package/report hashes and any remaining limits before treating these
operations as adopted. Preserve failures and distinguish local/offline proofs
from actual Windows/native execution. RUV-019–021 identify the contracts; they
do not advance upstream ancestry or replace RUV-018's release boundary.
