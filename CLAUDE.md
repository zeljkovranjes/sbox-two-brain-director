<!-- sbox-standard: v1 | type: library | root: SboxTwoBrains -->
# Two-Brain Director

**Standard: sbox-standard v1** (library, root namespace `SboxTwoBrains`). This package follows the package layout and
code standard in E:\.sbox\CLAUDE.md (loaded automatically), enforced by `sbox-check`. Run it before
calling work done.

s&box library `notpointless.two_brain_director`.

## Package facts

Read `docs/ARCHITECTURE.md` and `docs/EVIDENCE_MATRIX.md` before changing behaviour.

Core rules (`Code/SboxTwoBrains/Core`):
- Shared source: `tests/SboxTwoBrains.Tests` and `dev/examples` compile `Core/**/*.cs` directly with
  implicit usings off and without `Assembly.cs`. Every Core file keeps its own explicit `using` lines.
- Core uses its own `Vec3` (metres), never an engine vector. `Engine/SandboxVec` converts to s&box
  units (1 unit = 0.0254 m) at the boundary.
- Whitelist (SB500) breaks found here, all banned: `[GeneratedRegex]`, `ZLibStream`,
  `Environment.NewLine`, `OverflowException`, `InvalidDataException`, `Type.IsPrimitive`,
  `Array.Clone()`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. `System.Text.Json`, LINQ and
  collections are fine.
- Determinism (the replay contract): `double` math with + - * / and `Math.Sqrt` only, no
  transcendental functions, no wall clock, no `System.Random` (use `DeterministicRng`), no static
  mutable state.
- Macro output never contains target coordinates or movement instructions.
- Game-specific names (menace, backstage, vent, AlienConfig template names) and recovered constants
  appear only in the compat preset (`AlienIsolationPresets`, `AlienIsolationConfigRecord`) and preset docs.
- Every state transition emits a reason code (`ReasonCodes`) into telemetry.

Engine gotchas:
- No namespace segment named `Sandbox` (the adapter once was `SboxTwoBrains.Sandbox` and broke the
  in-engine `Sandbox.Internal` globals; it then became `SboxTwoBrains.Host`, now split into
  Components/Engine/UI with `[Alias]` to the `Host` names).
- `TwoBrainsComponent.System` shadows the `System` namespace inside that class: write `global::System...`.

Checks:
- `dotnet test tests/SboxTwoBrains.Tests` (181 tests), `dotnet run --project dev/examples/TwoBrains.Examples.csproj` (15 self-checking examples).
- `dotnet build dev/offline-check/OfflineCheck.csproj -p:SboxRoot=<sbox install>`: fast compile of Code + Editor
  against the engine DLLs (its default root is the original author's `D:\SteamLibrary\...`).
- Authoritative: `dev/editor-rig/run_editor_gate.ps1` boots the real editor (needs Steam and a desktop);
  `Editor/CompileGate.cs` is its in-editor half. `dev/run_all.ps1` runs everything.

Research: `docs/research/aio-research` is a gitignored clone of proprietary research material, used only
to derive constants. Never copy files from it into the library, never build its tools.
