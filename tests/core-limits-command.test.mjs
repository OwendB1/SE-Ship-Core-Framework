import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL(
  "../ShipCoreFramework/src/Data/Scripts/ShipCoreFramework/",
  import.meta.url,
);
const read = path => readFile(new URL(path, root), "utf8");

const [dispatch, chat, presentation] = await Promise.all([
  read("Shared/Commands/Commands.Dispatch.cs"),
  read("Client/UI/Commands.Chat.cs"),
  read("Client/UI/Commands.Presentation.cs"),
]);

assert.match(dispatch, /sub\.Equals\("limits", StringComparison\.OrdinalIgnoreCase\)/);
assert.match(chat, /case "limits":[\s\S]*CoreLimits\(playerId\)/);
assert.match(presentation, /GetCoreLimits\(IMyCubeGrid targetGrid/);
assert.match(presentation, /Utils\.GetLocalizedBlockName\(block\)/);
assert.match(presentation, /Dictionary<string, Dictionary<double, int>>/);
assert.doesNotMatch(presentation, /Tuple</);
assert.match(presentation, /Append\("pt x"\)/);
assert.match(presentation, /Append\("\) \| "\)/);
assert.match(presentation, /\/core limits[\s\S]*per-limit block usage by display name/);
assert.doesNotMatch(
  presentation,
  /GetCoreLimits\(IMyCubeGrid targetGrid[\s\S]*blockType\.(?:TypeId|SubtypeId)/,
);

console.log("Core limits command contract checks passed.");
