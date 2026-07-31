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

assert.doesNotMatch(hud, /SCFApi|API_ID|RegisterMessageHandler/);
assert.match(hud, /grid\?\.GetGroupComponent\(\)/);
assert.match(hud, /group\.HasRuntimeState/);
assert.match(hud, /group\.Limits\.TryGetValue/);
assert.match(hud, /group\.EffectiveSpeedLimitMetersPerSecond/);
assert.match(hud, /MenuKeybindInput/);
assert.match(hud, /WriteFileInLocalStorage/);
assert.match(hud, /MyKeys\.NumPad0/);
assert.match(host, /_coreStatusHud\.OnHudReady\(\)/);
assert.match(host, /_coreStatusHud\?\.Update\(\)/);

console.log('core status HUD integration checks passed');
