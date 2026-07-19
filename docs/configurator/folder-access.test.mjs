import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appSource = readFileSync(new URL("./app.js", import.meta.url), "utf8");
const helperStart = appSource.indexOf("function getUpdateEntryPath");
const helperEnd = appSource.indexOf("\n\nfunction generatedXmlEntries", helperStart);
const { getUpdateEntryPath, writeEntriesToFolder } = Function(
  `${appSource.slice(helperStart, helperEnd)}; return { getUpdateEntryPath, writeEntriesToFolder };`
)();

test("maps generated files from mod roots and Data folders", () => {
  assert.equal(getUpdateEntryPath("ShipCoreConfig_Manifest.xml", false), "Data/ShipCoreConfig_Manifest.xml");
  assert.equal(getUpdateEntryPath("Data/Cores/Frigate.xml", false), "Data/Cores/Frigate.xml");
  assert.equal(getUpdateEntryPath("ShipCoreConfig_Manifest.xml", true), "ShipCoreConfig_Manifest.xml");
  assert.equal(getUpdateEntryPath("Data/Cores/Frigate.xml", true), "Cores/Frigate.xml");
});

test("rejects paths that escape the selected folder", () => {
  assert.throws(() => getUpdateEntryPath("../Frigate.xml", false), /Unsafe generated path/);
  assert.throws(() => getUpdateEntryPath("Other/Frigate.xml", true), /outside Data/);
});

test("writes generated content into created subdirectories", async () => {
  const written = new Map();
  const directory = (path = "") => ({
    async getDirectoryHandle(name) { return directory(`${path}${name}/`); },
    async getFileHandle(name) {
      return {
        async createWritable() {
          return {
            async write(content) { written.set(`${path}${name}`, content); },
            async close() {}
          };
        }
      };
    }
  });

  await writeEntriesToFolder(directory(), [{ name: "Data/Cores/Frigate.xml", content: "<ShipCore />" }], false);
  assert.equal(written.get("Data/Cores/Frigate.xml"), "<ShipCore />");
});
