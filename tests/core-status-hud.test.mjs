import assert from 'node:assert/strict';
import fs from 'node:fs';

const hud = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/UI/CoreStatusHud.cs',
  'utf8',
);
const host = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/UI/BuildPreviewHud.cs',
  'utf8',
);
const abilities = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Shared/Components/GroupComponent.Abilities.cs',
  'utf8',
);
const replica = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/Components/GroupComponent.Replica.cs',
  'utf8',
);
const commands = fs.readFileSync(
  'ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/Client/UI/Commands.Presentation.cs',
  'utf8',
);

assert.doesNotMatch(hud, /SCFApi|API_ID|RegisterMessageHandler/);
assert.match(hud, /grid\?\.GetGroupComponent\(\)/);
assert.match(hud, /group\.HasRuntimeState/);
assert.match(hud, /group\.Limits\.TryGetValue/);
assert.match(hud, /group\.EffectiveSpeedLimitMetersPerSecond/);
assert.match(hud, /AppendAbility\("Boost"/);
assert.match(hud, /AppendAbility\("Active Defense"/);
assert.match(hud, /AppendAbility\("Power Increase"/);
assert.match(hud, /AbilityRefreshIntervalUpdates/);
assert.match(abilities, /GetAbilityTimers/);
assert.match(abilities, /Session\.CurrentTick - _runtimeAbilityStateTick/);
assert.match(replica, /_runtimeAbilityStateTick = Session\.CurrentTick/);
assert.match(hud, /MenuKeybindInput/);
assert.match(hud, /private bool _enabled;/);
assert.match(hud, /private int _infoLevel = 2;/);
assert.match(hud, /ReadFileInLocalStorage/);
assert.match(hud, /pair\[0\] == "Enabled".*_enabled = flag/);
assert.match(hud, /pair\[0\] == "Level".*int\.TryParse/s);
assert.match(hud, /WriteLine\("Enabled=" \+ _enabled\)/);
assert.match(hud, /WriteLine\("Level=" \+ _infoLevel/);
assert.match(hud, /WriteFileInLocalStorage/);
assert.match(hud, /MyKeys\.NumPad0/);
assert.match(hud, /TryParseInfoLevel/);
assert.match(hud, /"standard".*level = 1/s);
assert.match(hud, /"detailed".*level = 2/s);
assert.match(hud, /"full".*level = 3/s);
assert.match(hud, /int infoLevel = cockpit \? 0 : _infoLevel/);
assert.match(hud, /cockpit != _lastCockpit/);
assert.match(hud, /if \(infoLevel >= 1\).*AppendUsage/s);
assert.match(hud, /if \(infoLevel >= 1\) AppendLimits/);
assert.match(hud, /AppendSpeed\(grid, group, infoLevel\);/);
assert.match(hud, /AppendPunishmentWarning/);
assert.match(hud, /if \(infoLevel >= 2\) AppendCoreLimits/);
assert.match(hud, /if \(infoLevel >= 3\).*AppendEnforcement/s);
assert.match(hud, /AppendModifiers\(group\)/);
assert.match(commands, /\/corehud <1-3\|standard\|detailed\|full>/);
assert.match(commands, /\/corehud level <1-3\|standard\|detailed\|full>/);
assert.match(commands, /\/core inventory/);
assert.match(host, /_coreStatusHud\.OnHudReady\(\)/);
assert.match(host, /_coreStatusHud\?\.Update\(\)/);

console.log('core status HUD integration checks passed');
