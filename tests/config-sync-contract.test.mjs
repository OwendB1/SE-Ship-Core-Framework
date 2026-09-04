import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [fingerprint, loading, contracts, client, server, sessionRun, configSync, cooldown,
  commands] = await Promise.all([
  read("Config/ModConfig.ContentFingerprint.cs"),
  read("Config/ModConfig.Loading.cs"),
  read("Shared/Network/DirectionalPacketContracts.cs"),
  read("Client/Network/PresentationPacketHandlers.cs"),
  read("Session/Server/Session.ServerServices.cs"),
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

assert.match(contracts, /class PacketSendConfig[\s\S]*ContentFingerprint/);

const compare = client.indexOf("!string.Equals(ContentFingerprint, localFingerprint");
const apply = client.indexOf("Session.Config.ApplyWorldSettingsFrom(import)");
assert.ok(compare >= 0 && apply > compare, "fingerprint must be checked before applying server settings");
assert.match(client, /Revision <= Session\.AppliedConfigRevision/);

assert.match(server, /ConfigRevision\+\+/);
assert.match(server, /p\.SteamUserId == localSteamId/);
assert.match(server, /SelectedNoCore != null && !RuntimeInitialized/);

assert.ok(
  sessionRun.indexOf("RunConfigSyncTick()") <
    sessionRun.indexOf("if (!ConfigSyncReady) return"),
  "config retry must run before the runtime-ready guard",
);
assert.match(configSync, /ConfigSyncRetryIntervalTicks = 5 \* 60/);
assert.match(configSync, /ConfigSyncReady \|\| _configSyncCountdown < 0/);
assert.match(cooldown, /DateTime\.UtcNow\.Ticks/);
assert.doesNotMatch(cooldown, /ConfigRequestTicks/);
assert.match(commands, /RefreshGroupsAfterConfigChanged\(\);[\s\S]*BroadcastConfigToClients\(\)/);
assert.match(commands, /CombatLogging = \(clVal == "on"\);[\s\S]*SaveConfig\(true\)/);
assert.match(commands, /MaxPossibleSpeedMetersPerSecond = newSpeed;[\s\S]*BroadcastConfigToClients\(\)/);

console.log("Configuration synchronization contract checks passed.");
