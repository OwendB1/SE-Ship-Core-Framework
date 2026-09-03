import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [fields, sessionRun, commands, presentationPackets, config, loading, groupLimits,
  serverFields, serverLifecycle, serverTick] = await Promise.all([
    read("Session/Session.Fields.cs"),
    read("Session/Session.Run.cs"),
    read("Server/Commands/Commands.Administration.cs"),
    read("Client/Network/PresentationPacketHandlers.cs"),
    read("Config/ModConfig.cs"),
    read("Config/ModConfig.Loading.cs"),
    read("Server/Components/GroupComponent.Limits.cs"),
    read("Session/Server/Session.ServerFields.cs"),
    read("Session/Server/Session.ServerLifecycle.cs"),
    read("Session/Server/Session.ServerTick.cs"),
  ]);

assert.match(fields, /static volatile ModConfig Config/);

for (const [name, source] of [
  ["session startup", sessionRun],
  ["server reload", commands],
  ["client fallback", presentationPackets],
]) {
  const construct = source.indexOf("var loadedConfig = new ModConfig()");
  const load = source.indexOf("loadedConfig.LoadConfig", construct);
  const publish = source.indexOf("Config = loadedConfig", load);
  assert.ok(construct >= 0 && load > construct && publish > load, `${name} must load before publish`);
}
assert.doesNotMatch(commands, /Session\.Config = null/);

assert.doesNotMatch(config, /DefaultNoCoreConfig|_defaultNoCore/);
assert.doesNotMatch(loading, /DefaultNoCoreConfig|_defaultNoCore/);

assert.match(loading, /var resolvedGroups = new List<BlockGroup>\(\)/);
assert.match(loading, /limit\.BlockGroups = resolvedBlockGroups/);
assert.match(loading, /limit\.ExcludedBlockGroups = resolvedExcludedBlockGroups/);
assert.doesNotMatch(loading, /resolvedGroups\.Clear\(\)/);

assert.match(serverFields, /int _serverSimulationBatchRunning/);
assert.match(serverLifecycle, /Interlocked\.Exchange\(ref _serverSimulationBatchRunning, 0\)/);
assert.match(serverTick, /Interlocked\.CompareExchange\(ref _serverSimulationBatchRunning, 1, 0\)/);
assert.match(
  serverTick,
  /finally[\s\S]*Interlocked\.Exchange\(ref _serverSimulationBatchRunning, 0\)/,
);
assert.match(serverTick, /catch \(System\.Exception exception\)/);
assert.match(serverTick, /background batch failed/);
assert.match(serverTick, /failed to schedule background batch/);
assert.doesNotMatch(serverTick, /throw;/);
assert.match(
  groupLimits,
  /EnforceGroupPunishment\(bool[\s\S]*if \(!Session\.IsServer \|\| _closing \|\| Session\.IsShuttingDown\) return;[\s\S]*if \(!Session\.IsGameThread\)[\s\S]*InvokeOnGameThread/,
);

console.log("Thread-safe config publication and single-flight tick contract checks passed.");
