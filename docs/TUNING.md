# Tuning

Defaults are the clean-room generic baseline — playable, not balanced for your game.
Tune with a fixed seed and a recorded snapshot sequence so every comparison is
reproducible; watch `TelemetryEvent` codes (`candidate_latched`,
`mode_aggressive_start`, `opportunity_completed`, `quota_blocked`, ...) rather than
eyeballing behavior. Change one section at a time: pressure pacing first, then
perception, then behavior guards. Field names, ranges, and defaults referenced below
are in [CONFIG_REFERENCE.md](CONFIG_REFERENCE.md).

## Pressure pacing

The gauge fills asymptotically: `(1 − p) / FillSeconds · dt`. Early progression is
fast, the approach to the threshold slow — so `FillSeconds` behaves more like a
time-to-mostly-full than a linear timer.

| Knob | Raise it → | Lower it → |
|---|---|---|
| `FillSeconds` | Longer build-up before each opportunity. | Hair-trigger pacing; the monster reacts almost immediately. |
| `CooldownSeconds` | Longer relief after each opportunity; breathing room. | Relentless pressure; can feel unfair. |
| `DecreaseSeconds` / `DecreaseDelaySeconds` | Progress decays slowly / after a grace when the player escapes. | Tension collapses quickly once lost. |
| `MaxOpportunities` | More aggressive cycles before the quota blocks. | Fewer set-pieces per session. |
| `EventQuotaMin/Max` | Wider spread of the random event-quota target. | Tighter, more predictable reset cadence (0 disables). |
| `AggressiveThresholdProgression` | Must be near-full to fire (1.0 = full). | Fires early, at partial charge. |
| `SweepDurationSeconds` | Longer aggressive windows. | Short, sharp appearances. |
| `ExclusionFirst*` / `ExclusionSubsequent*` | Staging kept further from protected targets/objectives. | Staging allowed closer in. |
| `IngressAttractMin/MaxSeconds` | Longer windows in which ingress points stay attractive. | Brief windows. |

Watch for `quota_blocked` in telemetry: if it appears while you still expect
aggressive behavior, `MaxOpportunities` is the cap you hit.

## Perception

Per channel (`Visual`, `Auditory`, `Touch`, `Damage`, `Light`, `GameDefined`):

| Knob | Raise it → | Lower it → |
|---|---|---|
| `Threshold` | Ignores weak evidence; only confident stimuli register. | Jumpy; reacts to faint hints. |
| `DecayHalfLifeSeconds` | Long memory; old clues still guide search. | Forgets quickly; loses the trail. |
| `MaxAgeSeconds` | Hard memory horizon extends. | Records dropped sooner regardless of confidence. |
| `Weight` | Channel dominates `WeightedSum` combination. | Channel discounted. |

Decay is linear, not exponential: an unconfirmed memory loses
`BaseConfidence / (2 × DecayHalfLifeSeconds)` per second, reaching zero in two
half-lives. A recently confirmed memory at/above threshold also keeps its sense
active for `RecentConfirmationSeconds` after the stimulus stops (the active latch).

Global: `MemoryCapacity` bounds retained records (evicts under pressure),
`RecentConfirmationSeconds` defines "fresh" evidence, `CombineMode` picks `Max`
(one strong memory dominates) or `WeightedSum` (several weak ones can add up). A
monster that feels psychic usually has thresholds too low or half-lives too long on
`Auditory`; one that feels blind usually has `Visual.Threshold` too high for the
confidence values your host actually reports. Check what confidence your snapshot
builder emits before tuning thresholds.

## Search, threat, combat, offstage

- **Search.** `SystematicWindowSeconds` gates when a clue still justifies a
  systematic sweep; `NodeRevisitPenaltySeconds` prevents re-treading;
  `GiveUpSeconds` and `MaxNodesPerSearch` bound the episode. Raise revisit penalty
  and node count for methodical hunters; lower `GiveUpSeconds` for easily
  distracted ones.
- **Threat.** `DangerousThreatRating` decides who counts as dangerous;
  `CloseDistance`/`VeryCloseDistance` set the reaction bands;
  `AimedWeaponHesitationSeconds` and `VisualRetentionSeconds` shape hesitation;
  `FlankChance` controls ingress flanking over direct approach;
  `DeterrentRetreatSeconds` how much deterrent exposure forces withdrawal.
- **Combat.** `AttackRange` is the commit distance (route distance, not straight
  line); `ChaseGiveUpDistance`/`ChaseGiveUpSeconds` end lost pursuits;
  `AttackCooldownSeconds` paces repeated commits; `AttackBanSeconds` punishes failed
  attempts with a retry delay — raise it to stop attack spam when the host rejects
  often.
- **Offstage.** `NodeDwellMin/MaxSeconds` set how long the monster lingers offstage;
  `IngressBanSeconds` prevents immediate re-use of the same point;
  `PreferIngressNearPressure` biases ingress choice toward the pressured region;
  `KilltrapEnabled` allows staged waiting at egress points;
  `IngressTimeoutSeconds` bounds unanswered traversal requests.
- **Movement.** `SpeedSlow`/`SpeedFast`/`SpeedFastest` are host-relative scales —
  they only work if your executor maps them to distinct locomotion speeds.

## Archetype sketches

Three archetypes configured with zero core changes, sharing one base via `BasedOn`:

```csharp
var catalogue = new ProfileCatalogue()
    .Add( new MonsterProfileConfig { Name = "stalker" } )   // generic baseline as the shared root
    .Add( new MonsterProfileConfig
    {
        Name    = "brute",
        BasedOn = "stalker",
        // Fast cycles, no subtlety: charges in, shrugs off deterrence.
        Pressure = new PressureSection { FillSeconds = 2.0, CooldownSeconds = 15.0, MaxOpportunities = 6 },
        Threat   = new ThreatSection { FlankChance = 0.0, AimedWeaponHesitationSeconds = 0.0, DeterrentRetreatSeconds = 6.0 },
        Combat   = new CombatSection { AttackRange = 3.0, ChaseGiveUpDistance = 60.0, AttackBanSeconds = 2.0 },
    } )
    .Add( new MonsterProfileConfig
    {
        Name    = "lurker",
        BasedOn = "stalker",
        // Slow cycles, long memory, lives offstage.
        Pressure   = new PressureSection { FillSeconds = 5.0, CooldownSeconds = 40.0, SweepIdleMinSeconds = 15.0, SweepIdleMaxSeconds = 60.0 },
        Perception = new PerceptionSection
        {
            Auditory = new PerceptionChannelSection { Threshold = 0.2, DecayHalfLifeSeconds = 30.0 },
        },
        Offstage = new OffstageSection { NodeDwellMinSeconds = 20.0, NodeDwellMaxSeconds = 90.0, KilltrapEnabled = true },
    } );
```

Authoring rules: single `BasedOn` chain, root-first resolution, child wins per
field, null fields inherit, cycles and missing parents are startup errors. Keep each
profile's delta small — a profile that overrides everything hides which change
caused which behavior. Full inheritance and modifier rules:
[CONFIG_REFERENCE.md](CONFIG_REFERENCE.md).

## Keeping determinism while tuning

- Iterate against a fixed seed and a fixed tick rate; record snapshots and acks for
  regressions you care about. Two runs must agree on `DecisionBatch.StateHash`
  every tick.
- After changing a profile, resolve it and diff `EffectiveConfig.Describe()` — it
  prints every effective value sorted, so config drift is visible in code review.
- Treat `ComputeHash()` as the profile's identity: if a tuning change must not
  invalidate old saves, the hash (and `ConfigVersion`) tells you it did.
- Modifiers are additive deltas on pressure fields, not replacements; tune the base
  profile first, then express difficulty as small offsets.

## Common pitfalls

- **Wall clock.** `DateTime`/`Stopwatch` in host-fed data breaks replay. Time enters
  only via `TickIndex` + `DeltaTimeSeconds`.
- **`System.Random`.** Never, in policy-adjacent code. Use the `DeterministicRng`
  forks; a hidden random source desynchronizes save/restore.
- **Transcendental math.** The core uses `+ − * /` and `Math.Sqrt` on `double` only.
  Keep `Math.Sin`/`Cos`/`Pow`/`Atan2` and `float` out of anything that feeds policy
  decisions if you need cross-platform bit equality.
- **Mutating snapshots.** Build each `WorldSnapshot` fresh per tick and never touch
  it after `Tick` returns; the core treats it as immutable input.
- **Skipping acknowledgements.** Unacknowledged requests and opportunities pin
  module state and expire awkwardly. Always answer — `Rejected` with a `Detail` is a
  valid, useful answer.
- **Tick gaps.** Snapshots must be strictly sequential. Save/restore continues at
  `NextTickIndex`; do not restart numbering after loading.
- **Tuning against the wrong truth.** Thresholds compare against your host's
  reported `Confidence`; distances compare against your host's route distances.
  Calibrate the snapshot builder before calibrating the profile.

See also: [architecture](ARCHITECTURE.md) · [API map](API.md) · [configuration reference](CONFIG_REFERENCE.md) · [getting started](GETTING_STARTED.md) · [evidence](EVIDENCE.md) · [tick order](TICK_ORDER.md)
