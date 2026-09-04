import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [fingerprint, loading, contracts, packetBase, networking, client, server, handlers,
  sessionRun, configSync, cooldown, commands] = await Promise.all([
  read("Config/ModConfig.ContentFingerprint.cs"),
  read("Config/ModConfig.Loading.cs"),
  read("Shared/Network/DirectionalPacketContracts.cs"),
  read("Shared/Network/PacketContracts.cs"),
  read("Shared/Network/Networking.cs"),
  read("Client/Network/PresentationPacketHandlers.cs"),
  read("Session/Server/Session.ServerServices.cs"),
  read("Server/Network/CommandPacketHandlers.cs"),
  read("Session/Session.Run.cs"),
  read("Session/Session.ConfigSync.cs"),
  read("Session/Server/Session.RuntimeStatePublisher.cs"),
  read("Server/Commands/Commands.Administration.cs"),
]);

assert.match(fingerprint, /_contentFingerprintInputs\.Sort\(StringComparer\.Ordinal\)/);
assert.match(fingerprint, /14695981039346656037UL/);
assert.match(fingerprint, /1099511628211UL/);
assert.match(loading, /FinalizeContentFingerprint\(\)/);
assert.equal((loading.match(/TrackContentFile\(/g) || []).length, 5);

assert.match(packetBase, /ProtoInclude\(12000, typeof\(PacketConfigAck\)\)/);
assert.match(contracts, /class PacketRequestConfig[\s\S]*KnownRevision[\s\S]*KnownContentFingerprint/);
assert.match(contracts, /class PacketSendConfig[\s\S]*ContentFingerprint[\s\S]*ServerRuntimeReady/);
assert.match(contracts, /class PacketConfigAck[\s\S]*bool Applied/);

assert.match(networking, /internal bool SendToPlayer/);
assert.match(networking, /bytes\.Length > MaxPacketBytes/);
assert.match(networking, /SendMessageTo\(_channelId, bytes, steamId\)\) return true/);

const compare = client.indexOf("!string.Equals(ContentFingerprint, localFingerprint");
const apply = client.indexOf("Session.Config.ApplyWorldSettingsFrom(import)");
assert.ok(compare >= 0 && apply > compare, "fingerprint must be checked before applying server settings");
assert.match(client, /Session\.RejectConfigSync[\s\S]*Session\.SendConfigAck/);
assert.match(client, /Revision < Session\.AppliedConfigRevision/);

assert.match(server, /AdvanceConfigRevision\(\)/);
assert.match(server, /p\.SteamUserId == localSteamId/);
assert.match(server, /knownRevision == ConfigRevision[\s\S]*unchanged: true/);
assert.match(server, /configuration exceeds the synchronization size limit/);
assert.match(handlers, /class PacketConfigAck[\s\S]*rejected config revision/);

assert.ok(
  sessionRun.indexOf("RunConfigSyncTick()") <
    sessionRun.indexOf("if (!ConfigSyncReady) return"),
  "config retry must run before the runtime-ready guard",
);
assert.match(configSync, /ConfigSyncRetryIntervalTicks = 5 \* 60/);
assert.match(configSync, /ConfigSyncPollIntervalTicks = 30 \* 60/);
assert.match(cooldown, /DateTime\.UtcNow\.Ticks/);
assert.doesNotMatch(cooldown, /ConfigRequestTicks/);
assert.match(commands, /RefreshGroupsAfterConfigChanged\(\);[\s\S]*BroadcastConfigToClients\(\)/);
assert.match(commands, /CombatLogging = \(clVal == "on"\);[\s\S]*SaveConfig\(true\)/);
assert.match(commands, /MaxPossibleSpeedMetersPerSecond = newSpeed;[\s\S]*BroadcastConfigToClients\(\)/);

console.log("Configuration synchronization contract checks passed.");
