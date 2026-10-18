# Agent Guidelines — SBGames

2D dungeon-defense / auto-battler RPG in Unity 6 (`6000.6.4f1`). This file is only the entry point; the deep docs below are the source of truth.

## Communication
- Respond in English only, regardless of the language of prompts, comments, or docs (`GEMINI.md`).

## Read first
- `docs/agents/project_overview.md` — game loop, subsystems, directory layout, binding rules.
- `docs/engineering_guide.md` — architecture patterns, coding invariants (§4), feature playbooks (§5), known quirks (§6).
- `docs/goap_utility_context.md` — GOAP + Utility AI; its **Rules** section is binding and its boilerplates are the templates to start from.
- `GLOSSARY.md` + `docs/adr/` — canonical domain vocabulary and decisions (`docs/agents/domain.md`).
- `docs/agents/issue-tracker.md` + `docs/agents/triage-labels.md` — issues/specs are GitHub Issues, driven with `gh`.
- `docs/project_status_snapshot.md` — latest progress; regenerate with the `project-snapshot` skill.

## Paths: trust the filesystem, not the docs
Earlier docs (including `goap_utility_context.md`'s relative file links) predate a code reorganization and point at folders that no longer exist. Current locations:
- Characters/combat: `Assets/Scripts/Character/` (docs say `Characters/`).
- GOAP + Utility AI: `Assets/Scripts/Character/AI/GOAP/` (docs say `Assets/Scripts/GOAP/`).
- Runtime tests: `Assets/Tests/` (NUnit, namespace `SBGames.Tests`) — not under `Assets/Scripts/`.
- Editor tooling: `Assets/Editor/` and `Assets/Scripts/Editor/`. Data tables: `Assets/DataTables/`.

## Unity automation (mandatory)
Use the Unity CLI (`unity`, or `C:\Users\Admin\AppData\Local\Unity\bin\unity.exe`); never Unity MCP. The Editor is usually left running, so prefer live pipeline commands:
- `unity status` — confirm an Editor is connected before issuing other pipeline commands.
- `unity cmd <name>` — run Editor-registered pipeline commands (e.g. `unity cmd console`, `unity cmd eval`); bare `unity cmd` lists them.
- `unity recompile` — recompile and report compile errors.
- `unity test --mode EditMode --filter <TestName>` — run tests (`EditMode` or `PlayMode`); writes an NUnit report to `test-results.xml`.
- Batch fallback: `unity -quit -batchmode -projectPath "D:\UnityProject\SBGames" -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation`.

## Coding invariants agents most often miss
Full set in `docs/engineering_guide.md` §4; the commonly violated ones:
- Never mutate ScriptableObjects at runtime — keep mutable state on component managers.
- Damage only through `IDamageable.TakeDamage(in DamageData)`; never deduct health directly.
- Hot paths (`Update`, `FixedUpdate`, GOAP `Sense`/`Perform`, Utility `Evaluate`) must be zero-alloc: no LINQ, no per-call lambdas, `sqrMagnitude`, `NonAlloc` physics.
- GOAP actions only act — movement is owned by `AgentMoveBehaviour`; inject controllers with `[GetComponentInParent]`.
- No inline TODO/FIXME comments — track work in GitHub Issues.

Note: `docs/engineering_guide.md` §6.3's list of misspelled files is stale (those were since renamed); verify names on disk before trusting it.

## Build layout
- Game code compiles into `Assembly-CSharp`; the only project asmdefs ship inside `SensorToolkit`. Adding an asmdef changes compile boundaries, so don't add one casually.
