# Evidence and provenance

This library is a clean-room behavioral implementation inspired by the two-brains
architecture of Alien: Isolation. It contains no Creative Assembly code or assets.
Everything research-derived comes from a private research repository
(`reference/aio-research`, gitignored and never redistributed) used only to derive
constants and structure. The full claim-by-claim mapping — 26 rows, each with source,
confidence label, and consuming component — is
[EVIDENCE_MATRIX.md](EVIDENCE_MATRIX.md). This file is the short version.

## Confidence model

| Label | Meaning | Where it may be used |
|---|---|---|
| **Proven** | Established by decoded shipped data, binary strings/call sites, or runtime traces. | May define `AlienIsolationInspired` preset defaults. |
| **Strong reconstruction** | Structure is proven; wiring or exact runtime use is reconstructed. | Configurable policy, never presented as exact. |
| **Inferred** | Plausible reading of the evidence without direct proof. | Configurable policy, labelled inferred. |
| **Clean-room** | Original design with no recovered-source claim. | Architecture and engineering only. |

Enforcement rules, everywhere: proven facts may shape the preset; inferred behavior
stays configurable; game-specific names (menace, backstage, vent, AlienConfig
template names) appear only under `TwoBrains.Core.Compat` and in preset
documentation.

## What is proven decoded data

- **The preset's numeric values.** The 12 fully-valued intensity records
  (`DEFAULT`, `MILD`, `MODERATE`, `MODERATELY_INTENSE`, `INTENSE`, the
  `BACKSTAGE*` family, `CANTEEN`, `CREWEXPENDABLE_VENT`) are verbatim from the
  machine-readable decode: fill/cooldown/decrease times, max menaces, sweep
  durations and box geometry, idle and distance bounds, exclusion radii, role
  timeouts, killtrap and ambush timers, vent-attract windows. Shipped misspellings
  and `-1` disabled sentinels are preserved (see
  [CONFIG_REFERENCE.md](CONFIG_REFERENCE.md#the-alienisolationinspired-preset)).
- **The macro gauge shape.** A fill-toward-opportunity gauge with
  seconds-to-fill / cool-down / decrease-time / max-menaces parameters, the
  `(1 − p) / fill` increment form, the Normal↔Aggressive mode cycle, scripted
  set/reset entry points, and an enforced opportunity quota.
- **Micro structure.** A modular micro brain with ordered selection, typed sense
  channels with thresholded activation, threat-aware distances and timers,
  staged suspicious-item investigation, a systematic-search window, and authored
  constants the preset carries (threat distances, 0.5 s retention, 20/80 branch,
  20 s search window, 2 s facing).

## What is configurable reconstruction

- **The macro → micro data flow.** Exactly what the director handed the creature is
  unresolved in the research. This library's choice — region ids, roles, constraints,
  and expiry, never coordinates — is a clean-room decision enforced by the contract.
- **Module wiring.** Gate constants are proven authored data; how modules compose
  (ordering, fallthrough, recovery paths) is a reconstruction and stays per-profile
  configurable (`Modules.Order`, `Modules.Disabled`).
- **Sweep, exclusion, and ingress semantics.** The config fields are proven to
  exist; their precise runtime meaning is strong reconstruction at best, so the
  library exposes them as explicit policy over host-supplied world data.
- **Urgency, candidate scoring, hysteresis.** Clean-room derivations, documented
  in code and [ARCHITECTURE.md](ARCHITECTURE.md).
- **Everything in the generic baseline.** All defaults outside the preset are
  clean-room starting values with no recovered-source claim.

## Non-goals

Carried from the research and enforced by design:

- **No source parity.** No byte-identical or source-identical reconstruction, no
  copied code or assets, no exact-parity claims for the preset.
- **No omniscience by default.** The micro decides from its own perception memory;
  perfect target knowledge exists only as the explicit, telemetry-visible
  `WorldSnapshot.OmniscientTargets` switch.
- **No teleport.** Offstage repositioning happens only through host-approved
  `IngressPoint` edges, each use logged and acknowledged. There is no universal
  tether distance and no unscripted arbitrary relocation.
- **No learning.** Adaptation is authored gauges, probabilities, timers, and flags;
  there is no ML and no persistent player profiling.

See also: [architecture](ARCHITECTURE.md) · [API map](API.md) · [configuration reference](CONFIG_REFERENCE.md) · [getting started](GETTING_STARTED.md) · [tuning](TUNING.md) · [tick order](TICK_ORDER.md) · [evidence matrix](EVIDENCE_MATRIX.md)
