# Changelog

## Unreleased

- Added the deterministic core scaffold: engine-independent host contract (world/monster/target snapshots, sensed stimuli and memory records, navigation candidates, offstage regions, ingress points, pressure opportunities, script directives, decision batches, action results, telemetry, versioned saved state).
- Added validated profile configuration with deterministic single-chain inheritance, additive difficulty modifiers, documented units/ranges, and an effective-config describe/hash for save identity.
- Added the seedable, fully serializable deterministic RNG and canonical JSON serializer used by the replay contract.
- Added the `TwoBrainsSystem` facade implementing the explicit tick order: validate, acknowledgements, directives, macro update, micro update, deterministic conflict resolution, commit.
- Added the evidence-to-component matrix, implementation plan, and tick-order documentation.
