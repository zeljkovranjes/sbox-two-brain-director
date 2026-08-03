# Verification report

How the library is verified, and how to re-run every check. Current status: **all green**.

| Layer | Command | Result |
|---|---|---|
| Unit + integration tests | `dotnet test dev/SboxTwoBrains.Tests` | 180/180 pass |
| Examples (runnable, self-checking) | `dotnet run --project examples/TwoBrains.Examples.csproj` | 15/15 pass |
| Offline s&box compile pre-check | `dotnet build dev/offline-check/OfflineCheck.csproj` | 0 errors, 0 warnings |
| Real s&box editor compile gate | `powershell dev/editor-rig/run_editor_gate.ps1` | PASS (assembly `package.notpointless.two_brain_director`, core type `SboxTwoBrains.TwoBrainsSystem`, 0 compile errors, SB500 clean) |
| One-shot everything | `powershell dev/run_all.ps1` | all of the above |

## Test matrix (180)

- **Core (22)** — deterministic RNG (sequence/state/fork/bounds), canonical JSON byte-stability, tick validation, config inheritance (child-wins, cycles, missing parents, modifier clamps), validation ranges, preset resolution and decoded values.
- **Macro (60)** — fill formula bit-exactness, decrease timing, threshold crossing, completion/cooldown, quota block + event quota, resets, all directives, all opportunity ack paths, exclusion margins, hysteresis, ingress filtering, urgency, save/restore byte-equality, double-run determinism, 2000-tick property invariants ×3 seeds.
- **Micro (75)** — perception merge/decay/eviction, thresholds + latches, omniscience edge, every module's gates, arbitration priority, preemption, attack matrix, chase give-up, threat hesitation/flank determinism, retreat triggers, nav-failure escalation, all six ack statuses (incl. deferred-twice), staged investigate, search selection/revisit/give-up, stalk exclusion, ingress→sweep→exit, action lapse, id uniqueness, save/restore continuation, double-run determinism.
- **Integration (23)** — all 13 required scenarios through `TwoBrainsSystem` + `FakeHost`; 300-tick byte-equivalent replay; 150/save/150 continuation via canonical JSON envelope; three archetypes (stalker/brute/lurker) with divergent behavior from config alone; 3 seeds × 2000 ticks system invariants with mid-run restore.

## Issues found by the pipeline and fixed

1. `SboxTwoBrains.Sandbox` namespace collided with s&box's `Sandbox.Internal` globals — adapter renamed to `SboxTwoBrains.Host`.
2. `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` — SB500 whitelist violation, removed.
3. `Array.Clone()` in config inheritance — SB500 whitelist violation, replaced.
4. `Vec3` JSON round-trip — `[JsonConstructor]` added.
5. FakeHost `List<T>` mutation during enumeration — fixed.

## Assumptions and remaining gaps

- The gauge fill is asymptotic: with `AggressiveThresholdProgression = 1.0` and small dt it never quite reaches 1.0 (verified numerically). Use thresholds < 1.0 for natural transitions, or coarse dt; scripted/forced transitions are unaffected. Documented in docs/CONFIG_REFERENCE.md.
- `SetPressureMode(Aggressive)` without `ResetGauge` discharges immediately (no sweep window armed); use `ForceOpportunity` for a sustained scripted encounter. Documented in docs/API.md.
- `quota_blocked` re-emits each tick while the condition holds — filter in telemetry consumers.
- NavCandidate `RouteDistance`/LOS from the s&box adapter are conservative (−1/false) unless the host supplies better; the core handles both.
- s&box runtime smoke (HUD visuals, input string, driver movement in a live scene) is verified at compile level only; it gets its live pass in the demo project.
- Research confidence: preset values are proven decoded data; macro/micro wiring is strong reconstruction; no claim of exact parity. See docs/EVIDENCE.md.
