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

