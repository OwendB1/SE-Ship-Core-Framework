import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [blocks, limits, selection, upgrades, tick] = await Promise.all([
  read("Server/Components/GridComponent.Blocks.cs"),
  read("Server/Components/GroupComponent.Limits.cs"),
  read("Server/Components/GroupComponent.CoreSelection.cs"),
  read("Server/Components/GroupComponent.Upgrades.cs"),
  read("Session/Server/Session.ServerTick.cs"),
]);

assert.match(blocks, /Grid\.IsBlockTrasferInProgress/);
assert.match(blocks, /ScheduleBlockTransferReconcile\(\)/);
assert.match(blocks, /bypassLimits = true/);
assert.doesNotMatch(blocks, /Block limits NOT Applied\."\);\s*return false/);
assert.match(blocks, /!groupComponent\.IsLimitPunishmentDeferred\(\)/);
assert.match(limits, /grid\.IsBlockTrasferInProgress/);
assert.match(limits, /if \(IsLimitPunishmentDeferred\(\)\) return;/);
assert.match(upgrades, /if \(IsLimitPunishmentDeferred\(\)\)/);
assert.match(selection, /Session\.CurrentTick \+ 1/);
assert.match(selection, /ReconcileAfterBlockTransfer\(\)/);
assert.match(tick, /RunBlockTransferReconcileTick\(\)/);

console.log("Block-transfer limit guard contract checks passed.");
