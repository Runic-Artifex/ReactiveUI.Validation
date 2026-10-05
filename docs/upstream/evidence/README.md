# Upstream review diagnostic evidence

These diagnostics were run on NixOS with the repository's pinned .NET SDK 10.0.401
and both validation flavors at fork commit `509dd45`. The final run compiled
successfully and executed four expectations twice:

| Expectation | Primitives | System.Reactive | Finding |
| --- | --- | --- | --- |
| `ReplacingViewModelDetachesOldValidation` | Failed | Failed | A detached model still writes its error to the view. |
| `BindingDisposalIsIdempotent` | Failed | Failed | A second Dispose call throws NullReferenceException. |
| `HasErrorsIsCurrentInsidePropertyChanged` | Passed | Passed | The tested plain PropertyChanged callback sees the current state. |
| `EntityValidationAppearsInGetErrors` | Passed | Passed | A general rule's error is returned by GetErrors(null). |

Failures are expected assertions against desired behavior, not shipping-test
failures. They are not claimed to be exhaustive or verified on Windows. The
unchanged shipping suite passed all 402 tests before this investigation and again
after removing the temporary diagnostic class and rebuilding both flavors.

[Source](UpstreamInvestigationProbes.cs.txt) and [final output](probe-results.txt)
are retained. Initial diagnostic drafts did not compile because of analyzer
formatting findings and the unavailable legacy WhenAnyPropertyChanged API; the
final source uses plain PropertyChanged for that narrower observation probe.
Only the final successful compilation and runtime results support the findings.

To reproduce from the repository root, temporarily copy the source into the
Primitives test project. Its shared-source counterpart automatically compiles
the same tests into the Reactive flavor:

```sh
cp docs/upstream/evidence/UpstreamInvestigationProbes.cs.txt \
  src/tests/ReactiveUI.Validation.Tests/UpstreamInvestigationProbes.cs
cd src
dotnet test --solution ReactiveUI.Validation.slnx -c Release -- \
  --maximum-parallel-tests 4 \
  --treenode-filter '/*/*/UpstreamInvestigationProbes/*' --report-trx
```

On the Runic NixOS desktop, prefix the test command from `src` with
`direnv exec ../../runic-sdk`. Expect exit code 2 for the reviewed baseline.
Remove only the temporary `UpstreamInvestigationProbes.cs` after collecting
results, then rebuild and run the normal suite to avoid stale diagnostic
binaries. Do not overwrite an existing file with that name. The retained
`.cs.txt` source is intentionally outside the normal compilation inputs.
