---
name: project-snapshot
description: Scan the SBGames Unity codebase (Assets/Scripts/ and docs/) to generate or update docs/project_status_snapshot.md for syncing project progress with external AI architects.
---

# Project Progress & Architecture Snapshot Skill

When invoked, scan the current codebase and documentation in the workspace and generate a comprehensive, up-to-date technical summary saved directly to `docs/project_status_snapshot.md`.

## Steps to Execute

1. **Check Git & Recent Changes:**
   - Run `git status -s` and `git log -n 10 --oneline --stat` to identify recently added or modified scripts, ScriptableObjects, and tests.
2. **Scan Codebase Inventory:**
   - Inspect `Assets/Scripts/` subdirectories (`GOAP/`, `Characters/`, `Squads/`, `Loop/`, `Inventory/`, `Placement/`, `Shop/`, `Skills/`, `Stat/`, `Personality/`, `Testing/`, etc.).
   - Check `docs/` for any phase completion docs, layout contracts, or issue trackers.
3. **Generate/Overwrite `docs/project_status_snapshot.md`:**
   Write the file using the exact structure below:

```markdown
# SBGames — Project Progress & Architecture Snapshot
*Last Updated: [Current Date/Time] | Latest Commit: [Short Git Hash & Message]*

## 1. Recently Completed Milestones & Features
- Summarize the latest completed phases or systems (e.g., Adventurer AI Phases, GOAP goals/actions, UI Toolkit controllers, tests).
- List newly created ScriptableObject assets or Editor tools.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
- Group by folder under `Assets/Scripts/`.
- Highlight newly added or refactored classes, interfaces, and structs, along with their core responsibilities and key public methods/properties.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI:** List active `GoalBase`, `GoapActionBase`, `UtilityGoal`, `WorldKey`, `TargetKey`, and `CapabilityFactory` registrations.
- **Core Systems & Services:** Note any updates to `BaseAI`, `PartyLeader`, `SquadBlackboard`, `CharacterAIMemory`, `GameServices`, or `CoreGameLoop`.
- **Event Hooks:** List active C# events/actions used for cross-system communication (e.g., DM Seal, Battle Report, Altar Events).

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:** List any pending prefab wiring, ScriptableObject assignments, or scene setups required in Unity Editor.
- **Known Gotchas / Constraints:** Note any active constraints or deferred systems waiting to be implemented next.