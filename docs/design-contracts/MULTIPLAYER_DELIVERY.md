# Multiplayer delivery — M01

Design decision: 2026-09-12. User selected **Option A: show available play options only**. Native rollback remains the first post-launch Package 7 milestone, ahead of content packages. This contract defines intended behavior; it does not certify Steam integration or shipped network features.

## Launch menu and player flow

On supported Steam builds, Fighter Mode offers **Local Versus** and **Steam Remote Play Together**. Local Versus opens the existing local character-select/session flow. Remote Play Together shows short guidance, then enters that same local flow so the host can invite a friend through Steam. Example guidance: “Play local 1v1 with a friend through Steam. Your computer runs the match and streams it to your friend. Invite them through Steam's overlay.”

Use Steam's current supported invitation flow; this decision does not require a custom invitation browser or guessed API. Verify the overlay integration and instructions before release. Hide the Steam-specific menu entry when Steam integration is unavailable. If integration fails after opening it, show an honest unavailable message and retain Back/Local Versus; do not claim an invitation was sent.

Do not show disabled Native Online, Coming Later, LAN, matchmaking or ping-browser entries. Put future native rollback/LAN in the roadmap until released. Existing CPU choices and main-menu Calibration Drills remain available through their accepted flows; this menu change neither requires Story progress nor alters their rules.

Store/help copy may describe Steam Remote Play Together as the launch remote-play option, explicitly identifying streamed local play. Do not advertise native rollback as available, promise latency parity, or borrow Steam's general multi-player capacity as support for more than this game's two fighter slots. Keep protocol/engine details in technical documents rather than the user play flow.

## Delivery and gameplay boundaries

Steam Remote Play Together runs the game on the host, sends streamed audiovisual output and receives guest input. The guest is not a second instance of FTT's native rollback simulation. See [Valve's Remote Play description](https://store.steampowered.com/remoteplay), checked 2026-09-12.

Retain the local 1v1 rules, stage/hazard/item settings, actor ownership, Stock/Time scoring and fixed simulation pipeline. No streamed-input route changes HP, stocks, recovery, meter or input eligibility. Do not feed native peer prediction/resync data from streaming status, or imply the local rollback pipeline removes streaming latency.

Either assigned fighter can invoke the existing local pause menu on the host. It pauses that one shared simulation. Steam overlay visibility alone must not be assumed to pause gameplay; verify actual overlay/focus behavior. Native LAN/online keeps its separate synchronized-pause requirement.

The prohibition is on **delay-based-only native netcode**. It does not ban streamed local play or Package 7's bounded 0–3-frame input delay alongside rollback. Native handshake, hash/desync policy, synchronized start, reconnection, forfeit and future disconnect-win rules do not automatically apply to Remote Play Together.

## Controller, disconnect and response validation

Before claiming supported launch behavior, verify:
- Host and guest can each join exactly one fighter slot, select/readied characters, play mirror matches, use their remapped inputs and complete rematches. Steam-visible devices must bind consistently to local P1/P2; one input must not unintentionally operate both actors or both menu cursors.
- Keyboard/mouse sharing and controllers are handled through the existing binding/device model. State tested combinations in help; do not promise arbitrary mixed/shared layouts. Extra connected devices or guests cannot create a third fighter or take over a ready slot silently.
- Invite/accept/cancel, host/guest overlay use, scene loading, pause/resume, character select, post-match and rematch do not leak held inputs, bypass Ready or confuse slot ownership.
- Test guest disconnect, controller removal, stream interruption and reconnect separately. Establish what Steam actually exposes to the game versus only its overlay. On a reported device/session loss, clear held input and use the local controller-loss/pause flow with an explicit resume or return-to-selection path. Rejoining must restore the intended slot with fresh input, without resetting fighters or transferring control silently.
- Where the game cannot detect a streaming failure, do not promise automatic detection or a native reconnect timer. Validate Steam's notice and the host's usable local pause/exit path; document any support limitation. A delivery interruption does not itself synthesize a KO, disconnect win, CPU replacement or match result.
- Measure host and guest response, stutter, audiovisual clarity and recovery under representative connection/controller conditions. Record settings, observed latency and failures; no “lag-free” claim or invented universal connection threshold. Confirm timers and match results remain those of the host simulation.

Use the same slot, input-clear and local pause rules when applicable to physically local controller loss; this decision does not add a second competitive networking system. Any missing integration or unresolved loss path remains a release-validation issue, not an assumed pass.

## Authority and status

See [main design](../design-godot-v7.md), [Fighter rules](FIGHTER_MATCH_RULES.md), [controls/comfort](COMFORT_SETTINGS.md) and [rollback state inventory](ROLLBACK_STATE_CONTRACT.md). Preserve the historical preamble's dated claims as history; M01 governs current delivery/menu language. Concrete Steam binding, local device-loss hooks, storefront configuration and runtime support checks remain pending.
