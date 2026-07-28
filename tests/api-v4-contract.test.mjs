import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL("../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/", import.meta.url);
const read = async path => readFile(new URL(path, root), "utf8");

const [
  data,
  provider,
  clientFactory,
  serverFactory,
  clientConfig,
  runtimeConsumer,
  wrapper,
] = await Promise.all([
  read("API/ApiData.cs"),
  read("API/ModAPI.cs"),
  read("API/Client/ModAPI.ReplicaQueries.cs"),
  read("API/Server/ModAPI.ServerFactory.cs"),
  read("Client/Network/PresentationPacketHandlers.cs"),
  read("Session/Client/Session.RuntimeStateConsumer.cs"),
  read("API/SCF_ModAPIClient.cs"),
]);

assert.match(data, /API_MAJOR\s*=\s*4/);
assert.match(data, /SERVER_LOCAL_API_ID/);
assert.match(data, /CLIENT_REPLICA_API_ID/);
assert.match(data, /EVENT_RUNTIME_SNAPSHOT_READY/);

assert.match(provider, /Session\.IsServer[\s\S]*SERVER_LOCAL_API_ID/);
assert.match(provider, /if \(Session\.IsClient\)[\s\S]*CLIENT_REPLICA_API_ID/);
assert.doesNotMatch(provider, /else if \(Session\.IsClient\)/);

assert.doesNotMatch(clientFactory, /case ApiMethodId\.SetFriction/);
assert.doesNotMatch(clientFactory, /case ApiMethodId\.ClearFriction/);
assert.match(clientFactory, /RuntimeStateStore\.TryGetByGrid/);
assert.match(
  clientFactory,
  /ClientReplicaMethodFactory\(methodId\)[\s\S]*clientRead == null[\s\S]*ServerMethodFactory\(methodId\)/,
);
assert.match(serverFactory, /case ApiMethodId\.SetFrictionEnabledForGroup/);
assert.match(serverFactory, /ApiCapabilityData\.RuntimeMutations/);

assert.ok(
  clientConfig.indexOf("ModAPI.MarkConfigReady(true)") <
    clientConfig.indexOf("ModAPI.BroadcastConfigReceived()"),
  "config readiness must be updated before ConfigReceived is broadcast",
);
assert.match(runtimeConsumer, /ModAPI\.MarkRuntimeSnapshotReady\(sequence, snapshotRevision\)/);

assert.match(wrapper, /class ShipCoreFrameworkClientApi/);
assert.match(wrapper, /class ShipCoreFrameworkServerApi/);
assert.match(wrapper, /RuntimeSnapshotReady = false;[\s\S]*ConfigReceived/);

console.log("API v4 role/readiness contract checks passed.");
