# Fighters Through Time dependency pin

- Upstream: `https://github.com/xpTURN/Klotho`
- Release: `v0.6.1`
- Commit: `031f04250f6ae217ae44ada5059211ad0d274c8a`
- License: Apache-2.0 (`LICENSE` in this directory)
- Imported: 2026-08-03 from the release's `dist/addons/klotho/` directory

Klotho v0.6.1 is explicitly described upstream as experimental. This pin is
the approved deterministic Fighter simulation foundation and feasibility
baseline; it is not evidence that production rollback, transport, or platform
certification is complete. Upgrade only through a deliberate compatibility,
determinism, license, and target-platform review.

Local compatibility modification: `Klotho.props` selects Newtonsoft.Json
13.0.4 instead of upstream's 13.0.3 because the project's GdUnit4 API package
requires 13.0.4. No Klotho runtime or adapter source was modified.
