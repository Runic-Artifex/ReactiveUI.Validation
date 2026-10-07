# Release asset guard

The manual workflow promotes the latest eligible completed Build for the exact
maintained-main SHA. It does not rebuild the accepted package pair.

`release_assets.py` requires the current-fork main Build, both core hosts and
both native hosts, complete core TRX and generated-managed reports for each core
host, and native/generated reports for the Ubuntu pair consumed by both native
hosts. Linux and Windows package bytes are separately validated because the
pack outputs are host-specific. The guard also requires branded `9.0.0-runic.N` packages,
source/dependency/license metadata,
and bundled generator/transitive props. It creates a draft, verifies uploaded
bytes, creates a non-force `v<version>` tag at the source SHA, then
publishes. Existing tags and releases always fail.
