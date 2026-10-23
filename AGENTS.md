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

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **SBGames-FirstDance** (14134 symbols, 32328 relationships, 751 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`; report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- MUST warn on HIGH/CRITICAL `risk` pre-edit; never use `riskSharedAxes` to waive a HIGH/CRITICAL `risk` warning. Compare File/symbol: MCP File omits axes; Graph-RAG expands File.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- **MUST use `query({search_query: "concept"})` for concepts/flows, `context({name: "symbolName"})` for a named symbol, or `impact` for blast radius, on read-only callers, dependencies, imports, or execution flow.** Graph first; text search only for empty/`UNKNOWN`/literals.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/SBGames-FirstDance/context` | Codebase overview, check index freshness |
| `gitnexus://repo/SBGames-FirstDance/clusters` | All functional areas |
| `gitnexus://repo/SBGames-FirstDance/processes` | All execution flows |
| `gitnexus://repo/SBGames-FirstDance/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
