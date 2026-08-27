import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [dispatch, chat, presentation] = await Promise.all([
  read("Shared/Commands/Commands.Dispatch.cs"),
  read("Client/UI/Commands.Chat.cs"),
  read("Client/UI/Commands.Presentation.cs"),
]);

assert.match(dispatch, /sub\.Equals\("mass", StringComparison\.OrdinalIgnoreCase\)/);
assert.match(chat, /case "mass":[\s\S]*CoreDryMass\(playerId\)/);
assert.match(presentation, /GetCoreDryMass\(IMyCubeGrid targetGrid, GroupComponent groupComponent\)/);
assert.match(presentation, /GridComponent\.GetBlockMass\(block\)/);
assert.match(presentation, /Dictionary<string, Dictionary<float, int>>/);
assert.match(presentation, /MAIN GRID:/);
assert.match(presentation, /SUBGRID /);
assert.match(presentation, /GROUP TOTAL:/);
assert.match(presentation, /Inventory contents are excluded/);
assert.match(presentation, /\/core mass[\s\S]*dry mass by block type/);

console.log("Core dry mass command contract checks passed.");
