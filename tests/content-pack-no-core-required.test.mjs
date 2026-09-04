import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [config, loading, sessionRun, serverLifecycle, clientConfig, persistence, commands] =
  await Promise.all([
    read("Config/ModConfig.cs"),
    read("Config/ModConfig.Loading.cs"),
    read("Session/Session.Run.cs"),
    read("Session/Server/Session.ServerLifecycle.cs"),
    read("Client/Network/PresentationPacketHandlers.cs"),
    read("Config/Server/ModConfig.Persistence.cs"),
    read("Server/Commands/Commands.Administration.cs"),
  ]);

await assert.rejects(access(new URL("Config/DefaultNoCoreConfig.cs", root)));
assert.doesNotMatch(config, /DefaultNoCoreConfig|_defaultNoCore/);
assert.doesNotMatch(loading, /Falling back to default|_defaultNoCore/);
assert.match(loading, /internal bool ResolveSelectedNoCore\(bool logFailure = true\)/);
assert.match(loading, /RetiredDefaultNoCoreUniqueName = "DEFAULT-NO-CORE-ALL-GRID-TYPES"/);
assert.match(loading, /NoCoreConfigs\.Count == 1[\s\S]*SelectedNoCore = NoCoreConfigs\[0\]/);
assert.match(loading, /The retired built-in no-core profile is still selected/);
assert.match(loading, /No content-pack no-core profile is selected/);
assert.match(sessionRun, /if \(_runtimeInitialized \|\| Config\?\.SelectedNoCore == null\) return false/);
assert.doesNotMatch(sessionRun, /NotifyMissingNoCore/);
assert.match(serverLifecycle, /RegisterSecureMessageHandler[\s\S]*if \(Config\.SelectedNoCore != null\)/);
assert.match(clientConfig, /ResolveSelectedNoCore\(\)[\s\S]*TryInitializeRuntime\(\)/);
assert.match(persistence, /bool broadcast = true/);
assert.match(commands, /if \(!Session\.RuntimeInitialized\)[\s\S]*Reload the world to start Ship Core Framework/);
assert.match(commands, /if \(loadedConfig\.SelectedNoCore == null\)[\s\S]*Config reload rejected/);

console.log("Content-pack no-core requirement checks passed.");
