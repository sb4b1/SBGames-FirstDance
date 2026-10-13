# Agent Guidelines

## Agent skills

### Project overview

High-level architecture, game loop, directory layout, and binding rules. See `docs/agents/project_overview.md`.

### Engineering guide

Code-level technical architecture, component lifecycles, data flow, and feature extension playbooks. See `docs/engineering_guide.md`.

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
The Unity Editor is always kept running by the user: prioritize pipeline commands (`unity cmd console`, `unity cmd eval`, `unity cmd recompile`, etc.) for reading logs and interacting with the editor.
