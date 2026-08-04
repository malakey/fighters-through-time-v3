# Architecture decisions

Architecture Decision Records (ADRs) capture choices that constrain multiple systems. They complement `design-godot.md`; they do not replace its gameplay/content specification.

| ADR | Decision | Status |
|---|---|---|
| 0001 | Shared definition data with mode-specific execution | Accepted |
| 0002 | Deterministic Klotho Fighter simulation | Accepted, integration spike required |
| 0003 | Serializable per-player input frames | Accepted |
| 0004 | Versioned authenticated save envelopes | Accepted, key provider pending platform spike |
| 0005 | Stable resource IDs and canonical tuning ownership | Accepted |
| 0006 | GdUnit4-first automated test strategy | Accepted, dependency installation pending |

Update an ADR when its decision changes. Supersede rather than silently rewriting a decision after implementation depends on it.

