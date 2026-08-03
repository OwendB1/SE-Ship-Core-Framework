import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [enforcement, lifecycle, abilities, speedState, state, config, worldSettings, definitions, commands,
  apiData, api, snapshot, contracts, replica, clientTick] =
  await Promise.all([
    read("Server/Enforcement/SpeedEnforcement.cs"),
    read("Server/Components/GroupComponent.Lifecycle.cs"),
    read("Server/Components/GroupComponent.Abilities.cs"),
    read("Server/Components/GroupComponent.SpeedState.cs"),
    read("Server/Components/GroupComponent.State.cs"),
    read("Config/ModConfig.XmlModels.cs"),
    read("Config/ModConfig.WorldSettings.cs"),
    read("Session/Session.Definitions.cs"),
    read("Server/Commands/Commands.Administration.cs"),
    read("API/ApiData.cs"),
    read("API/ModAPI.cs"),
    read("Server/Components/GroupComponent.Snapshot.cs"),
    read("Shared/Network/RuntimeStateContracts.cs"),
    read("Client/Components/GroupComponent.Replica.cs"),
    read("Session/Client/Session.ClientTick.cs"),
  ]);

assert.match(state, /SpeedEnforcementDeferred = true/);
assert.match(lifecycle, /MainCoreComponent = coreComponent;[\s\S]*SpeedEnforcementDeferred = false/);
assert.match(lifecycle, /confirmedAbsent[\s\S]*SpeedEnforcementDeferred = false/);
assert.match(enforcement, /if \(context\.SourceGroup\.SpeedEnforcementDeferred\) return/);
assert.match(enforcement, /baseMaxSpeed = worldSpeedLimit/);

assert.ok((lifecycle.match(/BeginSpeedRampDown\(\)/g)?.length ?? 0) >= 2);
assert.match(speedState, /SpeedRampDownCap = currentCap/);
assert.ok(
  (enforcement.match(/RampDownSpeedCap\(/g)?.length ?? 0) >= 3,
  "boost and core-loss paths must share the extracted ramp helper",
);
assert.match(enforcement, /SpeedRampDownIntervalTicks = 5/);
assert.match(enforcement, /_speedRampDownLerpStep = speedRampDownPercentage \/ 12f \* 0\.01f/);
assert.match(enforcement, /if \(sourceGroup\.SpeedRampDownActive\)/);
assert.doesNotMatch(enforcement, /MainCoreComponent == null && sourceGroup\.SpeedRampDownActive/);
assert.match(speedState, /private void BeginSpeedRampDown\(\)/);
assert.match(
  abilities,
  /if \(!previousPunishSpeed && PunishSpeed\)\s*BeginSpeedRampDown\(\)/,
);
assert.match(abilities, /ChainPunishmentGate\(ref punishments, HasBrokenMainCore\(\), GroupPunishmentFlags\.Both\)/);
assert.match(enforcement, /MathHelper\.Lerp\(currentCap, targetCap, lerpAmount\)/);
assert.doesNotMatch(enforcement, /Math\.Pow\(1f - percentage, elapsedPeriods\)/);
assert.match(definitions, /CacheSpeedRampDownStep\(Config\.SpeedRampDownPercentage\)/);
assert.match(commands, /ReloadConfig\(\)[\s\S]*ApplyConfigToDefinitions\(\)/);

assert.match(enforcement, /internal static float InterpolateSpeedRampDownCap/);
assert.match(enforcement, /if \(nextCap >= currentCap\)[\s\S]*return targetCap/);
assert.match(enforcement, /SpeedRampDownActive = speedRampDownActive/);
assert.match(enforcement, /EffectiveSpeedRampDownActive = context\.SpeedRampDownActive/);
assert.match(enforcement, /EffectiveSpeedRampDownTargetCap = context\.SpeedRampDownTarget/);
assert.match(
  enforcement,
  /rampStateChanged \|\| otherSpeedStateChanged \|\|\s*effectiveSpeedChanged && !context\.SpeedRampDownActive/,
  "cap-only ramp steps must not dirty the full runtime state",
);
assert.match(contracts, /ProtoMember\(57\)\]\s*internal bool SpeedRampDownActive/);
assert.match(contracts, /ProtoMember\(58\)\]\s*internal float SpeedRampDownTarget/);
assert.match(snapshot, /SpeedRampDownActive = speedRampDownActive/);
assert.match(snapshot, /SpeedRampDownTarget = speedRampDownTarget/);
assert.match(replica, /SpeedEnforcement\.InterpolateSpeedRampDownCap\(/);
assert.match(replica, /_runtimeSpeedRampDownLastTick = Session\.CurrentTick/);
assert.match(clientTick, /CurrentTick % SpeedEnforcement\.SpeedRampDownIntervalTicks == 0/);
assert.match(clientTick, /RunRuntimeSpeedRampDownTick\(\)/);

assert.match(config, /XmlElement\("SpeedRampDownPercentage"\)/);
assert.match(worldSettings, /SpeedRampDownPercentage < 0f \|\| import\.SpeedRampDownPercentage > 100f/);
assert.match(apiData, /ProtoMember\(23\)\]\s*public float SpeedRampDownPercentage/);
assert.match(api, /SpeedRampDownPercentage = config\.SpeedRampDownPercentage/);

console.log("Speed enforcement transition contract checks passed.");
