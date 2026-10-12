# Agent Guidelines

## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical triage label vocabulary. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`GLOSSARY.md` + `docs/adr/`). See `docs/agents/domain.md`.

### GOAP / Utility AI

Before adding or changing AI code (`Assets/Scripts/GOAP/`: actions, goals, sensors, keys, utility scorers, SensorToolkit wiring), read `docs/goap_utility_context.md`. Its **Rules** section is binding. Its boilerplates are the templates to start from.

### Unity Automation

Always use the **Unity CLI** (`unity` / `C:\Users\Admin\AppData\Local\Unity\bin\unity.exe`) for Unity Editor automation, package management, testing, and project inspection instead of Unity MCP.
