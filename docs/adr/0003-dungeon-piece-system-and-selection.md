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

## Amendment: 16x16 Grid Standardization, Opening Geometry, and Prefab Mode Authoring

Date: 2026-10-15

### Settled Design Specifications
1. **16x16 Grid Standardization**: Standard piece size is fixed to 16x16 tiles, where 1 tilemap cell = 1 placement grid cell = 1 world unit (1:1 scale). `DungeonGeneratorConfigSO` defaults updated to `roomWidth = 16`, `roomHeight = 16`, and `corridorLength = 0` for direct adjacent abutting piece layouts.
2. **Standard Opening Geometry**:
   - Opening Width is configured per Piece Set (default 2 tiles, centered on edge: tiles 7-8 on a 16-tile edge).
   - Parity requirement: `(cellSize - openingWidth)` must be non-negative and even to ensure symmetric edge centering.
   - Pure C# `OpeningGeometry` computes edge tile coordinates and quarter-turn rotations.
   - Openings remain manually authored on the piece; `PieceValidator` validates declared Openings against the painted Ground tilemap (raising errors for missing Ground and warnings for undeclared Ground reach).
3. **Tile Pixel Size & PPU Consistency**:
   - `PieceSetSO` specifies uniform `tilePixelSize` (16, 32, or 48).
   - Sprites across all tilemaps in a piece must match `PPU == tilePixelSize` so each tile fills exactly one 1-unit cell.
   - Validation badge flags mismatches with one-click fix utility in editor tooling without mutating tileset assets unexpectedly.
4. **Prefab Mode Authoring Pipeline**:
   - Standard prefab hierarchy: root (`DungeonPiece`, `RoomVolume`, 16x16 trigger `BoxCollider2D`), child `Grid` (cellSize 1) containing `Ground` (NavMesh Walkable, order 1), `Walls` (TilemapCollider2D, NavMesh Not Walkable, order 2), and `Decoration` (NavMesh ignore, order 3) tilemaps.
   - Interactive scene GUI handles allow clicking edges to toggle Openings and painting/erasing buildable cells.
   - Automatic buildable mask generation from Ground: interior Ground minus Walls, edge margins, and Opening clearance.
   - Opening Cap prefabs are authored at `openingWidth x 1` tile dimensions to seamlessly seal unused doorways.
5. **Anchors & Entry Spawning Guarantee**:
   - Entrance pieces require $\ge 1$ authored entry anchor (raising an error badge if missing).
6. **Capacity Derived from Buildable Mask**:
   - Replaces hardcoded capacities with dynamic derivations based on rotated buildable cell count:
     $\text{Soft} = \lfloor \text{maskCount} / \text{cellsPerSoftSlot} \times \text{mult}_{\text{soft}} \rfloor$,
     $\text{Max} = \lceil \text{Soft} \times \text{mult}_{\text{max}} \rceil$.
   - Tunable on `DungeonGeneratorConfigSO` with defaults reproducing 4 soft / 6 max for standard 14x14 Normal interiors, 6 soft / 10 max for Boss, 0/0 for Entrance, and 0/2 for SafeRoom.
7. **Batch Simulation Seam & NavMesh Verification**:
   - Pure C# Seam Check verifies Ground tile continuity across all adjacent abutting doorway openings for every generated seed.
   - Post-build 2D NavMesh path check validates complete unobstructed connectivity from Entrance to Boss room.

## Amendment: Dynamic Room Sizes, Freeform Openings, Pathway Pieces, and Seam Snapping

Date: 2026-10-15

### Settled Design Specifications
1. **Dynamic Piece Sizes ($W \times H \ge 4 \times 4$)**:
   - Pieces are no longer locked to a fixed 16x16 macro cell. Each `DungeonPiece` declares its own custom dimensions (`width x height` in tiles, e.g. 10x10 small room, 24x18 large room, 6x8 pathway).
   - Fixed cell-size mismatch rejections are removed from `DungeonRuntimeBuilder` so diverse room and corridor sizes coexist in the same dungeon.
2. **Piece Structural Roles (`PieceRole`)**:
   - Each piece is categorized as either `Room` (Entrance, Normal, SafeRoom, Boss) or `Pathway` (corridor connectors).
   - Both roles are instantiated and registered in `DungeonRoomRegistry`, enabling AI navigation and DM monster/trap placement across both rooms and pathways (excluding Entrance).
3. **Freeform 2-Tile Openings (`PieceOpening`)**:
   - A piece can declare any number of 2-tile-wide openings at arbitrary tile coordinates along its boundary (`AnchorTile` + outward `CardinalDirection` normal).
   - Non-square quarter-turn rotation cleanly swaps effective dimensions ($W' = H, H' = W$) and maps anchor coordinates and outward directions through full 4-turn cycles.
4. **Collision-Free Seam Snapping**:
   - The pure C# `DungeonSeamLayoutSolver` computes exact world origin alignment for piece sockets:
     $$\text{OriginB} = \text{OriginA} + \text{OpeningA.AnchorTile} + \text{Normal}(\text{OpeningA.Direction}) - \text{OpeningB.AnchorTile}$$
   - Direct `Room -> Room` connections occur whenever opposite sockets snap without tile overlap against `OccupiedDungeonTiles` (footprint of Ground $\cup$ Walls).
   - When direct room fit overlaps, or when `pathwayProbability` (default 0.35f) rolls true, a `Pathway` piece is inserted to bridge the rooms (`Room -> Pathway -> Room`) without tile overlap.
5. **Prefab Mode Authoring with Dynamic Bounds**:
   - Authoring window provides presets for diverse room and pathway dimensions.
   - Scene view overlays draw dynamic $W \times H$ frames, freeform 2-tile opening handles, and bounded buildable mask painting.
   - "Fit Bounds to Painted Tiles" helper automatically recalibrates `CellSize` to the tight bounding box of painted Ground and Wall tiles.
6. **Pure C# Verification**:
   - Comprehensive unit tests in `Assets/Tests/DungeonPieceTests.cs` verify non-square rotation cycles, seam snapping math, zero overlap, direct room-to-room and room-to-pathway layout solving, and unused opening capping.
