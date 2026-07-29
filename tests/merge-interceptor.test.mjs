import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [blocks, cleanup, blockInterceptor, groupInterceptor, limits, tick] =
  await Promise.all([
    read("Server/Components/GridComponent.Blocks.cs"),
    read("Server/Components/GridComponent.Cleanup.cs"),
    read("Server/Components/GridComponent.MergeInterception.cs"),
    read("Server/Components/GroupComponent.MergeInterception.cs"),
    read("Server/Components/GroupComponent.Limits.cs"),
    read("Session/Server/Session.ServerTick.cs"),
  ]);

assert.match(blocks, /MergeStateChanged \+= MergeBlockOnStateChanged/);
assert.match(blocks, /MergeStateChanged -= MergeBlockOnStateChanged/);
assert.match(cleanup, /MergeStateChanged -= MergeBlockOnStateChanged/);
assert.match(blockInterceptor, /MergeState\.Constrained/);
assert.match(blockInterceptor, /GroupComponent\.ScheduleMergeValidation/);
assert.doesNotMatch(blockInterceptor + groupInterceptor, /BeforeMerge|OnGridMerge/);

assert.match(groupInterceptor, /Session\.CurrentTick \+ \(deferOneTick \? 1 : 0\)/);
assert.match(groupInterceptor, /Utils\.IsCoreBlock\(block\)/);
assert.match(groupInterceptor, /GetProjectedUpgradeModules/);
assert.match(groupInterceptor, /ComputeEffectiveMaxCount\(shipCore, limit, projectedModules\)/);
assert.match(groupInterceptor, /first\.Enabled = false/);
assert.match(groupInterceptor, /second\.Enabled = false/);
assert.match(limits, /ComputeEffectiveMaxBlocks\(ShipCore, GetEffectiveUpgradeModules\(true\)\)/);
assert.match(tick, /RunPendingMergeValidationTick\(\)/);

console.log("Merge interceptor contract checks passed.");
