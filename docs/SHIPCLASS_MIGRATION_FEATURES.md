# ShipClass ➜ Ship Core Framework Migration Features

This document defines practical features to support **full migration** from legacy `ModConfig`/`GridClass` XML into Ship Core Framework files.

## 1) Core XML Mapping Rules

### Legacy root-level mapping
- `IgnoreFactionTags`, `MaxPossibleSpeedMetersPerSecond`, and `IncludeAiFactions` should map to global framework config values.
- Legacy `DefaultGridClass` should always map to **no-core config** (`ShipCoreConfig_No_Core.xml`).

### Legacy class mapping
- Each legacy `<GridClass>` should become one Ship Core definition file and an entry in `ShipCoreConfig_Manifest.xml`.
- Recommended core id/name strategy:
  - Core file name: sanitized `<Name>` (e.g., `Firebase` → `Firebase_Core.xml`).
  - Display name: preserve `<Name>`.
  - Internal unique id: deterministic hash or slug to avoid collisions.

## 2) BlockLimit and BlockGroup Migration Rules

### 2.1 Convert inline BlockTypes to reusable BlockGroups
For every migrated limit:
1. Build a normalized signature of its `<BlockTypes>` list:
   - key per block: `TypeId|SubtypeId|CountWeight`
   - sort keys ascending
   - join into one signature string
2. Compare signatures across all cores for limits with the **same logical name**.
3. If signatures are identical across cores:
   - create one shared group in `ShipCoreConfig_Groups.xml`
   - point each core limit to the shared group.
4. If signatures differ by core:
   - create core-specific group names and point each limit to the core-specific group.

### 2.2 Naming convention for generated groups
- Shared group: `BL_<SanitizedLimitName>`
- Core-specific group: `BL_<SanitizedLimitName>__<SanitizedCoreName>`

Examples:
- same across all cores: `FB-NW 100mm` → `BL_FB_NW_100mm`
- different per core: `Weapon PTS` in `Firebase` → `BL_Weapon_PTS__Firebase`

### 2.3 Preserve behavior fields
For each limit, migrate:
- `MaxCount`
- no-fly enforcement flag (`TurnedOffByNoFlyZone`/equivalent target property)
- any punishment metadata supported by the target schema.

## 3) Data Quality/Conflict Handling

Add migration checks before export:
- Duplicate core names after sanitization.
- Duplicate generated group names.
- Empty limit/group definitions after normalization.
- Same limit name but incompatible block list shape (warn and split to per-core groups).
- Unknown tags: preserve in warnings report for manual review.

## 4) Suggested Configurator Features

To make this migration one-click in the configurator:

1. **Legacy importer mode (`ModConfig`)**
   - Paste/upload old `ModConfig` XML.
   - Parse `DefaultGridClass` + `GridClasses`.

2. **No-Core assignment toggle**
   - Default ON: `DefaultGridClass` → `ShipCoreConfig_No_Core.xml`.

3. **Cross-core BlockGroup dedupe engine**
   - Implements signature comparison above.
   - Option: strict (`CountWeight` included) vs loose (`TypeId+SubtypeId` only).

4. **Group naming strategy selector**
   - `shared + per-core split` (recommended)
   - always per-core
   - always shared by limit name (unsafe, expert mode)

5. **Migration report output**
   - Summary counts:
     - classes migrated
     - limits migrated
     - groups generated (shared vs per-core)
     - warnings/errors
   - Include a table of split groups caused by mismatched block definitions.

6. **Preview diff panes**
   - Side-by-side for each generated file:
     - `ShipCoreConfig_No_Core.xml`
     - `ShipCoreConfig_Groups.xml`
     - `ShipCoreConfig_Manifest.xml`
     - each core XML

## 5) Recommended Handling of Your Example

Based on your sample:
- `DefaultGridClass` should be emitted as no-core config.
- `Firebase` should become a dedicated core XML.
- Limits like `UND Weapon PTS` and `FB Weapon PTS` should be evaluated by signature:
  - if exact block list+weights match: one shared generated group.
  - if not exact (your sample appears to differ): split to per-core group names.
- `UND Forbidden` is a good candidate for a large reusable group if reused unchanged by other cores.

## 6) Definition of "full migration"

A migration run is complete only when:
- no-core + all cores are emitted,
- all limits are represented via group references,
- all referenced groups exist,
- manifest references all produced core files,
- a migration report is produced with zero hard errors.
