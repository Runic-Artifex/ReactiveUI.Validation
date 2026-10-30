# Release guard and nuget.org publishing

`release_assets.py` is the narrow gate used by the manual **Package release**
workflow ([`release.yml`](../../.github/workflows/release.yml)). It promotes the
package pair of the latest eligible completed Build for the exact maintained-main
SHA; it does not rebuild. The pair is exactly:

- `RunicArtifex.ReactiveUI.Validation.<version>.nupkg`, depending on `ReactiveUI`
  26.0.1 and `RunicArtifex.DynamicData` 10.0.0;
- `RunicArtifex.ReactiveUI.Validation.Reactive.<version>.nupkg`, depending on
  `ReactiveUI.Reactive` 26.0.1 and `RunicArtifex.DynamicData.Reactive`
  10.0.0.

The AndroidX packages are not part of a release (see
[CONTRIBUTING](../../CONTRIBUTING.md#scope)). Earlier releases were published only
as GitHub release assets, under the IDs `Runic.ReactiveUI.Validation` and
`Runic.ReactiveUI.Validation.Reactive`. Those assets stay as they are; nuget.org
has only the `RunicArtifex.*` IDs, and the guard refuses the legacy IDs.

## Build evidence

`resolve`, `download` and `verify` require the current-fork main Build, both core
hosts and both native hosts, complete core TRX and generated-managed reports for
each core host, and native/generated reports for the Ubuntu pair consumed by both
native hosts. Artifacts are downloaded by ID and checked against their GitHub
SHA-256 digests. Linux and Windows package bytes are separately validated because
the pack outputs are host-specific; the Ubuntu pair is the one released. `stage`
copies exactly that verified pair into `artifacts/packages`.

## Package checks

Every command that takes `--packages` first validates the folder: exactly the two
`RunicArtifex.*` packages at one major-9 version (stable, such as `9.0.0`, or
`9.M.P-runic.N`), a single `net10.0`
dependency group with the pinned ReactiveUI and RunicArtifex.DynamicData cohort and
no upstream, legacy or opposite-flavor dependency, the MIT license, the bundled
generator and flavor-named `buildTransitive` props, and repository URL and commit
matching the current workflow repository and `GITHUB_SHA`. The fork attribution
is checked too: authors exactly `ReactiveUI and Contributors, Runic Artifex`, no
`<owners>` element (nuget.org takes owners from the account), the original
`Copyright © 2019-… ReactiveUI and Contributors` notice, and a title and
description that start by naming the Runic Artifex fork of ReactiveUI.Validation
and name the flavor (`ReactiveUI.Primitives` or `System.Reactive`).

## GitHub release

A `-runic.N` version becomes a GitHub prerelease; a stable version (such as
`9.0.0`) becomes a regular release. The draft's prerelease flag is checked when
it is created and again before it is published.

`publish` refuses every existing tag or release, creates a draft targeted at the
SHA, uploads the exact two assets, downloads them for byte comparison, creates and
resolves a new non-force `v<version>` tag at that SHA, and only then publishes the
draft. The release carries generated release notes; it is where the fork's
release notes live. GitHub publishing is restricted to a clean checkout of
maintained `main` at `GITHUB_SHA`, and verifies that `main` remains the default
branch. `check` runs the same GitHub checks read-only. A failed verification
leaves the draft for deliberate recovery.

## nuget.org publishing

`nuget-check` and `nuget-push` use only the Python standard library and keyless,
read-only lookups of the public nuget.org service index, search and
flat-container endpoints. Before anything is pushed they check every package of
the pair:

- The ID must match a package glob of [`nuget-policy.json`](nuget-policy.json),
  the hand-kept mirror of the nuget.org trusted-publishing policy (owner
  `runic-artifex`, glob `RunicArtifex.*`, may push new packages). nuget.org
  publishes no policy, so the mirror is the only source; keep it in step with the
  account.
- Every declared dependency must be listed on nuget.org at its minimum version, so
  consumers can restore the package from nuget.org alone.
- If nuget.org already has that version, it is downloaded and compared with the
  local package entry by entry. nuget.org adds its repository signature
  (`.signature.p7s`) to every package it accepts, so that entry is ignored and
  every other entry must be byte-identical. An identical version is skipped; a
  different one fails, because published versions are immutable.
- A package still to be pushed must have an ID that is new on nuget.org or that
  `runic-artifex` owns. An ID nuget.org knows but whose search lists no version
  (all versions unlisted, or a push minutes ago not yet indexed) fails because
  its owners cannot be checked.

A failure names each package and the remedy, and nothing is pushed: the
trusted-publishing key would otherwise answer 403 during `dotnet nuget push`,
possibly after the first package was already published. `nuget-push` runs only in
GitHub Actions, repeats these checks and then runs `dotnet nuget push <package>
--source https://api.nuget.org/v3/index.json --api-key "$NUGET_API_KEY"` for each
missing package. `wait-indexed <version>` polls the flat-container version lists,
which `dotnet restore` uses, every 30 seconds for up to an hour until both list
the version.

## Workflow jobs and dry run

Run **Package release** on `main`. Its `dry-run` input defaults to checked.

- `packages` (`actions: read`, `contents: read`) runs the guard fixtures, then
  `resolve`, `download`, `verify` and `stage`, then `check` and `nuget-check`;
  it records the version and `sha256sum` of the pair and uploads the pair as the
  `packages-<run id>` artifact, plus the guard evidence.
- `github-release` (`contents: write`) downloads that artifact, checks it against
  the recorded hashes and runs `publish`.
- `nuget` runs after the GitHub release, in the `nuget` environment, and is the
  only job with `id-token: write`. It downloads and checks the same artifact, runs
  `nuget-check` before logging in, logs in with `NuGet/login`
  (`user: ${{ vars.NUGET_USER }}`), then runs `nuget-push`.
- `indexed` (`contents: read`, no environment or secret) runs `wait-indexed`.

A dry run ends after `packages`: the Build evidence, every package validation,
the read-only GitHub checks and the keyless nuget.org checks run, and the GitHub
release, the NuGet login, the push and the indexing wait are skipped. A failing
dry-run check is the failure the real run would hit.

## Retries

If `nuget` fails, use **Re-run failed jobs** in the same run: `github-release` is
not repeated, and `nuget` pushes the same artifact again, skipping any version
nuget.org already has with identical contents. If the push failed with 409
Conflict, nuget.org is still validating an earlier push of that version; wait
until it is listed, then rerun. If `indexed` times out, rerun it; nuget.org can
take up to an hour to list an accepted package. A new run for the same commit
cannot help: its GitHub release check refuses the existing tag, and a changed
package needs a new version.

## nuget.org account setup

Done once, by an administrator of the `runic-artifex` nuget.org organization and
of the GitHub repository:

1. On nuget.org, open **Trusted Publishing** for `runic-artifex` and add a policy
   for repository owner `Runic-Artifex`, repository `ReactiveUI.Validation`,
   workflow file `release.yml` and environment `nuget`. Allow it to push new
   packages and give it the package glob `RunicArtifex.*`, matching
   [`nuget-policy.json`](nuget-policy.json). (The DynamicData policy names its own
   repository, so this repository needs its own policy.)
2. In the GitHub repository settings, add the repository variable `NUGET_USER`
   with the nuget.org profile name that owns the policy; `NuGet/login` exchanges
   the workflow's OIDC token for a short-lived key as that user.
3. Create the `nuget` environment, restricted to the `main` branch, with required
   reviewers so that each push to nuget.org is approved.

Then run **Package release** with `dry-run` checked; it reports both IDs as new.

## Tests

Run the offline tests with:

```sh
python3 -B -m unittest discover -s eng/release -p test_release_assets.py -v
```

They generate small `.nupkg` fixtures and use mocked GitHub calls, a local
`dotnet` stand-in and a mock nuget.org served on `127.0.0.1`. They cover the Build
evidence, the package and attribution checks, staging, the read-only GitHub check
and the draft-first publication, and for nuget.org: new IDs, an identical
published version (skipped), a different published version, an ID owned by
another account or with unknown owners, an ID outside the package glob, a
dependency nuget.org does not list, a partial push and its retry, the dry-run
check, the indexing wait and its timeout, and the workflow's dry-run default and
job permissions. They check that every nuget.org lookup is keyless, and they do
not contact GitHub or nuget.org. `NUGET_TEST_SERVICE_INDEX` and `NUGET_POLICY`
exist for these tests only and are refused in GitHub Actions.
