### 4.4 Art Direction: 2D Pixel Art with AI-Generated Assets (Binding)

**Status:** SBGames stays 2D top-down pixel art. There is no 3D migration.
Art assets are produced with AI generation plus a mandatory cleanup step.
(Reason: art cost and availability for a solo developer; AI output was
inspected and judged good enough.)

**Art spec (single source of truth; do not deviate):**
- Pixels per unit (PPU): `<TBD>`
- Tile size: `<TBD>` px
- Character canvas: `<TBD>` x `<TBD>` px, pivot `<TBD>` (e.g. bottom-center)
- Icon canvas (items, runes, artifacts, skills): `<TBD>` x `<TBD>` px
- Palette: shared fixed palette file at `<TBD path>`; all assets are
  quantized to it
- Facings / animations required per unit: `<TBD>` (e.g. idle, walk, attack,
  cast, hit, death)

**Rules for agents:**
1. **Never import raw AI output.** Every AI-generated image must pass the
   cleanup step first: nearest-neighbor downscale to spec size, palette
   quantization, transparent background, correct canvas and pivot.
2. **Unity import settings are fixed for all sprites:** Filter Mode = Point,
   Compression = None, Sprite Mode and PPU per the spec above, Mesh Type =
   Full Rect for pixel sprites.
3. **Naming follows the data tables.** Asset names derive from the IDs in
   `Items.csv`, `Skills.csv` and `Characters.csv` (e.g. `<Id>_idle`,
   `<Id>_walk_01`). Do not invent a second naming scheme.
4. **Do not generate or replace art on your own.** Agents wire up existing
   sprites, animators and prefabs. Use clearly named placeholder sprites for
   missing art and list them in the task summary.
5. **Animator work keeps the existing structure** (controllers under
   `Assets/Animators/...`). Do not restructure folders or rename clips.
6. **Provenance:** keep a record of which assets are AI-generated and which
   tool produced them (`docs/art/ai_asset_log.md`), as this is needed for
   store disclosure and licensing checks.
7. **Existing rules still apply** (zero-allocation hot paths, damage only
   via `IDamageable.TakeDamage`, immutable ScriptableObjects, glossary terms).