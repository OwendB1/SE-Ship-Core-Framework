import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [config, worldSettings, session, dispatch, administration, player, faction, ownership, help, hud] =
  await Promise.all([
    read("Config/ModConfig.XmlModels.cs"),
    read("Config/ModConfig.WorldSettings.cs"),
    read("Session/Session.Fields.cs"),
    read("Server/Commands/Commands.Dispatch.cs"),
    read("Server/Commands/Commands.Administration.cs"),
    read("Server/Managers/PerPlayerManager.cs"),
    read("Server/Managers/PerFactionManager.cs"),
    read("Server/Components/GroupComponent.Ownership.cs"),
    read("Client/UI/Commands.Presentation.cs"),
    read("Client/UI/BuildPreviewHud.cs"),
  ]);

assert.match(config, /CreativeCoreCountLimitsEnabled = true/);
assert.match(worldSettings, /CreativeCoreCountLimitsEnabled = import\.CreativeCoreCountLimitsEnabled/);
assert.match(session, /!MyAPIGateway\.Session\.CreativeMode[\s\S]*Config\.CreativeCoreCountLimitsEnabled/);
assert.match(dispatch, /case "corecountlimits":[\s\S]*CheckIfAdmin\(playerId\)[\s\S]*CoreCountLimits\(args\)/);
assert.match(administration, /if \(!MyAPIGateway\.Session\.CreativeMode\)/);
assert.match(administration, /CreativeCoreCountLimitsEnabled = value == "on"/);
assert.match(administration, /SaveConfig\(true\)/);
assert.match(player, /Session\.GetEffectivePlayerCoreLimit\(core\)/);
assert.match(faction, /if \(!Session\.CoreCountLimitsEnabled \|\| core == null\)/);
assert.match(faction, /requiresFaction = HasFactionCoreLimit\(core\)/);
assert.match(ownership, /Session\.CoreCountLimitsEnabled/);
assert.match(help, /\/core corecountlimits on\|off[\s\S]*creative worlds/);
assert.match(hud, /CoreCountLimitReminderText = "SCF: Player\/faction core count limits OFF"/);
assert.match(hud, /new Vector2D\(0\.98d, 0\.9d\)/);
assert.match(hud, /visible = !global::ShipCoreFramework\.Session\.CoreCountLimitsEnabled/);
assert.match(hud, /UpdateCoreCountLimitReminder\(\)[\s\S]*_coreStatusHud\?\.Update\(\)/);

console.log("Creative core count limits command contract checks passed.");
