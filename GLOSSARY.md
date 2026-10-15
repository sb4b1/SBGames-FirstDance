# SBGames

Core RPG systems, combat data definitions, inventory, and balance tooling for SBGames.

## Language

**Character Base Stats**:
The innate baseline attributes of a character archetype (MaxLife, MaxMana, Movespeed, Damage, AttackRange, AttackTimeInterval) configured on a ScriptableObject.
_Avoid_: Base attributes, unit parameters, hero stats

**Item Stat Modifier**:
A discrete numerical bonus (Flat, PercentAdd, or PercentMult) assigned to an item or rune targeting a specific stat type.
_Avoid_: Item perk, stat buff, affix

**Stat Balance Matrix**:
A unified tabular data model and editor view representing entity statistics across characters, items, and skills for global balancing.
_Avoid_: Stat sheet, tuning spreadsheet

**Bulk Scaling**:
A tooling operation that applies proportional (percentage) or flat offsets to numeric attributes across multiple selected or filtered entities simultaneously.
_Avoid_: Mass edit, global multiplier

**Validation Badge**:
An inline visual indicator on a catalog entry signifying data anomalies, missing references, or out-of-bound attribute thresholds.
_Avoid_: Error tag, status icon

**Boss Phase Goal**:
A high-priority GOAP Utility Goal that hard-evaluates to maximum urgency when a boss unit crosses health or time thresholds, triggering instant action interruption and phase transition mechanics.
_Avoid_: Boss state machine, enrage script

**Swarm Courage**:
A dynamic morale buffer derived from local friendly unit density, scaling down fear intake and lowering survival panic thresholds for pack-based archetypes.
_Avoid_: Pack buff, swarm aura

**Morale Shock**:
An emergent route state triggered when a pack's Alpha leader falls or severe pack casualties occur, temporarily overriding combat behavior with panic or flight.
_Avoid_: Fear trigger, pack scatter

**Archetype Threat Policy**:
A ScriptableObject configuration defining normalized scoring weights (proximity, health deficit, damage intake, cluster density) used to rank target priority for AI perception.
_Avoid_: Aggro table, threat list

**Dungeon Piece**:
One authored map chunk filling exactly one macro cell (roomWidth x roomHeight placement cells).
_Avoid_: Tile, room prefab, map block

**Piece Set**:
A themed collection of Dungeon Pieces that author a cohesive dungeon environment.
_Avoid_: Tileset, palette, theme pack

**Opening**:
A doorway side of a Dungeon Piece aligned with a CardinalDirection where an exit or corridor connects.
_Avoid_: Door slot, socket, entrance port

**Opening Cap**:
A static barrier or wall chunk placed to seal an unused Opening on a placed Dungeon Piece.
_Avoid_: Wall plug, blocker, door seal

**Footprint**:
The macro cell dimensions of a Dungeon Piece on the layout grid (strictly 1x1 in v1).
_Avoid_: Size, area, span

**Buildable Mask**:
A piece-local binary grid defining the specific interior cells where the Dungeon Master is permitted to place defenders and traps.
_Avoid_: Placement layer, spawn zone, build zone

**Layout Graph**:
The mathematical macro graph (DungeonLayoutGraph) representing room nodes, cardinal connections, and reachability.
_Avoid_: Dungeon map, floor plan, grid tree

**Opening Width**:
The width of a standard doorway opening in placement cells/tiles (default 2), symmetrically centered along the macro-cell edge. Must share even parity with cell dimensions.
_Avoid_: Door width, exit size, corridor thickness

**Tile Pixel Size**:
The uniform pixel resolution per tile (16, 32, or 48) enforced across all tileset sprites within a Piece Set, requiring texture Pixels Per Unit (PPU) to equal Tile Pixel Size so that each tile maps 1:1 to a 1-unit world cell.
_Avoid_: Sprite size, tile resolution, pixel scale


