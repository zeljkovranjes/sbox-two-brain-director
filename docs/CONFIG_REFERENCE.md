# Configuration reference

Profiles are authored as `MonsterProfileConfig` (every field nullable — null means
"inherit"), resolved by a `ProfileCatalogue` into a fully-populated, validated
`EffectiveConfig`. The defaults below are the **generic baseline** the resolver
applies when no profile in the inheritance chain sets a field; they are clean-room
starting values, not research-derived data. Ranges are enforced at resolve time and a
violation throws `ConfigException` listing every offending field.

Authoring types: `PressureSection`, `PerceptionSection` (with
`PerceptionChannelSection` per channel), `SearchSection`, `ThreatSection`,
`CombatSection`, `OffstageSection`, `ModulesSection`, `MovementSection`. Their
nullable fields mirror the tables below; the resolved runtime values live on the
nested `EffectiveConfig.Resolved*` classes (`ResolvedPressure`,
`ResolvedPerception`, `ResolvedPerceptionChannel`, `ResolvedSearch`,
`ResolvedThreat`, `ResolvedCombat`, `ResolvedOffstage`, `ResolvedModules`,
`ResolvedMovement`).

Units: timers in **seconds**, distances in **metres**, fractions unitless in
**[0,1]**, counts dimensionless.

## Pressure

Macro pacing: the gauge, mode transitions, quotas, sweeps, and exclusion margins.

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `FillSeconds` | s | 3.0 | [0.5, 600] | Time for an eligible gauge to fill (asymptotic: `(1−p)/fill·dt`). Hard floor 0.5 (`MinFillSeconds`). |
| `CooldownSeconds` | s | 25.0 | [0, 600] | Cooldown after an opportunity completes before pressure may rise again. |
| `DecreaseSeconds` | s | 20.0 | [0.1, 600] | Time over which a full gauge decays to zero while ineligible. |
| `DecreaseDelaySeconds` | s | 0.0 | [0, 600] | Grace period after losing eligibility before decrease starts. |
| `MaxOpportunities` | count | 4 | [1, 100] | Completed opportunities before the quota blocks new cycles. |
| `EventQuotaMin` | count | 0 | [0, 100] | Event-quota random target lower bound; 0 disables the quota. |
| `EventQuotaMax` | count | 0 | [0, 100] | Event-quota random target upper bound (inclusive). Must be ≥ `EventQuotaMin`. |
| `AggressiveThresholdProgression` | [0,1] | 0.95 | [0.0001, 1] | Progression at which Normal may switch to Aggressive. Must be < 1 for natural transitions: the fill is asymptotic and never quite reaches 1.0. |
| `StartProgression` | [0,1] | 0.0 | [0, 1] | Progression a fresh/reset cycle starts at. |
| `SweepDurationSeconds` | s | 50.0 | [1, 600] | How long an offstage sweep opportunity stays active. |
| `SweepMinDistance` | m | 10.0 | [0, 1000] | Minimum route distance for sweep staging. |
| `SweepMaxDistance` | m | 60.0 | [0, 1000] | Maximum route distance for sweep staging. Must be ≥ `SweepMinDistance`. |
| `SweepIdleMinSeconds` | s | 5.0 | [0, 600] | Idle dwell at a sweep node, lower bound. |
| `SweepIdleMaxSeconds` | s | 40.0 | [0, 600] | Idle dwell at a sweep node, upper bound. Must be ≥ `SweepIdleMinSeconds`. |
| `AmbushTimeoutSeconds` | s | 40.0 | [1, 600] | Ambush role timeout. |
| `KilltrapSeconds` | s | 10.0 | [0, 600] | Dwell time for a killtrap-style staged point. |
| `RoleTimeoutMinSeconds` | s | 45.0 | [0, 600] | Role assignment timeout, lower bound. |
| `RoleTimeoutMaxSeconds` | s | 120.0 | [0, 600] | Role assignment timeout, upper bound. |
| `ExclusionFirstMin` | m | 8.0 | [0, 1000] | Exclusion margin around targets/objectives before the first completion, min. |
| `ExclusionFirstMax` | m | 21.0 | [0, 1000] | Exclusion margin before the first completion, max. |
| `ExclusionSubsequentMin` | m | 0.0 | [0, 1000] | Exclusion margin for subsequent opportunities, min. |
| `ExclusionSubsequentMax` | m | 16.0 | [0, 1000] | Exclusion margin for subsequent opportunities, max. |
| `IngressAttractMinSeconds` | s | 20.0 | [0, 600] | Ingress-attraction window, lower bound. |
| `IngressAttractMaxSeconds` | s | 20.0 | [0, 600] | Ingress-attraction window, upper bound. |
| `SweepBoxHalfWidth` | m | 3.0 | [0, 100] | Sweep box half width. |
| `SweepBoxMinHalfLength` | m | 16.0 | [0, 1000] | Sweep box minimum half length. |
| `OpportunityExpirySeconds` | s | 30.0 | [1, 600] | How long a macro opportunity remains valid. |

Two hard clamp constants live on `EffectiveConfig.ResolvedPressure`:
`MinFillSeconds = 0.5` (effective fill floor) and `MinSpatialValue = 10.0` (spatial
derived-value floor, per the research's effective-config rule).

## Perception

Micro senses and memory. `MemoryCombineMode`: `Max` (highest confidence wins) or
`WeightedSum` (per-channel weighted sum, clamped to [0,1]).

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `MemoryCapacity` | records | 32 | [1, 256] | Maximum remembered stimuli retained. |
| `CombineMode` | enum | `Max` | `Max`, `WeightedSum` | Combination rule for same-subject memories. |
| `RecentConfirmationSeconds` | s | 0.5 | [0, 60] | Window in which a memory counts as recently confirmed. |

Per-channel fields (`Visual`, `Auditory`, `Touch`, `Damage`, `Light`, `GameDefined`)
each have the same four knobs:

| Field | Units | Range | Controls |
|---|---|---|---|
| `Threshold` | [0,1] | [0, 1] | Activation threshold: a stimulus counts when confidence ≥ this. |
| `DecayHalfLifeSeconds` | s | [0.1, 3600] | Memory confidence half-life. |
| `MaxAgeSeconds` | s | [1, 3600] | Records older than this are forgotten. |
| `Weight` | — | [0, 4] | Channel weight for weighted-sum combination. |

Baseline channel defaults:

| Channel | Threshold | Half-life (s) | Max age (s) | Weight |
|---|---|---|---|---|
| Visual | 0.3 | 30 | 180 | 1.0 |
| Auditory | 0.3 | 15 | 90 | 0.8 |
| Touch | 0.5 | 60 | 300 | 1.0 |
| Damage | 0.5 | 60 | 300 | 1.0 |
| Light | 0.4 | 10 | 60 | 0.6 |
| GameDefined | 0.3 | 20 | 120 | 1.0 |

## Search

Systematic and stimulus-driven search.

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `SystematicWindowSeconds` | s | 20.0 | [1, 600] | Window in which a searched/sensed position still qualifies systematic search. |
| `NodeRevisitPenaltySeconds` | s | 30.0 | [0, 600] | A node searched within this window is skipped. |
| `GiveUpSeconds` | s | 120.0 | [1, 3600] | Give up an unproductive search after this long. |
| `MaxNodesPerSearch` | count | 8 | [1, 64] | Nodes visited per search episode before resting. |
| `NodeReachDistance` | m | 2.0 | [0.1, 100] | Distance at which a searched point counts as reached. |

## Threat

Threat-aware hesitation, flanking, and withdrawal.

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `CloseDistance` | m | 8.0 | [0.1, 1000] | "Close target" route distance. |
| `VeryCloseDistance` | m | 5.0 | [0.1, 1000] | "Very close target" route distance. Must be ≤ `CloseDistance`. |
| `AimedWeaponHesitationSeconds` | s | 0.5 | [0, 60] | Pause when a dangerous target aims at the monster. |
| `VisualRetentionSeconds` | s | 0.5 | [0, 60] | Keep acting on sight this long after losing it. |
| `FlankChance` | [0,1] | 0.2 | [0, 1] | Probability of an ingress flank over a direct approach. |
| `ThreatTimeoutSeconds` | s | 30.0 | [1, 3600] | Threat-aware episode timeout. |
| `DeterrentRetreatSeconds` | s | 2.0 | [0, 600] | Retreat when deterrent exposure persists this long. |
| `DangerousThreatRating` | [0,1] | 0.5 | [0, 1] | Threat rating at/above which a target counts as dangerous. |

## Combat

Chase and attack execution guards.

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `AttackRange` | m | 2.5 | [0.1, 100] | Route distance within which an attack may commit. |
| `ChaseGiveUpDistance` | m | 40.0 | [1, 1000] | Abandon chase beyond this route distance. |
| `ChaseGiveUpSeconds` | s | 8.0 | [0.1, 600] | Abandon chase after losing the target this long. |
| `AttackCooldownSeconds` | s | 1.5 | [0, 600] | Minimum time between attack commits. |
| `FlankIngressSeconds` | s | 20.0 | [0, 600] | Time budget for an ingress-flank manoeuvre. |
| `AttackBanSeconds` | s | 5.0 | [0, 600] | Wait after a failed/rejected attack before retrying. |

## Offstage

Offstage staging, sweep, and ingress policy for the micro layer.

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `IngressBanSeconds` | s | 20.0 | [0, 600] | After using an ingress, ignore it for this long. |
| `NodeDwellMinSeconds` | s | 5.0 | [0, 600] | Dwell at an offstage node, lower bound. |
| `NodeDwellMaxSeconds` | s | 40.0 | [0, 600] | Dwell at an offstage node, upper bound. Must be ≥ min. |
| `PreferIngressNearPressure` | bool | true | — | Prefer ingress points nearer the pressured region over nearer the monster. |
| `KilltrapEnabled` | bool | true | — | Allow killtrap-style staged waiting at egress points. |
| `IngressTimeoutSeconds` | s | 10.0 | [1, 120] | Seconds before an unanswered ingress request counts as failed. |

## Modules

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `Order` | names | empty | module registry names | Arbitration order (earlier wins). Empty = built-in default order. |
| `Disabled` | names | empty | module registry names | Modules force-disabled for this profile. |

Registry names and built-in default order: `Lifecycle`, `ScriptOverride`,
`DamageStun`, `Retreat`, `ThreatResponse`, `Ambush`, `Attack`, `SuspectResponse`,
`HidingTarget`, `Investigate`, `Search`, `Stalk`, `Offstage`, `Idle`. A name in
`Order` that matches no registered module is skipped with a `module_unknown`
telemetry event.

## Movement

| Field | Units | Default | Range | Controls |
|---|---|---|---|---|
| `SpeedSlow` | [0,1] | 0.35 | [0, 1] | Investigate/approach speed scale. |
| `SpeedFast` | [0,1] | 0.7 | [0, 1] | Search/stalk speed scale. |
| `SpeedFastest` | [0,1] | 1.0 | [0, 1] | Chase speed scale. |
| `InvestigateFacingSeconds` | s | 2.0 | [0, 60] | Seconds spent facing a point of interest on arrival. |

## Inheritance

`MonsterProfileConfig.BasedOn` names a single parent profile. Resolution walks the
chain and applies ancestors **root-first** — each profile overrides only the fields
it sets, the child wins per field, and the generic baseline supplies anything no
profile in the chain set. Resolution is deterministic and validated once at
`Resolve()` time.

```csharp
var catalogue = new ProfileCatalogue()
    .Add( new MonsterProfileConfig { Name = "base-stalker", Pressure = new PressureSection { FillSeconds = 3.0 } } )
    .Add( new MonsterProfileConfig { Name = "fast-stalker", BasedOn = "base-stalker", Pressure = new PressureSection { FillSeconds = 1.5 } } );
EffectiveConfig cfg = catalogue.Resolve( "fast-stalker" ); // FillSeconds 1.5, everything else from base-stalker/baseline
```

Errors are startup-fatal with actionable messages: a missing parent names the profile
that referenced it, and an inheritance cycle names the full chain. `ConfigVersion`
(default `"1"`) is carried into `EffectiveConfig` and the saved-state envelope; bump
it when a profile change would invalidate old saves.

## Modifiers

`ProfileCatalogue.ResolveWithModifier(baseName, modifierName)` resolves the base
profile normally, then applies a difficulty/player modifier profile on top. Pressure
fields combine **additively** — the modifier's set values are added to the resolved
base, so a modifier uses small signed deltas (e.g. `FillSeconds = -1.0` for a harder
mode). The additive pressure set is exactly:

`FillSeconds`, `CooldownSeconds`, `DecreaseSeconds`, `MaxOpportunities`,
`SweepMinDistance`, `SweepMaxDistance`, `ExclusionFirstMin`, `ExclusionFirstMax`.

After the merge, clamps mirror the research's effective-config rule:
`FillSeconds ≥ 0.5`, `MaxOpportunities ≥ 1`, and the first-stalk exclusion margins
`≥ 0`. All other pressure fields of the modifier are ignored. Non-pressure sections
of a modifier behave like ordinary overrides (set fields replace resolved values);
a modifier's `Modules` section is not applied. The result is validated like any
profile, so a modifier cannot push a field out of range without an error.

## The AlienIsolationInspired preset

`SboxTwoBrains.AlienIsolationPresets` ships the decoded intensity
configurations as verbatim `AlienIsolationConfigRecord`s plus
`CreateCatalogue()`, which registers one profile per record and the headliner
profile `ALIENISOLATIONINSPIRED` (based on `DEFAULT`, `ConfigVersion`
`"aio-inspired-1"`).

> **Confidence warning.** The values below are **proven decoded data** from the
> research decode (`alienconfigs_decoded.csv`, values inheritance-resolved). Their
> runtime semantics are **strong reconstruction** at best; several fields' exact
> meaning is unresolved. Records are preserved exactly — including pairs the
> research flags as possibly reversed or directional (e.g. `-1`/`-1` role timeouts,
> which ship as a disabled sentinel), and the shipped misspellings
> (`BACSTAGEHOLD_CLOSE`, `MeanceDeemedTime`). This preset is an
> Alien: Isolation-inspired compatibility option, **not exact parity**.

The research decode contains 13 rows; one (`CREWEXPENDABLE_VENT` in the
`ALIENCONFIGS.BML` index) carries no values. The library ships the 12 fully-valued
records below verbatim, and `CreateCatalogue()` adds the headliner profile, for 13
catalogue profiles total.

Primary pacing values (seconds / count), as decoded:

| Name | Template | Fill | Cooldown | Decrease | Max menaces | Sweep duration | Vent attract min–max |
|---|---|---|---|---|---|---|---|
| DEFAULT | — | 3 | 25 | 20 | 4 | 50 | 20–120 |
| MILD | DEFAULT | 2 | 30 | 30 | 3 | 70 | 40–180 |
| MODERATE | DEFAULT | 3 | 25 | 20 | 4 | 50 | 35–120 |
| MODERATELY_INTENSE | DEFAULT | 3 | 20 | 15 | 5 | 45 | 25–120 |
| INTENSE | DEFAULT | 4 | 15 | 10 | 5 | 40 | 20–60 |
| BACKSTAGEALERT | MODERATE | 3 | 25 | 20 | 1 | 50 | 35–120 |
| BACKSTAGEHOLD | MODERATE | 3 | 25 | 20 | 1 | 50 | 35–120 |
| BACKSTAGEHOLD_MILD | MILD | 2 | 30 | 30 | 1 | 70 | 40–180 |
| BACKSTAGEHOLD_VCLOSE | BACKSTAGEHOLD | 3 | 25 | 20 | 1 | 50 | 35–120 |
| BACSTAGEHOLD_CLOSE | BACKSTAGEHOLD | 3 | 25 | 20 | 1 | 50 | 35–120 |
| CANTEEN | MILD | 2 | 30 | 30 | 1 | 70 | 40–180 |
| CREWEXPENDABLE_VENT | INTENSE | 4 | 15 | 10 | 5 | 5 | 20–30 |

Staging values (seconds / metres), as decoded. `-1` is the shipped disabled
sentinel; `ToPressureSection()` maps it to `0`:

| Name | Ambush timeout | Killtrap | Role timeout min–max | Idle min–max | Distance min–max | Sweep box half width | Sweep box min half length |
|---|---|---|---|---|---|---|---|
| DEFAULT | 30 | 40 | 45–60 | 5–10 | 10–40 | 16 | 15 |
| MILD | 30 | 40 | 60–100 | 5–10 | 10–40 | 18 | 18 |
| MODERATE | 30 | 40 | 45–60 | 5–10 | 10–40 | 16 | 14 |
| MODERATELY_INTENSE | 30 | 40 | 50–80 | 5–10 | 10–40 | 14 | 12 |
| INTENSE | 30 | 40 | 25–40 | 5–10 | 10–40 | 12 | 10 |
| BACKSTAGEALERT | 30 | 25 | 200–400 | 5–10 | 5–35 | 16 | 14 |
| BACKSTAGEHOLD | 45 | 90 | -1–-1 | 1–5 | 5–35 | 16 | 14 |
| BACKSTAGEHOLD_MILD | 30 | 90 | -1–-1 | 1–5 | 5–35 | 18 | 18 |
| BACKSTAGEHOLD_VCLOSE | 45 | -1 | -1–-1 | 1–5 | 1–5 | 16 | 14 |
| BACSTAGEHOLD_CLOSE | 45 | -1 | -1–-1 | 1–5 | 5–30 | 16 | 14 |
| CANTEEN | 30 | 40 | 60–100 | 5–10 | 10–40 | 18 | 18 |
| CREWEXPENDABLE_VENT | 30 | 40 | 40–60 | 30–40 | 10–20 | 12 | 10 |

Exclusion radii (metres), as decoded:

| Name | Near-target first min–max | Near-target subsequent min–max | Near-objective first min–max | Near-objective subsequent min–max |
|---|---|---|---|---|
| DEFAULT | 0–15 | 0–10 | 5–15 | 5–10 |
| MILD | 8–29 | 6–24 | 12–29 | 10–24 |
| MODERATE | 0–21 | 0–16 | 8–21 | 8–16 |
| MODERATELY_INTENSE | 0–17 | 0–12 | 6–17 | 6–12 |
| INTENSE | 0–13 | 0–8 | 4–13 | 4–8 |
| BACKSTAGEALERT | 0–21 | 0–16 | 8–21 | 8–16 |
| BACKSTAGEHOLD | 0–21 | 0–16 | 8–21 | 8–16 |
| BACKSTAGEHOLD_MILD | 8–29 | 6–24 | 12–29 | 10–24 |
| BACKSTAGEHOLD_VCLOSE | 0–21 | 0–16 | 8–21 | 8–16 |
| BACSTAGEHOLD_CLOSE | 0–21 | 0–16 | 8–21 | 8–16 |
| CANTEEN | 8–29 | 6–24 | 12–29 | 10–24 |
| CREWEXPENDABLE_VENT | 0–13 | 0–8 | 4–13 | 4–8 |

Field mapping (`ToPressureSection()`): `MenaceGaugeSecondsToFill → FillSeconds`,
`MenaceCoolDownTime → CooldownSeconds`, `MenaceGaugeDecreaseTime → DecreaseSeconds`,
`MaxMenaces → MaxOpportunities`, `IncreaseSweepDuration → SweepDurationSeconds`,
`MinDistance/MaxDistance → SweepMinDistance/SweepMaxDistance`,
`MinIdleTime/MaxIdleTime → SweepIdleMinSeconds/SweepIdleMaxSeconds`,
`AmbushTimeout → AmbushTimeoutSeconds`, `KilltrapTime → KilltrapSeconds`,
`RoleTimeoutMin/Max → RoleTimeoutMinSeconds/RoleTimeoutMaxSeconds`,
near-target radii → `ExclusionFirst*`/`ExclusionSubsequent*`,
`VentAttractTimeMin/Max → IngressAttractMinSeconds/IngressAttractMaxSeconds`,
`SweepBoxHalfWidth`, `SweepBoxMinHalfLength` map to the same-named fields, and
`DecreaseDelaySeconds` is fixed at `0.0`. `MeanceDeemedTime`,
`DecreaseSweepDuration`, and the near-objective radii have no generic equivalent and
stay available on the record itself.

Preset profiles also carry the recovered micro-behavior tuning constants (proven
authored values; module wiring is reconstruction): threat close/very-close distances
8/5 m, aimed-weapon hesitation 0.5 s, visual retention 0.5 s, flank chance 0.2
(the shipped 20/80 branch), threat timeout 30 s, deterrent retreat 2 s, systematic
search window 20 s, investigate facing 2 s.

See also: [architecture](ARCHITECTURE.md) · [API map](API.md) · [getting started](GETTING_STARTED.md) · [tuning](TUNING.md) · [evidence](EVIDENCE.md) · [tick order](TICK_ORDER.md)
