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
assert.match(workflow, /group: steam-workshop-upload/);
assert.match(workflow, /max-parallel: 1/);
assert.match(workflow, /needs: build/);
assert.match(workflow, /SE_DS_APPID: "298740"/);
assert.match(workflow, /uses: actions\/cache@v5/);
assert.match(workflow, /key: ds64-linux-\$\{\{ steps\.dsbuild\.outputs\.buildid \}\}/);
assert.match(workflow, /steamcmd\.sh" \+quit \|\| true/);
assert.match(workflow, /for attempt in 1 2 3 4 5/);
assert.match(workflow, /app_update "\$SE_DS_APPID" validate/);
assert.match(workflow, /DedicatedServer64\/SpaceEngineersDedicated\.exe/);
assert.match(workflow, /test -f ds64\/Sandbox\.Game\.dll/);
assert.match(workflow, /dotnet build ShipCoreSystem\.sln --configuration Release/);
assert.match(workflow, /SE_DS_DIR="\$RUNNER_TEMP\/space-engineers-dedicated"/);
assert.match(workflow, /binarypath = %s\\n' "\$GITHUB_WORKSPACE\/ds64"/);
assert.doesNotMatch(workflow, /\$\{\{ runner\.temp \}\}/);
assert.match(
  workflow,
  /SBMI_PROFILE: \$\{\{ github\.event_name == 'workflow_dispatch' && inputs\.sbmi_profile \|\| 'beta' \}\}/,
);
assert.doesNotMatch(workflow, /github\.event\.inputs\.sbmi_profile \|\| 'main'/);
assert.match(workflow, /matrix\.workshop_dir/);
assert.match(workflow, /\.github\/workflows\/steam-workshop-upload\.yml/);
assert.match(workflow, /\("title", title\)/);
assert.match(workflow, /\("description", description\)/);
assert.match(workflow, /description_bytes >= 8000/);
assert.doesNotMatch(workflow, /replace\("\\n", "\\\\n"\)/);
assert.doesNotMatch(workflow, /GetPublishedFileDetails\/v1\//);

console.log("Workshop publishing contract checks passed.");
