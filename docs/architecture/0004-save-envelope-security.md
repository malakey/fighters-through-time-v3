# ADR 0004: Versioned authenticated save envelopes

Status: Accepted; platform key-provider spike pending

## Context

The current Story save is Base64-encoded JSON, global data is plaintext JSON, and a source constant is labeled as an encryption key but is unused. This does not provide confidentiality or integrity and does not support migrations or corruption recovery.

## Decision

- Save payloads use an envelope containing format version, schema version, nonce/IV, ciphertext, authentication data, created/updated timestamps, and payload type.
- Writes are atomic: write and validate a temporary file, rotate a last-known-good backup, then replace the primary.
- Loading validates the envelope and authentication before deserialization, then runs ordered schema migrations.
- Corrupt or tampered primary files fall back to the verified backup and report a recoverable error to UI.
- Secrets are supplied by a platform-aware key provider and are never committed as a source constant. The implementation spike will evaluate OS credential storage and Steam/user-bound derivation for Windows, macOS, and Linux/SteamOS.
- Story and global saves share envelope/migration infrastructure even if their payload schemas differ.

## Consequences

- `StorySaveData` and `GlobalSaveData` require explicit schema versions and migration tests.
- Existing Base64 prototype files need a one-time development migration or clearly documented reset policy before external builds.
- Encryption must not be described as anti-cheat; the primary goals are integrity, accidental-edit resistance, and safe migration/recovery.

