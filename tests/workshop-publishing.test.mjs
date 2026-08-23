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

const sharedWorkflow = await read(".github/workflows/steam-workshop-upload.yml");
const dedicatedWorkflows = [
  {
    contents: await read(".github/workflows/arcane-cores-workshop-upload.yml"),
    modDir: "ArcaneShipCores",
    otherModDir: "ShipCoreFramework",
  },
  {
    contents: await read(".github/workflows/shipcore-framework-workshop-upload.yml"),
    modDir: "ShipCoreFramework",
    otherModDir: "ArcaneShipCores",
  },
];

for (const { contents, modDir, otherModDir } of dedicatedWorkflows) {
  assert.match(contents, /group: steam-workshop-upload/);
  assert.match(contents, /default: main/);
  assert.ok(contents.includes(`- ${modDir}/**`));
  assert.ok(!contents.includes(otherModDir));
  assert.match(contents, /uses: \.\/\.github\/workflows\/steam-workshop-upload\.yml/);
  assert.match(contents, new RegExp(`mod_dir: ${modDir}`));
  assert.match(
    contents,
    /sbmi_profile: \$\{\{ github\.event_name == 'workflow_dispatch' && inputs\.sbmi_profile \|\| 'beta' \}\}/,
  );
  assert.match(contents, /secrets: inherit/);
}

assert.match(sharedWorkflow, /workflow_call:/);
assert.match(sharedWorkflow, /needs: build/);
assert.match(sharedWorkflow, /SE_DS_APPID: "298740"/);
assert.match(sharedWorkflow, /uses: actions\/cache@v5/);
assert.match(sharedWorkflow, /key: ds64-linux-\$\{\{ steps\.dsbuild\.outputs\.buildid \}\}/);
assert.match(sharedWorkflow, /steamcmd\.sh" \+quit \|\| true/);
assert.match(sharedWorkflow, /for attempt in 1 2 3 4 5/);
assert.match(sharedWorkflow, /app_update "\$SE_DS_APPID" validate/);
assert.match(sharedWorkflow, /DedicatedServer64\/SpaceEngineersDedicated\.exe/);
assert.match(sharedWorkflow, /test -f ds64\/Sandbox\.Game\.dll/);
assert.match(sharedWorkflow, /dotnet build ShipCoreSystem\.sln --configuration Release/);
assert.match(sharedWorkflow, /SE_DS_DIR="\$RUNNER_TEMP\/space-engineers-dedicated"/);
assert.match(sharedWorkflow, /binarypath = %s\\n' "\$GITHUB_WORKSPACE\/ds64"/);
assert.match(sharedWorkflow, /SBMI_PROFILE: \$\{\{ inputs\.sbmi_profile \}\}/);
assert.match(sharedWorkflow, /\$\{\{ inputs\.workshop_dir \}\}\/title\.txt/);
assert.doesNotMatch(sharedWorkflow, /matrix\./);
assert.doesNotMatch(sharedWorkflow, /\$\{\{ runner\.temp \}\}/);
assert.match(sharedWorkflow, /\("title", title\)/);
assert.match(sharedWorkflow, /\("description", description\)/);
assert.match(sharedWorkflow, /description_bytes >= 8000/);
assert.doesNotMatch(sharedWorkflow, /replace\("\\n", "\\\\n"\)/);
assert.doesNotMatch(sharedWorkflow, /GetPublishedFileDetails\/v1\//);

console.log("Workshop publishing contract checks passed.");
