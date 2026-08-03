# Getting started

The core is engine-independent pure C# (`Code/TwoBrainsCore`). In an s&box project,
place this repository in your project's `Libraries/local.two_brain_director`
directory; anywhere else, compile the sources directly into your host. The
integration contract is always the same four steps per tick.

## 1. Build a profile catalogue

Register named profiles, or start from the research-derived preset catalogue:

```csharp
using TwoBrains.Core.Config;

var catalogue = new ProfileCatalogue()
    .Add( new MonsterProfileConfig { Name = "demo" } ); // all generic-baseline defaults

// Or, for the Alien: Isolation-inspired preset set:
// var catalogue = TwoBrains.Core.Compat.AlienIsolationPresets.CreateCatalogue();
```

Resolution and validation happen here, once — bad ranges, missing parents, and
inheritance cycles throw `ConfigException` at startup, not mid-game.

## 2. Construct the system

```csharp
using TwoBrains.Core;

var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );
// Optional additive difficulty modifier (must also be in the catalogue):
// var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL, modifierName: "hard" );
```

The seed fully determines all randomness. Same seed + same inputs = same decisions.

## 3. Run the per-tick host loop

Every simulated tick, in this order:

1. **Build a `WorldSnapshot`** from your game state: monster, targets, fresh stimuli,
   navigation candidates, offstage regions, ingress points, exclusion zones, script
   directives, and the acknowledgements you produced since the last tick.
2. **Call `system.Tick(snapshot)`** → `DecisionBatch`. Tick indices must be strictly
   sequential; a gap or duplicate throws.
3. **Execute the batch's `Actions`** with your own movement/navigation/combat code.
   A request is an intention, never a fait accompli — you may refuse it.
4. **Feed `ActionResult`s back on a later tick** in the next snapshot's
   `Acknowledgements` list. Opportunities are acknowledged the same way, using the
   `PressureDecision.OpportunityId` as the `ActionResult.ActionId`.

## 4. A complete fake host

Self-contained, pure core, no engine. One monster, one target walking away at
1.5 m/s, immediate acknowledgements:

```csharp
using System;
using System.Collections.Generic;
using TwoBrains.Core;
using TwoBrains.Core.Config;
using TwoBrains.Core.Contract;

// --- one-time setup ------------------------------------------------------
var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "demo" } );
var system    = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

// --- fake world state ----------------------------------------------------
var monsterPos = new Vec3( 0, 0, 0 );
var pendingResults = new List<ActionResult>();

for ( long tick = 0; tick < 60 * 60; tick++ )               // one simulated minute at 60 Hz
{
    double dt = 1.0 / 60.0;
    var targetPos = new Vec3( 10.0 + 1.5 * tick * dt, 0, 0 );

    // 1. build the snapshot ------------------------------------------------
    var snapshot = new WorldSnapshot
    {
        TickIndex        = tick,
        DeltaTimeSeconds = dt,
        Monster          = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "hall" },
        Acknowledgements = new List<ActionResult>( pendingResults ),
    };
    snapshot.Targets.Add( new TargetSnapshot
    {
        TargetId  = "p1",
        Position  = targetPos,
        RegionId  = "hall",
        IsVisible = monsterPos.PlanarDistanceTo( targetPos ) < 25.0,
    } );
    pendingResults.Clear();

    // 2. tick the policy ----------------------------------------------------
    DecisionBatch batch = system.Tick( snapshot );

    // 3. execute action requests -------------------------------------------
    foreach ( var action in batch.Actions )
    {
        if ( action.Destination is Vec3 dest )
        {
            var to = dest - monsterPos;
            double distance = to.Length();
            if ( distance > 0.0 )
            {
                double step = Math.Min( distance, 4.0 * action.SpeedScale * dt );
                monsterPos = monsterPos + to * ( step / distance );
            }
        }
        pendingResults.Add( new ActionResult
        {
            ActionId = action.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick,
        } );
    }

    // 4. acknowledge a macro opportunity (same ActionResult channel) -------
    if ( batch.Macro != null && batch.Macro.OpportunityId.Length > 0 )
        pendingResults.Add( new ActionResult
        {
            ActionId = batch.Macro.OpportunityId, Status = ActionStatus.Succeeded, ResultTick = tick,
        } );

    // batch.Telemetry carries a reason-coded record of every transition.
}
```

A real host differs only in step 3: route requests through your navigation and
animation systems, and report honest `Rejected`/`Deferred`/`Interrupted`/`Failed`
results instead of always succeeding.

## 5. Save and restore

```csharp
using TwoBrains.Core.Serialization;

SavedStateEnvelope save = system.CaptureState();
string json = CanonicalJson.ToJson( save );          // persist anywhere

// Later — a system constructed with the same catalogue, profile and seed:
var restored = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );
restored.RestoreState( CanonicalJson.FromJson<SavedStateEnvelope>( json ) );
// Continue feeding snapshots from restored.NextTickIndex; output is identical
// to an uninterrupted run.
```

`RestoreState` validates the schema version only. Restoring into a system with a
different profile or seed silently breaks the replay contract, so compare
`ConfigVersion` (or `EffectiveConfig.ComputeHash()`) before trusting a save.

## Next steps

- `examples/` — runnable demo hosts and archetype configurations.
- `Code/TwoBrainsSbox/` — the s&box adapter: components, a world-snapshot builder,
  an action executor, and a debug overlay.
- [TUNING.md](TUNING.md) — pacing and perception tuning per archetype.
- [CONFIG_REFERENCE.md](CONFIG_REFERENCE.md) — every field, range, and default.

See also: [architecture](ARCHITECTURE.md) · [API map](API.md) · [configuration reference](CONFIG_REFERENCE.md) · [tuning](TUNING.md) · [evidence](EVIDENCE.md) · [tick order](TICK_ORDER.md)
