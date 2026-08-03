# Implementation Plan — Two-Brain Director

Status: approved working plan (auto mode). Source spec: user prompt + `reference/aio-research/PROMPT.MD`
+ `docs/EVIDENCE_MATRIX.md`. This file is the single source of truth for layout, naming, and rules.

## 1. Language and runtime

**C# / .NET 10.** Reasons: s&box (the target engine) runs .NET 10 managed code; .NET is a mature
cross-platform runtime with first-class testing (xUnit) and deterministic managed FP for + − * /;
the same core sources compile both under `dotnet` (tests) and inside the s&box editor compiler.

## 2. Repository layout (actual, post-restructure)

```
two-brain-director/
  two-brain-director.sbproj        s&box library package (org notpointless, ident two_brain_director)
  two-brain-director.slnx          dotnet solution (tests + offline check)
  Code/
    SboxTwoBrains/                 THE LIBRARY — flat, one namespace `SboxTwoBrains`
                                   (contract, config, determinism, macro, micro,
                                   serialization, compat preset — pure, zero engine refs)
      Sandbox/                     engine adapter namespace SboxTwoBrains.Sandbox:
                                   components, snapshot builder, driver, debug overlay HUD
  Editor/CompileGate.cs            editor compile-gate hook (adapted from humanoid-retargeter M0Gate)
  examples/                        runnable example suite (console exe, shared-source core)
  ProjectSettings/                 s&box project settings
  dev/
    SboxTwoBrains.Tests/           xUnit (Core, Macro, Micro, Integration, FakeHost)
    editor-rig/                    run_editor_gate.ps1 (adapted), README
    offline-check/                 csproj: dotnet build of Code/** against Sandbox managed DLLs
    run_all.ps1                    tests → library build → examples build → editor gate
  docs/                            EVIDENCE_MATRIX, PLAN, ARCHITECTURE, API, CONFIG_REFERENCE,
                                   GETTING_STARTED, TUNING, EVIDENCE, TICK_ORDER
  reference/aio-research/          research repo clone (GITIGNORED — not redistributed)
  README.md  AGENTS.md  CHANGELOG.md  .gitignore  .gitattributes  .github/workflows/
```

The core is **shared-source**: `dev/SboxTwoBrains.Tests`, `examples` and `dev/offline-check`
compile `Code/SboxTwoBrains/**` directly (tests/examples exclude `Sandbox/**`); s&box compiles
it as part of the project. No package references, no duplication.

## 3. Hard rules for `Code/SboxTwoBrains` core files (must survive the s&box in-engine compiler)

From humanoid-retargeter's verified findings:

- Explicit `using`s in every file; **no** implicit-usings reliance (csproj sets
  `<ImplicitUsings>disable</ImplicitUsings>`).
- No `[GeneratedRegex]`, no `ZLibStream`, `Environment.NewLine`, `OverflowException`,
  `InvalidDataException`, `Type.IsPrimitive`, `Array.Clone()` (SB500 whitelist bans).
- `System.Text.Json`, `System.Numerics`, `System.Collections.Generic`, `System.Linq` are fine.
- Determinism: `double` math only (+ − * / and `Math.Sqrt`; **no** transcendental functions),
  no wall clock, no `System.Random`, no static mutable state, no engine singletons.
- Namespace `SboxTwoBrains` (flat); engine types (`Sandbox.*`, `Vector3`) never enter the core.
  The core defines its own `Vec3` (3×double).

## 4. Determinism contract

- `DeterministicRng`: xorshift128+ over two `ulong`s; `NextUInt`, `NextInt(max)`,
  `NextDouble()` = 53-bit / 2^53, `NextRange(min,max)`; state = 2 ulongs, fully serialized.
- Time: host passes `TickIndex` (long) + `DeltaTime` (double seconds) each tick; core keeps
  `SimTime` accumulator. Wall clock is never read inside policy.
- Replay: identical config + seed + tick inputs + snapshots + host acks ⇒ byte-identical
  `DecisionBatch` canonical JSON. Canonical serializer: fixed property order, invariant
  culture, doubles via `"R"` round-trip format.
- Save/restore: `SavedStateEnvelope { SchemaVersion=1, ConfigVersion, TickIndex, RngState,
  MacroState, MicroState }`; restore at tick n then identical inputs ⇒ identical output.

## 5. Host contract (summary — full detail in docs/API.md, code in Contract/)

Inputs per tick (`WorldSnapshot`): tick index, sim dt, monster snapshot, target snapshots,
current stimuli, navigation candidates, offstage regions, ingress points, script directives,
**pending host acknowledgements** (`ActionResult`).

Outputs (`DecisionBatch`): optional `PressureDecision` + list of `ActionRequest` + telemetry.

- `PressureDecision`: mode, progression, urgency, candidate region id, allowed roles,
  ingress/exclusion constraints, expiry tick, reason code, evidence strings. **No target
  coordinates, no movement instructions.**
- `ActionRequest`: stable id (deterministic counter), kind (MoveTo/Search/Investigate/Stalk/
  Ambush/Threat/Chase/Attack/Retreat/UseIngress/Wait/Scripted/Custom), params, expiry.
- `ActionResult.Status`: Succeeded | PartiallySucceeded | Rejected | Deferred | Interrupted |
  Failed — applied at the start of a later tick; failures feed timers/flags/recovery.

## 6. Macro — `PressureDirector` (docs/TICK_ORDER.md steps 2–5)

State: mode (Normal|Aggressive), progression [0,1], completed count, event-quota progress/target,
enabled, candidate latch, cooldown remaining, decrease-delay remaining, active candidate id, rng.

Behavior: fill while eligible `(1−p)/max(fill,0.5)·dt`; decrease after `DecreaseDelaySeconds` at
`1/decrease·dt`; cooldown after completion; aggressive completion → count++, mode→Normal, reason
code; Normal→Aggressive when progression ≥ threshold ∧ count < max ∧ cooldown 0 ∧ candidate valid
∧ exclusion rules pass; optional event quota (seeded random target in configured range) emits a
quota-reached event and resets; script directives override mode/progression/reset/profile and are
telemetry-visible; sweep planner produces offstage sweep + ingress suggestions with vent
attract/ban timing — all against host-supplied regions/ingress, never invented coordinates.

## 7. Micro — `MonsterAgent` (steps 6–8)

Perception: typed stimuli (Visual/Auditory/Touch/Damage/Light/GameDefined), each with activation,
active latch, confidence, created/last-confirmed ticks. Current evidence (this tick) vs memory
(decayed confidence per channel half-life) are separate views; cross-sense combination is
configurable; deterministic tie-breaking.

Ordered guarded modules (each → Ineligible | Running | Succeeded | Failed | ActionRequest):

1. Lifecycle (death/suspend/reset/**pathfinding-failure recovery**)
2. ScriptOverride (host/cinematic directives)
3. DamageStun (stun gauge response)
4. Retreat/Withdrawal (retreat gauge, threat-driven)
5. ThreatResponse (flame/aimed weapon/close target/visual retention → hesitate/flank/withdraw)
6. Ambush
7. Attack (precondition → core: chase / attack / ingress flank / terminate)
8. SuspectResponse + HidingTarget
9. Investigate (staged: react → approach → inspect → search)
10. Search (systematic, region + history)
11. Stalk (frontstage), Offstage (sweep/ingress/backstage stalk) — macro-biased
12. Idle fallback

Every action checks: reachability, route distance, target state, cooldowns, flags,
animation/combat feasibility (host-reported), and pending host acks. Rejected/failed results set
cooldowns and steer alternative selection next tick. Module order is per-profile config.

## 8. Config system

`MonsterProfileConfig` (sections: Pressure, Perception, Search, Threat, Combat, Offstage,
Modules, Movement) with `BasedOn` inheritance (acyclic, cycle = startup error), deterministic
resolution, validated ranges with documented units, and `EffectiveConfig` exposing final values.
`Compat/AlienIsolationPresets`: 13 decoded configs (verbatim values from
`alienconfigs_decoded.csv`) + traced setup progressions, clearly labelled; reversed min/max pairs
preserved and flagged.

## 9. Verification plan

- xUnit: formulas, timers, every state transition, selector/utility rules, memory decay,
  config inheritance, every action-result path.
- Property-style tests (seeded loops): bounds/invariants from CLEAN_ROOM_SPECIFICATION §invariants.
- Replay: two identical runs ⇒ byte-equal canonical decision JSON; save at tick k, restore,
  continue ⇒ identical to uninterrupted run.
- Failure/recovery: pathfinding failure, host rejection/deferral/interruption.
- Integration: in-memory `FakeHost` driving macro+micro through the 13 required scenarios.
- Archetypes: ≥3 monsters (stalker, brute, lurker) configured with zero core changes.
- Offline compile check (`dotnet build dev/offline-check`), then the s&box editor gate
  (`dev/editor-rig/run_editor_gate.ps1`; needs Steam + interactive session).
- `dotnet format --verify-no-changes` on tests project; docs lint by review.

## 10. Execution order (subagent work packages)

1. ✅ research + matrix + plan
2. Scaffold (git, sbproj, sln, csproj, gitignore, AGENTS.md) — me
3. Contract + Determinism + Config — me (foundational, everything depends on it)
4. Macro core — coder subagent
5. Micro core (perception/memory + modules) — coder subagent(s)
6. Serialization + Compat preset — coder subagent
7. Test suites (unit/property/replay/integration/fake host) — coder subagent(s)
8. s&box adapter + debug overlay (design from adaptive-director-demo) + examples — coder subagent
9. dev/editor-rig + offline-check + run gates — me
10. Docs (ARCHITECTURE/API/CONFIG/TICK_ORDER/ADAPTERS/TESTING/COMPAT) + README — subagent + me
11. Final verification sweep + report — me
