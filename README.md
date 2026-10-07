# Two-Brain Director

Deterministic two-layer monster AI for pursuers, stalkers and other hostile creatures, inspired by the
two-brains design of Alien: Isolation. A macro pressure director decides when and roughly where tension
should build; a micro agent decides what the monster actually does from its own perception. Both emit
declarative decisions: your game keeps entities, navigation, animation and combat. Contains no Creative
Assembly code or assets.

## Requirements

None. A navmesh is optional (`MonsterDriverBase` falls back to moving the transform).

## Install

Search for **Two-Brain Director** in the s&box library manager, or add `notpointless.two_brain_director`.

## Quick start

### In the editor

1. On the monster GameObject add **Monster Driver Base** (or your own component implementing
   `IMonsterDriver`) and **Two-Brain Monster AI**. Add a `NavMeshAgent` if the scene has a navmesh.
2. Scatter **Two-Brain Nav Node** objects through the level. The GameObject name is the node id; set
   `RegionId` and `Kind` (frontstage, offstage or ingress).
3. Add **Two-Brain Target** to the player.
4. Optional: **Two-Brain Offstage Region** and **Two-Brain Ingress** for vents and back corridors,
   **Two-Brain Exclusion Zone** for safe rooms.
5. Optional: add **Two-Brain Debug HUD Spawner** to any object for a live telemetry overlay.
6. Press play.

### In code

Through the component:

```csharp
using SboxTwoBrains.Components;
using SboxTwoBrains.Core;

var ai = monster.Components.Get<TwoBrainsComponent>();
ai.ReportStimulus( SenseChannel.Auditory, noisePosition, targetId: "player" );
ai.ReportDamage( 0.1 );
string save = ai.CaptureSave();
```

Or drive the engine-free core yourself, once per tick:

```csharp
using SboxTwoBrains.Core;

var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "demo" } );
var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

DecisionBatch batch = system.Tick( snapshot ); // snapshot: your WorldSnapshot for this tick
// Execute batch.Actions, then send ActionResults back in the next snapshot's Acknowledgements.
```

A complete host loop is in [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md).

## Options

**Two-Brain Monster AI** (`TwoBrainsComponent`):

| Option | Default | What it does |
|---|---|---|
| ProfileName | `ALIENISOLATIONINSPIRED` | Profile resolved from the catalogue. |
| Seed | 1337 | Master seed. Same seed and inputs give identical decisions. |
| TicksPerSecond | 20 | Policy ticks per simulated second. |
| UseCompatCatalogue | true | Use the Alien: Isolation-inspired preset catalogue; off = one generic profile. |
| MaxTicksPerFrame | 1 | Catch-up cap per fixed update. |
| DebugEnabled | true | Gates debug output; the HUD shows DISABLED while off. |
| NavNodeRadius | 60 | Metres around the monster within which nav nodes are reported. |
| LosEyeHeight | 1.7 | Eye height in metres for line-of-sight traces. |

**Monster Driver Base**: `BaseSpeed` (240 units/s, used without a NavMeshAgent), `ArriveDistance`
(24 units), `MoveTimeoutSeconds` (15). Every profile field (pressure, perception, modules) is in
[docs/CONFIG_REFERENCE.md](docs/CONFIG_REFERENCE.md); tuning per archetype in [docs/TUNING.md](docs/TUNING.md).

## How it works

Each tick the host builds a `WorldSnapshot` (monster, targets, stimuli, nav candidates, offstage regions,
ingress points, exclusion zones, directives, acknowledgements) and calls `TwoBrainsSystem.Tick`. The macro
`PressureDirector` runs a pressure gauge with normal and aggressive modes, opportunity quotas, cooldowns,
spatial exclusion and offstage sweeps, and hands the micro layer a region, roles and an expiry, never target
coordinates. The micro `MonsterAgent` arbitrates 14 ordered modules (investigate, search, stalk, ambush,
attack, retreat, offstage traversal and more) from its own decaying perception memory and returns action
requests; your host executes them and reports success or failure on a later tick. Time is explicit, all
randomness comes from a seeded RNG saved with the state, and float math is limited to + - * / and square
root, so identical inputs replay byte for byte. Details: [architecture](docs/ARCHITECTURE.md),
[API map](docs/API.md), [tick order](docs/TICK_ORDER.md), [evidence map](docs/EVIDENCE_MATRIX.md).

## Multiplayer

Single-player. Nothing is synced and the component does not check `IsProxy`; in a networked game run it on
the host only and replicate the monster with your own networking.

## Limitations

- Drives one monster per `TwoBrainsComponent`; there is no shared director across monsters.
- `MonsterDriverBase` treats ingress traversal as an instant teleport and plays no animation: subclass it
  to hook in your animgraph, combat and sound.
- The scene markers are found with scene-wide lookups each tick; very large marker counts cost time.
- Research-derived constants live only in the optional `AlienIsolationInspired` preset; their confidence
  labels are in [docs/EVIDENCE.md](docs/EVIDENCE.md).

## Development

```
sbox-check
dotnet test tests\SboxTwoBrains.Tests\SboxTwoBrains.Tests.csproj
dotnet run --project dev\examples\TwoBrains.Examples.csproj
dotnet build dev\offline-check\OfflineCheck.csproj -p:SboxRoot="<s&box install>"
powershell -ExecutionPolicy Bypass -File dev\editor-rig\run_editor_gate.ps1
```

`dev\run_all.ps1` runs the tests, the library and examples builds and the editor gate (needs Steam).

## License

No license file yet.
