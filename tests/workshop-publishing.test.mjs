import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL("../", import.meta.url);
const read = path => readFile(new URL(path, root), "utf8");
const semver = /^\d+\.\d+\.\d+$/;

for (const mod of ["ShipCoreFramework", "ArcaneShipCores"]) {
  const [titleFile, versionFile, descriptionFile] = await Promise.all([
    read(`${mod}/workshop/title.txt`),
    read(`${mod}/workshop/version.txt`),
    read(`${mod}/workshop/description.bbcode`),
  ]);
  const title = titleFile.trim();
  const version = versionFile.trim();
  const description = descriptionFile.replace(/\r\n/g, "\n").trimEnd();

  assert.ok(title && !/[\r\n]/.test(title), `${mod} title must be one line`);
  assert.match(version, semver, `${mod} version must be semantic`);
  assert.ok(Buffer.byteLength(`${title} (${version})`) <= 128);
  assert.ok(Buffer.byteLength(description) < 8000);
  assert.match(description, /^\[img]https:\/\/i\.imgur\.com\/fVVaDCS\.gif\[\/img]/);
}

const workflow = await read(".github/workflows/steam-workshop-upload.yml");
assert.match(workflow, /default: main/);
assert.match(workflow, /matrix\.workshop_dir/);
assert.match(workflow, /\("title", title\)/);
assert.match(workflow, /\("description", description\)/);
assert.match(workflow, /description_bytes >= 8000/);
assert.doesNotMatch(workflow, /GetPublishedFileDetails\/v1\//);

console.log("Workshop publishing contract checks passed.");
