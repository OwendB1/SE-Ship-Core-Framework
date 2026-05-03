# Ship Core System

Ship Core System is a Space Engineers project centered on core-driven ship rules. Instead of relying only on vanilla block limits or Torch-side plugins, it lets a grid group inherit limits, modifiers, speed behavior, defense tuning, and upgrade-module effects from a selected ship core or no-core profile.

This repository contains:

- `ShipCoreFramework/`
  - The main framework mod package.
- `ArcaneShipCores/`
  - Additional core content built on top of the framework.
- `docs/`
  - A static XML configurator for authoring and renovating config files.

## Main capabilities

- Ship-core profiles with their own limits, modifiers, boost settings, and defense behavior.
- No-core profiles for grids without an active core.
- Logical group handling across connectors and attached subgrids.
- Configurable block limits, mass limits, PCU limits, and punishment behavior.
- Speed enforcement, boost, and friction-based speed limiting.
- Passive and active defense damage modifiers.
- Upgrade modules that can modify ship stats, speed values, defense values, and block-limit counts.
- An external mod API for other mods.

## Core and group behavior

- A logical group uses its main core as the source of truth for limits, modifiers, and abilities.
- Backup cores can exist, but upgrade-module effects are only taken from modules attached to the current main core.
- Group membership can change through connectors, attached subgrids, and grid splits.
- Grids without a core fall back to the selected no-core configuration.

## Upgrade modules

Upgrade modules are loaded from each mod's manifest and only apply when all of the following are true:

- The module subtype is allowed by the active core's `AllowedUpgradeModules`.
- The module is attached to the current main core.
- The module is functional and enabled.

Upgrade modules can currently modify:

- Grid modifiers such as thruster, gyro, refinery, assembler, drill, and power-output values.
- Speed modifiers such as max speed, max boost, boost duration, boost cooldown, and friction-related values.
- Block-limit counts through `BlockLimitModifiers`.
- Passive and active defense modifier values, with the caveats listed below.

### Defense modifier notes

Recognized defense-related upgrade stats:

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

Current caveats:

- `ActiveDefenseDuration` and `ActiveDefenseCooldown` are recognized by the upgrade-module parser, but the live active-defense timers still use the base core values. In practice, upgrade modules do not currently change active-defense duration or cooldown behavior.
- `PassiveBulletDamage` and `ActiveBulletDamage` are recognized, but runtime bullet damage is currently routed through `Energy`, `Kinetic`, or `PostShield`, so bullet-specific entries do not currently affect gameplay.
- Upgrade-module stat validation only checks that `Stat` exists. Unknown or misspelled stat names load successfully but silently do nothing.

## Configuration files

The framework loads its configuration from these entry points:

- `ShipCoreConfig_World.xml`
  - World-level settings such as debug mode, combat logging, ignored factions, max world speed, friction mode, and no-fly zones.
- `Data/ShipCoreConfig_Groups.xml`
  - Shared `BlockGroup` definitions for reuse across multiple core configs.
- `Data/ShipCoreConfig_Manifest.xml`
  - Lists per-core XML files and per-upgrade-module XML files to load from a mod.
- `Data/ShipCoreConfig_No_Core.xml`
  - Default no-core profile for grids without an active core.
- Per-core XML files referenced by the manifest.
- Per-upgrade-module XML files referenced by the manifest.

At a high level:

- `ShipCore` controls limits, speed rules, passive defense, active defense, and allowed upgrade modules.
- `UpgradeModule` contains `Modifiers` and optional `BlockLimitModifiers`.
- `BlockGroup` definitions let multiple limits reuse the same block classification list.

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

Use `/core help` in-game for the current command text.

## External API

The framework includes a mod API for other mods.

- Usage guide: [ShipCoreFramework/src/API_USAGE.md](ShipCoreFramework/src/API_USAGE.md)
- DTOs/constants: `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/ApiData.cs`
- Sample client wrapper: `ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/API/SCF_ModAPIClient.cs`

The recommended integration path is to copy `ApiData.cs` and `SCF_ModAPIClient.cs` into the consuming mod and use the client wrapper rather than raw message handlers.

## XML configurator

A static configurator lives under `docs/` and is intended to help build and update the framework XML files.

### What it does

- Loads the bundled `ModConfig.XmlModels.cs` snapshot from this repository.
- Lets you define reusable `BlockGroup` entries once and reference them from multiple core `BlockLimit` definitions.
- Supports renovating existing XML by uploading prior config files.
- Ignores unknown tags so older or partially customized files can still be loaded.
- Generates updated XML outputs for groups, manifests, and per-core definitions.

### Local preview

```bash
python3 -m http.server 8080 --directory docs
```

Then open `http://localhost:8080/` (it redirects to `configurator/`).

## License

This project is licensed under the GNU General Public License v3.0 (`GPL-3.0-or-later`).
If you distribute modified versions, you must also provide the corresponding source code under the same GPL terms.
