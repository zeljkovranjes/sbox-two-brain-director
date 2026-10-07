# Two-Brain Director

Journal for this library. Keep it current: decisions, engine gotchas, what failed and why, the next step.

## Package

| | |
|---|---|
| Ident | `notpointless.two_brain_director` |
| Type | library |
| Root namespace | `SboxTwoBrains` |
| Depends on | none (used by the two-brain-director-demo game project) |
| Published | sbox.game, 1.0.0 on 2026-08-03 (repo release 1.0.1 the same day) |

## Log

- 2026-08-02..03: built and released 1.0.0 and 1.0.1 (history in git and CHANGELOG; the original plan
  is docs/PLAN.md, the verification report docs/VERIFICATION.md).
- 2026-10-06: brought under the workspace standard. Core (everything the tests already compiled without
  the engine) moved to Code/SboxTwoBrains/Core as `SboxTwoBrains.Core`, kept flat (one feature, and
  sub-folders would have meant more namespaces for users to import). The `Host` adapter split into
  Components/Engine/UI with `[Alias]` to the old names; multi-type files split one type per file, no code
  changed. Tests compile `Core/**` only, not `Assembly.cs`, so Core files keep explicit usings. Baseline
  and final: 181/181 tests, 15/15 examples, Code/Editor/offline-check build clean (dev/out/baseline.txt).
  The editor gate (real s&box compile, whitelist) was not re-run.
  Next: re-run dev/editor-rig/run_editor_gate.ps1, and update the two-brain-director-demo project's
  `using` lines (SboxTwoBrains -> SboxTwoBrains.Core, SboxTwoBrains.Host -> .Components/.Engine/.UI).
