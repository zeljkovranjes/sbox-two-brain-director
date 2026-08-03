# Tick Order and Conflict Resolution

The `TwoBrainsSystem` facade is the only entry point. One `Tick(WorldSnapshot)` call runs the
phases below in exactly this order. Subsystems may not reorder phases or read state "from the
future". All aging uses the explicit `DeltaTimeSeconds` from the snapshot.

```mermaid
sequenceDiagram
    participant Host
    participant Sys as TwoBrainsSystem
    participant Macro as PressureDirector
    participant Micro as MonsterAgent
    Host->>Sys: Tick(snapshot n)
    Sys->>Sys: 1 validate (tick==n expected, dt range, monster present)
    Sys->>Macro: 2a ApplyOpportunityResults(acks for pending opportunity)
    Sys->>Micro: 2b ApplyActionResults(all other acks)
    Sys->>Sys: 3 route directives (SetProfile → config; others → macro/micro)
    Sys->>Macro: 4 Tick → PressureDecision? (ages cooldown/decrease/sweep/ban timers first)
    Sys->>Micro: 5 Tick(macro bias) → [ActionRequest] (ages memory/timers first)
    Sys->>Sys: 6 conflict resolution (unique ids, future expiries)
    Sys->>Sys: 7 commit (sim time += dt, next tick = n+1, state hash)
    Sys-->>Host: DecisionBatch(n)
    Host-->>Sys: (later tick) ActionResult acks in snapshot n+k
```

## Phase notes

1. **Validate.** Tick indices must be exactly sequential. A gap or duplicate is a host bug and
   throws — silently tolerating it would break replay equality.
2. **Acknowledgements.** An ack whose id equals the macro's pending opportunity id goes to the
   macro; everything else goes to the micro. Unknown ids are ignored with telemetry, never fatal.
   Terminal statuses: Succeeded, PartiallySucceeded, Rejected, Interrupted, Failed. Deferred is
   non-terminal and may repeat. A terminal ack removes the pending entry; failures and rejections
   set cooldowns/flags that steer next-tick selection (pathfinding-failure recovery included).
3. **Directives.** `SetProfile` re-resolves config immediately (telemetry `profile_switch`).
   Mode/progression/reset/opportunity directives go to the macro; withdrawal/scripted/despawn go
   to the micro. All are recorded as explicit overrides.
4. **Macro update.** Ages cooldown, decrease-delay, sweep and ingress-ban timers; advances the
   gauge while eligible; decreases it while ineligible; evaluates Normal↔Aggressive transitions,
   quota events and expiry; emits at most one `PressureDecision` per tick.
5. **Micro update.** Ages memory decay, timers and gauges; merges current stimuli into memory;
   evaluates modules in arbitration order; first non-Ineligible module wins; emits declarative
   requests only.
6. **Conflict resolution.** Modules already produce at most one primary action; the facade then
   enforces unique action ids and future expiry ticks, failing loudly on violations.
7. **Commit.** Sim time accumulates, the next expected tick advances, and a FNV-1a state hash is
   computed over both subsystem states + both RNG words + tick + sim time.
8. **Return.** The batch is pure data; hosts execute it and acknowledge later.

## Determinism rules recap

- No wall clock, no `System.Random`, no engine singletons, no static mutable state.
- `double` math with + − * / and `Math.Sqrt` only (no transcendental functions).
- All randomness flows through the two seeded `DeterministicRng` forks (macro stream 1,
  micro stream 2). Both words are inside every save.
- Sorted containers in state keep canonical JSON byte-stable.
