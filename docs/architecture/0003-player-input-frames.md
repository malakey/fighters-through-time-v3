# ADR 0003: Serializable per-player input frames

Status: Accepted

## Context

The current player and ability scripts poll global Godot actions. That can make multiple local fighters respond to the same device and prevents CPU or network-predicted commands from using the same controller API.

## Decision

- Every physics tick produces one `PlayerInputFrame` per player.
- A frame contains a tick number, quantized horizontal/vertical axes, and held/pressed/released bitmasks for Jump, Down, Basic Attack, Special 1, Special 2, Movement Ability, Block, Ultimate, Interact, and Pause where applicable.
- Input sources implement a common interface: local device, CPU, network/predicted, and deterministic test source.
- `PlayerController` and character abilities consume input frames; they do not call global `Input` gameplay polling.
- UI navigation may continue to use Godot UI actions because it is not authoritative combat input.

## Consequences

- Edge transitions must be derived consistently from consecutive frames.
- Input serialization becomes part of the online protocol and requires explicit versioning.
- Local device assignment and disconnect behavior can be tested without rendering a scene.

