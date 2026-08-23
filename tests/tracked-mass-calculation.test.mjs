import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [grid, trackedBlocks, cleanup, observer, sharedCache, serverCache, placement, limits, lifecycle,
  serverTick, serverServices, serverFields, serverLifecycle, config, modApi] = await Promise.all([
  read("Shared/Components/GridComponent.cs"),
  read("Shared/Components/GridComponent.Blocks.cs"),
  read("Shared/Components/GridComponent.Cleanup.cs"),
  read("Client/Components/GridComponent.Observer.cs"),
  read("Shared/Components/GroupComponent.CachedState.cs"),
  read("Server/Components/GroupComponent.CachedState.cs"),
  read("Server/Components/GridComponent.Blocks.cs"),
  read("Server/Components/GroupComponent.Limits.cs"),
  read("Shared/Components/GroupComponent.Lifecycle.cs"),
  read("Session/Server/Session.ServerTick.cs"),
  read("Session/Server/Session.ServerServices.cs"),
  read("Session/Server/Session.ServerFields.cs"),
  read("Session/Server/Session.ServerLifecycle.cs"),
  read("Config/ModConfig.XmlModels.cs"),
  read("API/ModAPI.cs"),
]);

assert.match(trackedBlocks, /internal static float GetBlockMass\(IMySlimBlock block\)/);
assert.match(trackedBlocks, /return definition\?\.Mass \?\? 0f/);
assert.match(grid, /internal readonly float DryMass/);
assert.match(grid, /private float _trackedDryMass/);
assert.match(grid, /internal TrackedContribution TrackedTotals/);
assert.match(trackedBlocks, /_trackedDryMass \+= contribution\.DryMass/);
assert.match(trackedBlocks, /_trackedDryMass -= contribution\.DryMass/);
assert.match(trackedBlocks, /RemoveTrackedBlock\(IMySlimBlock block, out TrackedContribution contribution\)/);
assert.match(cleanup, /_trackedDryMass = 0f/);
assert.match(observer, /var contribution = GetBlockContribution\(block\)/);
assert.match(observer, /RemoveTrackedBlock\(block, out contribution\)/);
assert.match(placement, /GroupMass \+ contribution\.DryMass > maxMass/);
assert.match(sharedCache, /private void AddGroupMass\(float delta\)/);
assert.match(sharedCache, /AddGroupMass\(contribution\.DryMass \* sign\)/);
assert.match(limits, /ApplyGroupContribution\(contribution, 1\)/);
assert.match(limits, /ApplyGroupContribution\(contribution, -1\)/);
assert.match(lifecycle, /var totals = comp\.TrackedTotals/);
assert.match(lifecycle, /ApplyGroupContribution\(totals, -1\)/);
assert.match(lifecycle, /_cachedDryMass = 0f/);
assert.doesNotMatch(trackedBlocks, /GetTrackedMass|GetCurrentMass/);
assert.match(modApi, /MassTypeMode = MassTypeModeData\.Dry/);

const removedPolling = [sharedCache, serverCache, serverTick, serverServices,
  serverFields, serverLifecycle].join("\n");
assert.doesNotMatch(removedPolling, /GetCurrentMass|RefreshMassCache|MassCacheRefresh|_massCache/);
assert.doesNotMatch(config, /MassTypeMode/);

console.log("tracked-mass-calculation: ok");
