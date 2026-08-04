import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const modifiers = await readFile(
  "ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Shared/Modifiers/CubeGridModifiers.cs",
  "utf8",
);
const config = await readFile(
  "ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Config/ModConfig.XmlModels.cs",
  "utf8",
);
const hud = await readFile(
  "ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/UI/CoreStatusHud.cs",
  "utf8",
);
const lcd = await readFile(
  "ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/UI/CoreTypeLCDScript.cs",
  "utf8",
);

assert.match(modifiers, /productivity = 0f;\s*effectiveness = 1f;/);
assert.match(modifiers, /slot < attachedModule\.SlotCount/);
assert.match(modifiers, /!upgradeBlock\.IsWorking \|\| !attachedModule\.Compatible/);
assert.match(
  modifiers,
  /\(baseSpeed \+ nativeProductivity\) \* modifiers\.RefineSpeed - baseSpeed/,
);
assert.match(
  modifiers,
  /nativeEffectiveness \* modifiers\.RefineEfficiency/,
);
assert.match(
  modifiers,
  /\(baseSpeed \+ nativeProductivity\) \* modifiers\.AssemblerSpeed - baseSpeed/,
);
assert.match(modifiers, /if \(modifiers\.RefineSpeed != -1f\)/);
assert.match(modifiers, /if \(modifiers\.RefineEfficiency != -1f\)/);
assert.match(modifiers, /CommitUpgradeValues\(\)/);
assert.match(modifiers, /OnUpgradeValuesChanged \+= state\.UpgradeValuesChanged/);
assert.match(config, /new ModifierNameValue\("Refinery yield", RefineEfficiency\)/);
assert.match(hud, /--- Framework Modifiers ---/);
assert.match(lcd, /AddSectionTitle\(sprites, "Framework Modifiers"/);
assert.doesNotMatch(modifiers, /baseYield|effSum/);

const yieldPerSlot = 1.0905077;
const nativeYield = yieldPerSlot ** 4;
const nativeProductivity = 0.5 * 4;
assert.ok(Math.abs(nativeYield - Math.SQRT2) < 1e-6);
assert.equal((1 + nativeProductivity) * 1.5, 4.5);
assert.ok(Math.abs(nativeYield * 1.5 - 2.1213200893900654) < 1e-12);

console.log("production-modifiers: ok");
