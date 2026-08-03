# AGENTS.md — two-brain-director

Deterministic, engine-independent two-layer monster AI for s&box. Read `docs/PLAN.md` and
`docs/EVIDENCE_MATRIX.md` before changing anything.

## Non-negotiable core rules (Code/TwoBrainsCore)

1. Pure C#, zero engine references. No `Sandbox.*`, no `Vector3` (use `TwoBrains.Core.Contract.Vec3`).
2. Explicit `using` directives in every file — implicit usings are DISABLED.
3. Banned APIs (s&box SB500 whitelist): `[GeneratedRegex]`, `ZLibStream`, `Environment.NewLine`,
   `OverflowException`, `InvalidDataException`, `Type.IsPrimitive`, `Array.Clone()`.
   `System.Text.Json`, `System.Numerics`, collections, LINQ are allowed.
4. Determinism: `double` math with + − * / and `Math.Sqrt` only — no transcendental functions.
   Never read wall clock. Never use `System.Random` (use `Determinism.DeterministicRng`).
   No static mutable state anywhere in the core.
5. Macro output never contains target coordinates or movement instructions.
6. Game-specific names (menace/backstage/vent/AlienConfig templates) appear ONLY under
   `TwoBrains.Core.Compat` and preset docs.
7. Every state transition emits a reason code into telemetry.
8. Namespace root `TwoBrains.Core`; file-scoped namespaces; one public type per file.

## Workflow

- Build/test: `dotnet test dev/SboxTwoBrains.Tests` (compiles the shared core sources).
- Offline s&box compile check: `dotnet build dev/offline-check`.
- Authoritative s&box compile: `powershell dev/editor-rig/run_editor_gate.ps1` (needs Steam).
- Do not commit build artifacts. Do not copy files from `reference/` into the library —
  it is proprietary research material used only to derive constants.
