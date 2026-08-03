# Evidence-to-Component Matrix

Maps every claim in `reference/aio-research` to a component of this library, with the
research confidence label preserved. Rules enforced everywhere:

- **Proven / High** → may define the `AlienIsolationInspired` compatibility preset defaults.
- **Strong reconstruction / Inferred** → configurable policy, never presented as exact.
- **Clean-room design** → architecture guidance only; no recovered proprietary source is copied.
- Game-specific names (menace, backstage, vent-attract, AlienConfig template names) appear
  **only** under `TwoBrains.Core.Compat` and in preset docs — never in the generic API.

| # | Research claim (source) | Confidence | Library component | How it is used |
|---|---|---|---|---|
| 1 | Dedicated macro pacing manager exists (`menace_manager.cpp` string, setters, resets) — README, ARCHITECTURE, BINARY_REVERSAL | Proven | `TwoBrains.Core.Macro.PressureDirector` | Justifies a separate macro controller type. Generic name; "menace" only in preset reason codes. |
| 2 | Gauge fills over time toward an opportunity; params `menace_gauge_seconds_to_fill`, `menace_cool_down_time`, `menace_gauge_decrease_time`, `max_menaces` (BML + exe) | Proven | `Macro/PressureGauge`, `PressureConfig` | `FillSeconds`, `CooldownSeconds`, `DecreaseSeconds`, `MaxOpportunities` in config; units documented. |
| 3 | Fixed-update increment `(1 - progression) / fill_seconds * (1/60)` — BINARY_REVERSAL §fixed update | Proven (for the recovered build, 60 Hz) | `Macro/PressureFormula` | Core uses `(1 - p) / max(fill, MinFillSeconds) * dt`. Compat preset documents `dt = 1/60` equivalence; core never hard-codes 1/60. |
| 4 | Effective config = base + difficulty/player modifier, clamped (fill ≥ 0.5, max ≥ 1, spatial ≥ 10) — BINARY_REVERSAL §getters | Proven | `Config/EffectivePressureConfig` resolver | `Resolve(base, modifier)` adds then clamps with the same minima; clamps are named constants. |
| 5 | Mode cycle Normal ↔ Aggressive; aggressive completion increments count, returns to normal — BINARY_REVERSAL + runtime trace | Proven | `Macro/PressureMode`, mode state machine | Two-state machine, transitions emit reason codes; count increments only on aggressive completion. |
| 6 | Script setters `SetAggressive(progression, reset)` / `SetNormal(progression, reset)`; property names `AggressiveMenace`, `ResetMenaceGauge`, `ProgressionFraction` | Proven | `ScriptDirective` handling in `PressureDirector` | Generic directives `SetMode`, `SetProgression`, `Reset`; recorded as explicit overrides in telemetry. |
| 7 | `max_menaces` is an enforced quota with event consequence; event quota progress/target bytes +0x1F6/+0x1F7 | Proven | `OpportunityQuota` in macro state | `MaxOpportunities` blocks new aggressive cycles; optional `EventQuotaTarget` (seeded-random in range) triggers reset event. |
| 8 | Reset/start helper clears count and latches, starts aggressive at progression 1.0 or 0.0 | Proven | `PressureDirector.Reset` | Deterministic reset; start progression is config, not hidden state. |
| 9 | Intensity templates MILD…INTENSE change fill/cooldown/decrease/max/sweep/vent-attract — CONFIG_REFERENCE | Proven (values), semantics of some fields unresolved | `Compat/AlienIsolationPresets` | 13 shipped configs imported verbatim from `alienconfigs_decoded.csv`; reversed min/max pairs preserved, flagged in docs. |
| 10 | Exclusion radii near target/objective, first/subsequent stalk; sweep box geometry; role timeouts; backstage sweep distance/idle/ambush/killtrap controls | Proven (exist as config), exact runtime use is strong reconstruction | `Macro/ExclusionRules`, `Macro/SweepPlanner`, config fields | Configurable spatial filters on candidate regions/ingress; labelled inferred where semantics uncertain. |
| 11 | Backstage nodes, area sweeps, vent traversal are first-class; no proof of arbitrary teleport — CATHODE_INTEGRATION, SYSTEM_DIAGRAMS | Proven (authored), teleport absent | `Contract OffstageArea/IngressPoint`, `Micro/OffstageModule` | Offstage moves only via host-approved ingress edges; every transition logged with reason + host result. |
| 12 | Micro layer = modular behavior tree; `alien_behave` linear selector; earlier children first — MICRO_BRAIN | Proven (structure) | `Micro/MonsterAgent` ordered module arbitration | Deterministic priority list of guarded modules; order configurable per profile. |
| 13 | Motivation gates (attack 1, despawn 2, sys. search 5, stalk 6, backstage stalk 7, shot 8, suspect 9, threat-aware 10, susp. item 22, stun 24, breakout 25, hide 26, ambush 29) — MICRO_BINARY_REVERSAL | Proven | `Micro/Motivation` flags + module gates | Generic motivation enum set by macro/local conditions; numeric ids only in preset docs. |
| 14 | Sense channels: visual, flashlight, touch, combined, heard-combat, heard-movement, damaged, flamethrower; thresholded activation `value >= threshold` + active latch | Proven | `Micro/Perception` (`SenseChannel`, activation, latch) | Generic channels Visual/Auditory/Touch/Damage/Light/GameDefined; per-channel threshold + decay config. |
| 15 | Threat-aware subtree: distance tests 20/8/5/14/12/9.5, 0.5 s recent-visual, 20/80 % branches, 30/25/120 s timers — MICRO_BRAIN | Proven (authored constants), branch wiring = reconstruction | `Micro/ThreatResponseModule` + preset values | Constants live in preset; module logic is clean-room utility over distance/threat/visual-retention. |
| 16 | Suspicious-item staged path: react → approach(15) → close react(8) → wait/inspect (2 s facing) → search | Proven (structure + numbers) | `Micro/InvestigateModule` stages | Staged investigation with configurable distances/timers; preset carries the numbers. |
| 17 | Systematic search precondition: searched position or sensing/search within 20 s window; search consumes region + history, not exact target | Proven (structure) | `Micro/SearchModule` | Search operates on region/memory; no omniscient coordinates. |
| 18 | Attack = precondition tree + core tree; inputs visual/touched/flashlight/heard-combat/combined/damaged; chase/grapple/vent-flank guarded by timers + feasibility | Proven (structure) | `Micro/AttackModule` (precondition + execution), `Micro/ChaseModule` | Split eligibility vs execution; all actions check reachability, distance, cooldowns, flags, host acks. |
| 19 | Retreat and stun-damage gauges exist in XML | Proven (exist), tuning = inferred | `Micro/RetreatModule`, `Micro/DamageStunModule` | Generic gauges with configurable thresholds. |
| 20 | Vents: vent-attract/vent-ban timers; `ALIEN_ALWAYS_KNOWS_WHEN_IN_VENT` flag; distance tests 1.5/2/4 | Proven (structure) | `Contract IngressPoint`, `Micro/OffstageModule` ingress policy | Ingress cooldowns/attract windows configurable; flag becomes omniscience policy toggle (off by default). |
| 21 | Pathfinding failure handling, target switching, suspension, reset precede motivations | Proven (order) | `Micro/LifecycleModule`, failure recovery | First-priority module; failed routes feed timers/flags/alternative selection. |
| 22 | Macro biases, micro resolves locally; no frame-by-frame director movement commands; imperfect information preserved | Strong reconstruction | Whole architecture: `PressureDecision` carries region/roles/constraints/expiry, never target coordinates | Enforced by contract: macro output has no target position field. |
| 23 | Director↔alien precise data flow (which region/coordinate passed) | **Unresolved** | `PressureDecision.CandidateRegion` is a region id only | Documented as open; library chooses region-level bias (clean-room decision). |
| 24 | 13 Cathode `NPC_SetupMenaceManager` setups (e.g. M10 0.5/aggressive, M32 0.7, CM16 0.4/0.8); 89 `NPC_AlienConfig` switches; BackstagePlayerDetection transitions BACKSTAGEHOLD↔MODERATE↔BACKSTAGEALERT | Proven | `Compat/AlienIsolationPresets` + scenario tests | Preset catalogue + scripted-encounter test scenarios mirror these progressions. |
| 25 | Backstage marker nodes ~ −4.25/−6/−9 m, extra_cost 1, open_on_reset true, flamethrower cancel 20 | Proven (authored data) | Preset offstage node metadata example | Used only in example/preset data, not core. |
| 26 | No ML / persistent player profiling; adaptation = gauges/probabilities/timers/flags | Proven (absence of evidence, stated as limit) | — | Library implements authored adaptation only; documented. |

## Explicit non-goals carried from the research

- No byte-identical or source-identical reconstruction; no copied game code/assets.
- No universal "hard tether" distance; no unscripted arbitrary teleport; no omniscient
  micro target location unless an explicit, telemetry-visible omniscience policy is enabled.
