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

