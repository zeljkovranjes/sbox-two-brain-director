# Changelog

## Unreleased

## 1.0.1 - 2026-08-03

- Fixed the offstage egress/re-entry ping-pong: the offstage flag now reconciles with actual host presence every tick instead of blindly toggling on ingress acknowledgements (monsters starting offstage were teleported back within a tick of every egress), and the offstage entry branch refuses to re-enter on the opportunity that owns the current sweep (recorded at sweep start).
- Fixed moves wedged against sealed offstage boundaries: the driver fails stalled moves after 2.5 s without progress instead of blocking up to 30 s, and the move timeout default is now 15 s.
- Fixed frontstage-bound modules (investigate, search, suspect response) chasing targets across sealed stage boundaries: they now yield while the monster is offstage so the Offstage module egresses first.
- Added `DebugStatusLine` to the s&box adapter component: one-line macro/micro state (mode, progression, module, motivations, flags, ingress bans, block id, telemetry) for HUDs and live probes.
- Added the `NoEgressReentryPingPong` regression scenario (181 tests total).

## 1.0.0 - 2026-08-03

- Fixed the unreachable default aggressive threshold: `AggressiveThresholdProgression` now defaults to 0.95 (the asymptotic fill never reaches 1.0, which previously stalled natural Normal→Aggressive transitions and kept monsters offstage forever).
- Fixed the s&box adapter reporting `Frontstage` unconditionally; presence is now derived from the monster's actual region.
- Added the deterministic macro core (`PressureDirector`): pressure gauge with the recovered fill formula, Normal/Aggressive modes, opportunity quotas and event quotas, cooldown/decrease timing, candidate latching with hysteresis, spatial exclusion rules, offstage sweep and ingress suggestion, script overrides, and full telemetry reason codes (60 unit tests).
- Added the deterministic micro core (`MonsterAgent`): typed perception with current-vs-remembered evidence and linear confidence decay, motivation arbitration, and 14 ordered modules (lifecycle/nav-recovery, script override, damage-stun, retreat, threat response, ambush, attack, suspect response, hiding target, investigate, search, stalk, offstage, idle) with full action-acknowledgement handling (75 unit tests).
- Added the AlienIsolationInspired compatibility preset: 12 verbatim decoded intensity configurations plus headliner profile and recovered micro tuning constants, isolated to the preset with confidence labels.
- Added the s&box adapter (`Code/SboxTwoBrains/Sandbox/`, namespace `SboxTwoBrains.Host`): `TwoBrainsComponent` host bridge, `IMonsterDriver` movement/animation abstraction, scene marker components (nav nodes, ingress, offstage regions, exclusion zones, targets), and the drop-in debug HUD overlay with basic/advanced telemetry views.
- Added the in-memory `FakeHost` and 23 integration tests covering all 13 required scenarios, byte-equivalent replay, save/restore continuation, three configured archetypes with zero core changes, and 2000-tick randomized invariants (180 tests total).
- Added a runnable examples suite: 15 self-checking examples (minimal host, fake-host scenario, three archetypes, offstage stalker, scripted encounter, replay, save/restore, telemetry, config inheritance, pressure tuning, omniscience policy, custom senses, compat preset) — `dotnet run --project examples` passes 15/15.
- Added the autonomous s&box compile pipeline: `dev/editor-rig/run_editor_gate.ps1` driver + `Editor/CompileGate.cs` in-editor hook (adapted from humanoid-retargeter's verified rig) and `dev/offline-check` fast pre-check. Real editor gate passes: the library compiles in the actual s&box editor with a clean SB500 whitelist.
- Added the deterministic core scaffold: engine-independent host contract (world/monster/target snapshots, sensed stimuli and memory records, navigation candidates, offstage regions, ingress points, pressure opportunities, script directives, decision batches, action results, telemetry, versioned saved state).
- Added validated profile configuration with deterministic single-chain inheritance, additive difficulty modifiers, documented units/ranges, and an effective-config describe/hash for save identity.
- Added the seedable, fully serializable deterministic RNG and canonical JSON serializer used by the replay contract.
- Added the `TwoBrainsSystem` facade implementing the explicit tick order: validate, acknowledgements, directives, macro update, micro update, deterministic conflict resolution, commit.
- Added the full documentation set: architecture, API map, configuration reference, getting started, tuning, evidence map, tick order, and editor-rig guide.
- Fixed the in-engine `Sandbox.Internal` resolution collision by namespacing the adapter as `SboxTwoBrains.Host` (found by the editor gate).
- Fixed the SB500 whitelist violations: removed `UnsafeRelaxedJsonEscaping` and `Array.Clone` usage (found by the editor gate).
- Fixed Vec3 JSON round-tripping via `[JsonConstructor]` and the FakeHost execution-enumeration bug.
