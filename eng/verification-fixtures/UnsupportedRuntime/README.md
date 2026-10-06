# Unsupported runtime negative fixture

These two unchanged .NET 10 core DLLs were extracted from the SHA-verified
`8.1.0-runic.0.790.17.15.10` package pair built at
`2550376b230ccfb78beb8d3b4ced8866b65d809e`. They are licensed under MIT by
ReactiveUI and Contributors; see the repository [LICENSE](../../../LICENSE).
The [provenance](provenance.json) pins each source archive, entry, SHA256 and MVID.
Never rebuild or replace these bytes with the current runtime.

The A05 configuration helper deliberately substitutes one of these compile
references while retaining the current packed analyzer. That unsupported pairing
must fail with actionable `RUVG004`, without accepting unrelated dependency or
load failures. The old runtime lacks `Capabilities.ValidationRuntime`; no new
runtime-contract marker is required to recognize this mismatch.

This fixture is separate from the historical precompiled caller compatibility
fixture and from the accepted current `.30` package/runtime graphs. It does not
publish, execute or endorse the old runtime as a supported new consumer.
