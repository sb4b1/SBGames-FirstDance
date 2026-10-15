# 3. Dungeon Piece System and Selection Architecture

Date: 2026-10-15

## Context
Phase 10A established the pure C# layout generator (`DungeonLayoutGenerator`) that produces a topological `DungeonLayoutGraph` containing room nodes, reachability guarantees, and door connections. However, dungeon visuals previously relied either on raw procedural colliders or per-room-type static prefabs, lacking authored art chunks, diverse room geometries (tunnels, turns, forks, dead ends), and visual themes.

## Decision
We implement a modular **Dungeon Piece & Piece Set** pipeline that plugs into the existing layout generator rather than replacing it:
1. **Single Macro-Cell Granularity (1x1 Footprint)**: Each authored Dungeon Piece maps to exactly one macro cell (`roomWidth` x `roomHeight` placement cells, default 10x10). Multi-cell pieces are deferred to future revisions to keep layout graph generation decoupled and stable.
2. **Cardinal Opening Bitmask & Rotations**: Each piece declares its doorway openings as a bitmask of `CardinalDirection` (North, East, South, West). Pieces marked rotatable are rotated in 90-degree clockwise increments ($d \to (d + k) \pmod 4$), requiring square macro cells (`roomWidth == roomHeight`).
3. **Exact vs. Superset Selection**: The selection engine prefers exact opening-set matches. If no exact match exists for a node's doors, it accepts superset matches and records which unused openings must be sealed with `Opening Cap` prefabs.
4. **Theme Scoping via Piece Set**: A `PieceSet` groups pieces belonging to a common visual theme and defines cell dimensions and opening cap templates.
5. **Piece-Local Buildable Mask**: Dungeon Pieces author their own interior buildable cells, intersected with the global placement grid so monsters and traps can only be placed within valid walkable areas (and never in the Entrance room).
6. **Authoring Tool with Validation Badges**: An editor window provides automated validation badges (red errors for invalid footprints or zero openings, yellow warnings for rotatable pieces with directional art) and a 15-door-set coverage matrix across room types.

## Consequences
- The generator's graph structure remains completely intact; pieces are chosen to fit the layout rather than dictating the layout.
- For seamless adjacent piece alignment, zero-corridor layouts (`corridorLength = 0`) are supported with doors placed on cell borders.
- Missing piece topologies in a theme are detected ahead of time via the coverage report rather than failing unpredictably during runtime generation.
