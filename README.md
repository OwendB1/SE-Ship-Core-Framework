# Ship Core System

Ship Core System is a Space Engineers project centered on core-driven ship rules. Instead of relying only on vanilla block limits or Torch-side plugins, it lets a grid group inherit limits, modifiers, speed behavior, defense tuning, placement rules, and upgrade-module effects from a selected ship core or no-core profile.

This repository contains:

- `ShipCoreFramework/`
  - Main framework mod package.
- `ArcaneShipCores/`
  - Additional core content built on top of the framework.
- `docs/`
  - Static XML configurator for authoring and updating config files.

## Feature overview

- Per-core and no-core profiles.
- Placement caps by player, faction, faction size, and manifest group.
- Shared `BlockGroup` definitions for reusable block-limit buckets.
- Weighted block limits with per-limit punishment types.
- Capacity gates for blocks, PCU, mass, beacon requirement, mobility type, and faction size.
- Speed enforcement with normal hard-cap mode or friction soft-cap mode.
- Boost and active-defense abilities.
- Upgrade modules that modify grid stats, speed values, defense values, and block-limit counts.
- No-fly zones with either full force-off or limit-specific punishment.
- Connector-aware limit behavior:
  - `CrossConnectorPunishment` for connected no-core grids.
  - Manifest blacklist for connected core groups.
- Critical limits that stay exempt from min-block and manifest-blacklist total limited-block shutoff.
- Minimum-block limited-block gate with periodic recheck.
- External mod API.

## Config file map

The framework reads configuration from several XML entry points.

| File | Shape | Purpose |
| --- | --- | --- |
| `ShipCoreConfig_World.xml` | `<ModConfig>` | World settings, ignored factions, no-fly zones, selected no-core profile, world max speed. |
| `Data/ShipCoreConfig_Groups.xml` | XML list of `BlockGroup` entries | Reusable block classifications for block limits. |
| `Data/ShipCoreConfig_Manifest.xml` | `<CoreManifest>` | Lists core files, upgrade-module files, manifest groups, connector blacklist entries. |
| `Data/ShipCoreConfig_No_Core.xml` | `<ShipCore>` | Default no-core profile loaded by a mod. |
| Per-core XML files from manifest | `<ShipCore>` | Actual ship-core definitions. |
| Per-upgrade-module XML files from manifest | `<UpgradeModule>` | Upgrade-module definitions. |

Load behavior:

- `SelectedNoCoreUniqueName` picks one loaded no-core profile by `UniqueName`.
- Manifest groups are global across all loaded manifest files.
- Duplicate core names, subtype IDs, manifest group names, and upgrade module `TypeId`/`SubtypeId` pairs are rejected during load.

## Conventions and behavior notes

- Most optional caps use a negative value to mean "disabled". Use the notes below for field-specific behavior.
- `UniqueName` is the friendly/config name shown in commands and some UI.
- `SubtypeId` is the actual block definition subtype used for core identity and default `UpgradeModule` upgrade-module identity.
- Logical groups are mechanical groups. Connectors, rotors, pistons, and similar subgrids can end up in one group depending on the game link graph.
- Many punishment gates do not remove the core. They shut off modifiers, speed, or limited blocks instead.
- `MinBlocks` is no longer only a one-time startup check. It also drives a limited-block punishment gate:
  - falling below minimum immediately forces non-critical limited blocks off
  - once the group reaches minimum again, the limited-block gate clears
  - the group is still periodically rechecked so stale state is corrected
- Manifest connector blacklist is separate from `CrossConnectorPunishment`.
  - `CrossConnectorPunishment` only affects limits marked with that flag and only pulls in blocks from connected no-core groups.
  - `CrossConnectorPunishmentWhitelist` in the manifest disables that pull-in behavior for listed core `SubtypeId` values.
  - Manifest blacklist compares connected core groups by `CoreSelectionPriority`, then block count, and can shut off all limited blocks on the lower-priority or smaller blacklisted group.
- Mechanical groups only allow one configured core subtype. Main-core selection is only backup/failover selection within that subtype and does not use `CoreSelectionPriority`.

## World config reference (`ShipCoreConfig_World.xml`)

Root tag: `<ModConfig>`

### World fields

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `IgnoreAiFactions` | `bool` | Ignores NPC-spawned grids for core placement/enforcement. | Does not replace `IgnoredFactionTags`; it is a separate skip path. |
| `IgnoredFactionTags` | `List<string>` | Faction tags to skip for enforcement and punishment. | Useful for admin, event, or NPC factions. |
| `SelectedNoCoreUniqueName` | `string` | Chooses which loaded no-core profile becomes the active fallback profile. | Must match a loaded no-core `UniqueName`. |
| `DebugMode` | `bool` | Enables debug-oriented behavior. | Also changes some player-count checks to count identities more aggressively. |
| `CombatLogging` | `bool` | Enables combat logging behavior exposed by the framework. | Runtime toggle also exists through commands. |
| `LOG_LEVEL` | `int` | Server/framework log verbosity. | Default in code is `2`. |
| `CLIENT_OUTPUT_LOG_LEVEL` | `int` | Client-side log verbosity. | Default in code is `2`. |
| `MaxPossibleSpeedMetersPerSecond` | `float` | World top-speed baseline in m/s. | Core `SpeedModifiers.MaxSpeed` and `MaxBoost` multiply against this value. |
| `MassTypeMode` | `Dry` or `Wet` | Chooses which grid mass reading is used for `MaxMass`. | `Dry` ignores inventory/fuel mass; `Wet` includes it. |
| `FrictionSpeedValueMode` | `Modifier` or `Absolute` | Chooses how friction min/max speed fields are interpreted. | In `Modifier` mode they scale against world max speed; in `Absolute` mode they are m/s values. |
| `BlockDirectionalPlacementOnSubgrids` | `bool` | Controls whether directional block limits treat subgrid placement as invalid. | Default is `true`. When `true`, a limited block with `AllowedDirections` cannot be placed on a different grid than the main core. When `false`, subgrids bypass the directional lock. |
| `NoFlyZones` | `List<Zones>` | World no-fly zones. | See nested fields below. |

### No-fly zone fields

Each entry uses `<Zones>`.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `ID` | `int` | Unique zone ID. | Used by admin commands and logging. |
| `Position` | `Vector3D` | Center of the zone. | Space Engineers XML vector format. |
| `Radius` | `double` | Radius in meters. | Punishment starts when a grid position is inside radius. |
| `AllowedCoresSubtype` | `List<string>` | Cores allowed inside the zone. | Runtime currently compares against the core `UniqueName`, despite the tag name saying subtype. |
| `OverideBlockLimitsForceShutOff` | `bool` | Forces all blocks off inside the zone. | Tag spelling is intentionally the current shipped spelling. If `false`, only limits with `PunishByNoFlyZone=true` are punished. |

## Manifest reference (`Data/ShipCoreConfig_Manifest.xml`)

Root tag: `<CoreManifest>`

### Manifest groups

`<ManifestGroups>` contains repeated `<Group>` entries.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Name` | `string` | Shared manifest group name. | Referenced by ship-core manifest entries. |
| `MaxCount` | `int` | Maximum simultaneous cores in this manifest group. | Must be non-negative. Group counts are global across loaded mods/configs. |

### Cross connector punishment whitelist

Repeated `<CrossConnectorPunishmentWhitelist>` entries under `<CoreManifest>` list core `SubtypeId` values. If the active core type is listed, `CrossConnectorPunishment` limits on that core do not pull blocks from connected no-core groups.

### Manifest ship-core entries

Repeated `<ShipCore>` entries under `<CoreManifest>`.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Filename` | `string` | Path to the `<ShipCore>` XML file in the mod. | Required. |
| `Group` | `List<string>` | Manifest group membership for this core file. | Repeat the tag once per group membership. |
| `CoreSelectionPriority` | `int` | Priority used only by manifest connector blacklist ranking. | Higher wins. Default `0`; equal priorities fall back to size. |
| `BlacklistedCoreSubtypeId` | `List<string>` | Core subtype IDs this core blacklists when connected by connector. | Repeat the tag once per blacklisted subtype. |

Blacklist behavior:

- Only applies when two core groups are connector-linked.
- The higher `CoreSelectionPriority` group is treated as the blacklisting side.
- If priorities match, the bigger group by block count is treated as the blacklisting side.
- The winning group's main core checks its blacklist against the losing group's main core `SubtypeId`.
- If matched, the losing group's limited blocks are shut off.
- If priority and block count both match, neither side outranks the other for blacklist punishment.

### Manifest upgrade-module entries

Repeated `<UpgradeModule>` entries under `<CoreManifest>`.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Filename` | `string` | Path to the `<UpgradeModule>` XML file in the mod. | Required. |

## Block-group reference (`Data/ShipCoreConfig_Groups.xml`)

This file is an XML list of `BlockGroup` entries. `BlockGroup` definitions let multiple limits reuse the same block classification list.

### `BlockGroup`

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Name` | `string` | Reusable block-group name. | Referenced by `BlockLimit.BlockGroups`. |
| `BlockTypes` | `List<BlockType>` | Block matching rules inside the group. | A block limit can reference multiple named groups. |

### `BlockType`

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `TypeId` | `string` | Space Engineers block type ID. | Required for a useful match. |
| `SubtypeId` | `string` | Specific subtype ID. | Empty means any subtype under that type. `any` also works as wildcard. |
| `CountWeight` | `float` | Weight contributed by a matching block. | Use fractions or larger weights for weighted caps. |
| `PrimaryDirection` | `DirectionType` | Block-local axis used for directional locking. | Optional. Defaults to `Forward`; use values like `Up` for blocks whose practical facing is not their forward axis. |

## Ship-core and no-core reference (`Data/ShipCoreConfig_No_Core.xml` and per-core XML files)

Root tag: `<ShipCore>`

The no-core file and normal core files use the same schema. Manifest groups and connector blacklist membership are assigned in the manifest, not inside the core XML itself.

### Identity and placement fields

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `SubtypeId` | `string` | Block subtype ID for this core. | Required for normal cores. |
| `UniqueName` | `string` | Friendly/config name for this core. | Used by commands, selection, and some UI. |
| `MaxBackupCores` | `int` | Max backup cores allowed in the same logical group. | Intended as extra cores beyond the active main core. Use negative to disable. |
| `ForceBroadCast` | `bool` | Requires beacon-based broadcasting for the core. | Missing or broken beacon becomes a punishment gate. |
| `ForceBroadCastRange` | `float` | Beacon radius forced onto tracked beacons. | Used when `ForceBroadCast=true`. |
| `MobilityType` | `Static`, `Mobile`, `Both` | Allowed grid mobility type for the group. | Mismatch punishes both speed and modifiers. |
| `MaxBlocks` | `int` | Maximum allowed total blocks in the logical group. | New blocks are removed if they exceed this. At/over cap also triggers capacity punishment. Negative disables. |
| `MinBlocks` | `int` | Minimum blocks required to keep limited blocks enabled. | Falling below this immediately forces non-critical limited blocks off; reaching the minimum clears the gate. Negative disables. |
| `MaxMass` | `float` | Maximum allowed total group mass. | Uses `MassTypeMode` to choose dry or wet mass. New blocks are removed if they exceed this. Negative disables. |
| `MaxPCU` | `int` | Maximum allowed total group PCU. | New blocks are removed if they exceed this. Negative disables. |
| `MaxPerFaction` | `int` | Fixed cap on how many groups of this core a faction may own. | `-1` disables fixed faction cap. |
| `FactionPlayersNeededPerCore` | `int` | Player-scaled faction cap. | `N` means one allowed core per `N` faction members. If combined with `MaxPerFaction`, runtime uses the lower of the two caps. |
| `MinFactionRank` | `None`, `Member`, `Leader`, `Founder` | Minimum grid majority-owner faction rank required to place this core. | Optional, defaults to `None`. Invalid placement is rejected and refunded before the core activates or counts against faction limits. |
| `MaxPerPlayer` | `int` | Cap on how many groups of this core a single player may own. | Negative disables. |
| `MinPlayers` | `int` | Minimum faction member count required for this core. | If set and owner has no faction, placement is rejected. Negative disables. |
| `MaxPlayers` | `int` | Maximum faction member count allowed for this core. | If faction is larger than this, placement/punishment gates fail. Negative disables. |

### Upgrade-module allowance field

Repeated `<AllowedUpgradeModules>` entries inside `<ShipCore>`.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `TypeId` | `string` | Allowed upgrade-module block type. | Optional. If omitted with `SubtypeId`, defaults to `UpgradeModule` for legacy entries. |
| `SubtypeId` | `string` | Allowed upgrade-module subtype. | Legacy/default path: matches `UpgradeModule/<SubtypeId>`. If set, this identity is checked before `UniqueName`. |
| `UniqueName` | `string` | Allowed upgrade-module config name. | Use this for upgrade modules whose config defines a non-default `TypeId`. |
| `MaxCount` | `int` | Maximum attached modules for that allowance on this main core. | Exceeding modules are removed as invalid. |

Upgrade-module application rules:

- Module definition or `UniqueName` must be listed here.
- Module must be attached to the current main core.
- Module must be functional and enabled.
- Only modules attached to the current main core contribute effects.

### Grid modifier fields (`<Modifiers>`)

These are multiplicative stat baselines unless modified by upgrade modules.

| Tag | Meaning |
| --- | --- |
| `AssemblerSpeed` | Assembler speed multiplier. |
| `DrillHarvestMultiplier` | Drill harvest multiplier. |
| `GyroEfficiency` | Gyro power-efficiency multiplier. |
| `GyroForce` | Gyro force multiplier. |
| `PowerProducersOutput` | Power output multiplier. |
| `RefineEfficiency` | Refinery efficiency multiplier. |
| `RefineSpeed` | Refinery speed multiplier. |
| `ThrusterEfficiency` | Thruster efficiency multiplier. |
| `ThrusterForce` | Thruster force multiplier. |

### Passive defense fields (`<PassiveDefenseModifiers>`)

All values are multipliers. `1` is neutral, below `1` reduces incoming damage, above `1` increases it.

| Tag | Meaning |
| --- | --- |
| `Bullet` | Bullet damage multiplier. |
| `PostShield` | Post-shield damage multiplier. |
| `Rocket` | Rocket damage multiplier. |
| `Explosion` | Explosion damage multiplier. |
| `Environment` | Environmental damage multiplier. |
| `Energy` | Energy damage multiplier. |
| `Kinetic` | Kinetic damage multiplier. |
| `Duration` | Present in schema but mainly relevant for active defense. |
| `Cooldown` | Present in schema but mainly relevant for active defense. |

### Speed fields

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `SpeedBoostEnabled` | `bool` | Enables boost ability for the core. | Boost top speed uses `SpeedModifiers.MaxBoost`. |
| `SpeedLimitType` | `Normal` or `Friction` | Selects hard-cap or friction soft-cap speed enforcement. | `Normal` uses direct cap with post-boost ramp down. `Friction` applies deceleration between configured friction speeds. |

### Speed modifier fields (`<SpeedModifiers>`)

| Tag | Meaning | Notes |
| --- | --- | --- |
| `MaxSpeed` | Base speed multiplier. | Multiplies against world `MaxPossibleSpeedMetersPerSecond`. |
| `MaxBoost` | Boost speed multiplier. | Also multiplies against world max speed. |
| `BoostDuration` | Boost duration in seconds. | Also used for post-boost ramp timing in normal speed mode. |
| `BoostCoolDown` | Boost cooldown in seconds. | |
| `MinimumFrictionSpeedAbsolute` | Friction start speed in m/s. | Used only when world `FrictionSpeedValueMode=Absolute`. |
| `MaximumFrictionSpeedAbsolute` | Friction max speed in m/s. | Used only when world `FrictionSpeedValueMode=Absolute`. |
| `MinimumFrictionSpeedModifier` | Friction start speed as world-speed multiplier. | Used only when world `FrictionSpeedValueMode=Modifier`. |
| `MaximumFrictionSpeedModifier` | Friction max speed as world-speed multiplier. | Used only when world `FrictionSpeedValueMode=Modifier`. |
| `MaximumFrictionDeceleration` | Max friction deceleration in m/s². | Used by legacy linear friction when no `FrictionCurve` is configured. |
| `CruiseFrictionMultiplier` | Multiplier applied to friction while the grid is above friction speed but not accelerating beyond the threshold. | Default `1`. Values below `1` make coasting decay slower. |
| `CruiseAccelerationThreshold` | Acceleration threshold in m/s² for cruise friction detection. | Default `0.05`. |
| `FrictionCurve` | Optional ordered list of friction curve `Segment` entries. | If missing, the old linear curve is synthesized from the min/max friction fields and `MaximumFrictionDeceleration`. Segment speeds use the world `FrictionSpeedValueMode`. |
| `AtmosphericFriction` | Optional atmosphere-only override profile. | When present, `Enabled=true`, and local air density is above `AirDensityThreshold`, its curve/cruise fields override normal friction. Outside atmosphere or when disabled, normal friction is used. |

`FrictionCurve` segment fields:

| Tag | Meaning |
| --- | --- |
| `StartSpeed` | Segment start speed. Interpreted as m/s in `Absolute` mode or as a world max-speed multiplier in `Modifier` mode. |
| `EndSpeed` | Segment end speed. Interpreted the same way as `StartSpeed`. Above the last segment, the last segment's `EndDeceleration` continues. |
| `StartDeceleration` | Deceleration in m/s² at `StartSpeed`. |
| `EndDeceleration` | Deceleration in m/s² at `EndSpeed`. |

Example absolute-speed curve with 20 m/s² from 100-200 m/s and 60 m/s² from 200 m/s upward:

```xml
<SpeedModifiers>
  <MaxSpeed>1</MaxSpeed>
  <MaxBoost>1</MaxBoost>
  <BoostDuration>10</BoostDuration>
  <BoostCoolDown>60</BoostCoolDown>
  <MinimumFrictionSpeedAbsolute>100</MinimumFrictionSpeedAbsolute>
  <MaximumFrictionSpeedAbsolute>300</MaximumFrictionSpeedAbsolute>
  <MinimumFrictionSpeedModifier>0.3</MinimumFrictionSpeedModifier>
  <MaximumFrictionSpeedModifier>1</MaximumFrictionSpeedModifier>
  <MaximumFrictionDeceleration>1</MaximumFrictionDeceleration>
  <CruiseFrictionMultiplier>0.25</CruiseFrictionMultiplier>
  <CruiseAccelerationThreshold>0.05</CruiseAccelerationThreshold>
  <FrictionCurve>
    <Segment>
      <StartSpeed>100</StartSpeed>
      <EndSpeed>200</EndSpeed>
      <StartDeceleration>20</StartDeceleration>
      <EndDeceleration>20</EndDeceleration>
    </Segment>
    <Segment>
      <StartSpeed>200</StartSpeed>
      <EndSpeed>300</EndSpeed>
      <StartDeceleration>60</StartDeceleration>
      <EndDeceleration>60</EndDeceleration>
    </Segment>
  </FrictionCurve>
  <AtmosphericFriction>
    <Enabled>true</Enabled>
    <CruiseFrictionMultiplier>1</CruiseFrictionMultiplier>
    <CruiseAccelerationThreshold>0.05</CruiseAccelerationThreshold>
    <AirDensityThreshold>0.05</AirDensityThreshold>
  </AtmosphericFriction>
</SpeedModifiers>
```

### Active defense fields

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `EnableActiveDefenseModifiers` | `bool` | Enables active-defense mode for the core. | When active, the runtime uses `ActiveDefenseModifiers`. |
| `ActiveDefenseModifiers` | `GridDefenseModifiers` | Defense multipliers while active defense is running. | Uses the same nested tags as passive defense, including `Duration` and `Cooldown`. |

### Block-limit fields (`<BlockLimits>`)

Each entry uses `<BlockLimit>`.

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Name` | `string` | Limit name. | Referenced by upgrade-module `BlockLimitModifiers`. |
| `BlockGroups` | `string[]` | Names of `BlockGroup` definitions included in this limit. | A block matches the limit if it matches any referenced group entry. |
| `MaxCount` | `float` | Maximum allowed total weight for this limit. | Weight comes from matched `BlockType.CountWeight`. |
| `CrossConnectorPunishment` | `bool` | Pulls blocks from connected no-core groups into this limit's bucket. | Only affects this limit. Does not control min-block or manifest-blacklist limited-block gates. |
| `PunishByNoFlyZone` | `bool` | Applies this limit's punishment inside no-fly zones. | Only used when the zone itself is not forcing everything off. |
| `IsCriticalLimit` | `bool` | Marks this limit as exempt from total limited-block shutoff gates. | Min-block and manifest-blacklist shutoff skip this limit. Normal limit overflow, directional checks, and no-fly-zone punishment still work normally. |
| `PunishmentType` | `ShutOff`, `Damage`, `Delete`, `Explode` | Punishment for blocks in this limit when the limit is violated. | Limited-block gate punishment always uses `ShutOff`, regardless of this setting. |
| `AllowedDirections` | `List<DirectionType>` | Directional lock for this limit. | If set, mismatched blocks are punished even if count is under cap. Directions are relative to the main core and compare the matched `BlockType.PrimaryDirection` axis. Subgrid behavior is controlled by world setting `BlockDirectionalPlacementOnSubgrids`. |

### Direction values

`AllowedDirections` may use:

- `Forward`
- `Backward`
- `Up`
- `Down`
- `Left`
- `Right`

## Upgrade-module reference (per-upgrade XML files)

Root tag: `<UpgradeModule>`

### Upgrade module fields

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `TypeId` | `string` | Upgrade-module block type ID. | Optional; defaults to `UpgradeModule`. Used with `SubtypeId` for early runtime tracking. |
| `SubtypeId` | `string` | Upgrade-module subtype ID. | Required. |
| `UniqueName` | `string` | Friendly/config name. | Falls back to `SubtypeId` if omitted. |
| `Modifiers` | `List<UpgradeStatModifier>` | Stat modifiers applied to the main core. | See recognized stats below. |
| `BlockLimitModifiers` | `List<BlockLimitModifier>` | Per-limit max-count modifiers. | References block-limit `Name`. |

### `UpgradeStatModifier`

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `Stat` | `string` | Runtime stat key. | Unknown names load but do nothing. |
| `Value` | `float` | Modifier value. | Interpreted by `ModifierType`. |
| `ModifierType` | `Additive` or `Multiplicative` | How `Value` is applied. | Default is multiplicative. |

### Recognized `Stat` values

Grid/system stats:

- `AssemblerSpeed`
- `DrillHarvestMultiplier`
- `GyroEfficiency`
- `GyroForce`
- `PowerProducersOutput`
- `RefineEfficiency`
- `RefineSpeed`
- `ThrusterEfficiency`
- `ThrusterForce`

Speed stats:

- `MaxSpeed`
- `MaxBoost`
- `BoostDuration`
- `BoostCoolDown`
- `MinimumFrictionSpeedAbsolute`
- `MaximumFrictionSpeedAbsolute`
- `MinimumFrictionSpeedModifier`
- `MaximumFrictionSpeedModifier`
- `MaximumFrictionDeceleration`
- `CruiseFrictionMultiplier`
- `CruiseAccelerationThreshold`
- `AtmosphericCruiseFrictionMultiplier`
- `AtmosphericCruiseAccelerationThreshold`
- `AtmosphericAirDensityThreshold`

Defense stats:

- `PassiveBulletDamage`
- `ActiveBulletDamage`
- `PassiveRocketDamage`
- `ActiveRocketDamage`
- `PassiveExplosionDamage`
- `ActiveExplosionDamage`
- `PassiveEnvironmentDamage`
- `ActiveEnvironmentDamage`
- `PassivePostShieldDamage`
- `ActivePostShieldDamage`
- `PassiveEnergyDamage`
- `ActiveEnergyDamage`
- `PassiveKineticDamage`
- `ActiveKineticDamage`
- `ActiveDefenseDuration`
- `ActiveDefenseCooldown`

### `BlockLimitModifier`

| Tag | Type | Meaning | Notes |
| --- | --- | --- | --- |
| `BlockLimitName` | `string` | Target block-limit `Name`. | Required. |
| `Value` | `float` | Modifier value. | Interpreted by `ModifierType`. |
| `ModifierType` | `Additive` or `Multiplicative` | How `Value` is applied to the limit max count. | Default is additive. |

### Current caveats

- `ActiveDefenseDuration` and `ActiveDefenseCooldown` are parsed and applied to the defense modifier cache, but live active-defense timers still use the base core values.
- `PassiveBulletDamage` and `ActiveBulletDamage` are parsed, but live bullet damage routing currently goes through other damage channels, so bullet-specific entries do not presently change gameplay the way the name suggests.
- Unknown `Stat` values are not rejected by validation. They load successfully and then do nothing at runtime.

## Commands

The framework exposes chat and admin commands under `/core`. Common ones include:

- `/core help`
- `/core info`
- `/core listcores`
- `/core listnocores`
- `/core select <NoCoreName|Subtype>`
- `/core coreinfo <UniqueName>`
- `/core reloadconfig`
- `/core listnfzs`
- `/core createnfz ...`
- `/core deletenfz <id>`
- `/core debug on|off`
- `/core combatlog on|off`
- `/core loglevel ...`
- `/core setworldspeed <m/s>`
- `/core ignoretags ...`
- `/core ignoreai`

Use `/core help` in-game for current command text.

## External API

The framework includes a mod API for other mods.

- Usage guide: [ShipCoreFramework/src/API_USAGE.md](ShipCoreFramework/src/API_USAGE.md)
- DTOs/constants: `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/ApiData.cs`
- Sample client wrapper: `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/SCF_ModAPIClient.cs`

Recommended integration path:

- copy `ApiData.cs`
- copy `SCF_ModAPIClient.cs`
- use the client wrapper instead of raw message handlers

## XML configurator

A static configurator lives under `docs/` and helps build or update:

- world config
- block-group config
- manifest config
- per-core config
- manifest connector blacklist entries

### Local preview

```bash
python3 -m http.server 8080 --directory docs
```

Then open `http://localhost:8080/` (it redirects to `configurator/`).

## License

This project is licensed under the GNU General Public License v3.0 (`GPL-3.0-or-later`).
If you distribute modified versions, you must also provide the corresponding source code under the same GPL terms.
