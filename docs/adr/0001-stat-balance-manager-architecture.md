# 1. Stat Balance Manager Architecture

Date: 2026-10-09

## Context
Balancing RPG stats across Characters, Items, and Skills requires comparing and adjusting numeric attributes simultaneously. The project previously relied on individual ScriptableObject inspectors or full creation wizards (`SBGamesCreatorWindow`), with a secondary two-way CSV pipeline (`DatabaseCsvPipeline`) syncing data tables. Designers lacked a centralized interface to inspect, batch-tune, validate, and compare values across whole catalogs.

## Decision
We implement a dedicated standalone editor window (`StatBalanceManagerWindow`) with the following architectural choices:
1. **Catalog Domain Isolation**: The window provides top-level navigation tabs (`Characters`, `Items`, `Skills`), keeping schema-specific data columns isolated while sharing a unified toolset.
2. **Hybrid Table & Detail Layout**: Uses a multi-column sortable table for high-level comparison and in-place scalar stat adjustments, paired with a detail pane for complex structures (such as `ItemStatModifierData` lists and skill sub-properties).
3. **In-Memory Buffering & Safe Writes**: Data edits are staged in memory with clear dirty state tracking, allowing designers to tweak multiple entities and revert or commit explicitly to ScriptableObjects.
4. **First-Class CSV Synchronization**: Integrates directly with `DatabaseCsvPipeline` for one-click export to and import from `Assets/DataTables/`, maintaining parity between in-editor tuning and external spreadsheet workflows.
5. **Bulk Arithmetic & Real-Time Validation**: Features a batch multiplier tool (+/- % and flat offsets) and real-time inline row warning badges for missing references, invalid values, or schema conflicts.
