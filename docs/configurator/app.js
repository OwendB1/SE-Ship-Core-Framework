const state = {
  schema: {},
  blockGroups: [],
  shipCores: []
};

const ids = (id) => document.getElementById(id);

function escapeXml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', '&quot;')
    .replaceAll("'", "&apos;");
}

function parseModConfigCs(source) {
  const classes = {};
  const classRegex = /public\s+class\s+(\w+)\s*\{([\s\S]*?)\n\s*\}/g;
  let classMatch;
  while ((classMatch = classRegex.exec(source)) !== null) {
    const className = classMatch[1];
    const body = classMatch[2];
    const fields = [];
    const fieldRegex = /\[XmlElement\("([^"]+)"\)\]\s*public\s+([^\s]+(?:\[\])?)\s+(\w+)/g;
    let fieldMatch;
    while ((fieldMatch = fieldRegex.exec(body)) !== null) {
      fields.push({ xmlName: fieldMatch[1], type: fieldMatch[2], name: fieldMatch[3] });
    }
    if (fields.length) {
      classes[className] = fields;
    }
  }
  return classes;
}

function setParserStatus(text) {
  ids("parserStatus").textContent = text;
}

function addBlockGroup(group = { name: "", blockTypes: [] }) {
  state.blockGroups.push(group);
  renderBlockGroups();
  renderShipCores();
}

function addShipCore(core) {
  state.shipCores.push(core ?? {
    subtypeId: "",
    uniqueName: "",
    mobilityType: "Both",
    maxBlocks: -1,
    maxMass: -1,
    maxPcu: -1,
    maxPerPlayer: -1,
    forceBroadcast: false,
    forceBroadcastRange: 2000,
    blockLimits: []
  });
  renderShipCores();
}

function blockTypeEditor(groupIdx, bt, btIdx) {
  return `<div class="row wrap">
    <input data-action="bt-type" data-g="${groupIdx}" data-i="${btIdx}" class="small" placeholder="TypeId" value="${escapeXml(bt.typeId)}" />
    <input data-action="bt-subtype" data-g="${groupIdx}" data-i="${btIdx}" class="small" placeholder="SubtypeId (or any)" value="${escapeXml(bt.subtypeId)}" />
    <input data-action="bt-weight" data-g="${groupIdx}" data-i="${btIdx}" class="small" type="number" step="0.1" value="${bt.countWeight}" />
    <button data-action="remove-bt" data-g="${groupIdx}" data-i="${btIdx}">Remove BlockType</button>
  </div>`;
}

function renderBlockGroups() {
  const container = ids("blockGroups");
  container.innerHTML = state.blockGroups.map((group, g) => `
    <div class="card">
      <h4>Group ${g + 1}</h4>
      <div class="row wrap">
        <input data-action="group-name" data-g="${g}" placeholder="Group Name" value="${escapeXml(group.name)}" />
        <button data-action="add-bt" data-g="${g}">Add BlockType</button>
        <button data-action="remove-group" data-g="${g}">Delete Group</button>
      </div>
      ${group.blockTypes.map((bt, i) => blockTypeEditor(g, bt, i)).join("")}
    </div>
  `).join("");
}

function blockGroupOptions(selected = []) {
  return state.blockGroups
    .filter((g) => g.name.trim())
    .map((g) => `<option value="${escapeXml(g.name)}" ${selected.includes(g.name) ? "selected" : ""}>${escapeXml(g.name)}</option>`)
    .join("");
}

function renderShipCores() {
  const container = ids("shipCores");
  container.innerHTML = state.shipCores.map((core, c) => `
    <div class="card">
      <h4>Core ${c + 1}</h4>
      <div class="row wrap">
        <input data-action="core-subtype" data-c="${c}" class="small" placeholder="SubtypeId" value="${escapeXml(core.subtypeId)}" />
        <input data-action="core-unique" data-c="${c}" class="small" placeholder="UniqueName" value="${escapeXml(core.uniqueName)}" />
        <select data-action="core-mobility" data-c="${c}" class="small">
          ${["Static", "Mobile", "Both"].map(v => `<option ${core.mobilityType===v?"selected":""}>${v}</option>`).join("")}
        </select>
        <button data-action="remove-core" data-c="${c}">Delete Core</button>
      </div>
      <div class="row wrap">
        <label class="inline">MaxBlocks <input data-action="core-maxblocks" data-c="${c}" class="small" type="number" value="${core.maxBlocks}" /></label>
        <label class="inline">MaxMass <input data-action="core-maxmass" data-c="${c}" class="small" type="number" value="${core.maxMass}" /></label>
        <label class="inline">MaxPCU <input data-action="core-maxpcu" data-c="${c}" class="small" type="number" value="${core.maxPcu}" /></label>
        <label class="inline">MaxPerPlayer <input data-action="core-maxpp" data-c="${c}" class="small" type="number" value="${core.maxPerPlayer}" /></label>
      </div>
      <div class="row wrap">
        <label class="inline">ForceBroadcast <input data-action="core-fb" data-c="${c}" type="checkbox" ${core.forceBroadcast ? "checked" : ""} /></label>
        <label class="inline">BroadcastRange <input data-action="core-fbr" data-c="${c}" class="small" type="number" value="${core.forceBroadcastRange}" /></label>
        <button data-action="add-limit" data-c="${c}">Add Block Limit</button>
      </div>
      ${core.blockLimits.map((limit, l) => `
        <div class="card">
          <div class="row wrap">
            <input data-action="limit-name" data-c="${c}" data-l="${l}" class="small" placeholder="Limit Name" value="${escapeXml(limit.name)}" />
            <label class="inline">MaxCount <input data-action="limit-max" data-c="${c}" data-l="${l}" class="small" type="number" step="0.1" value="${limit.maxCount}" /></label>
            <button data-action="remove-limit" data-c="${c}" data-l="${l}">Delete Limit</button>
          </div>
          <div class="row wrap">
            <label class="inline">PunishByNoFlyZone <input data-action="limit-punish" data-c="${c}" data-l="${l}" type="checkbox" ${limit.punishByNoFlyZone ? "checked" : ""}/></label>
            <select data-action="limit-type" data-c="${c}" data-l="${l}" class="small">
              ${["ShutOff", "Damage", "Delete", "Explode"].map(v => `<option ${limit.punishmentType===v?"selected":""}>${v}</option>`).join("")}
            </select>
            <input data-action="limit-dir" data-c="${c}" data-l="${l}" class="small" placeholder="AllowedDirections csv (Forward,Up)" value="${escapeXml((limit.allowedDirections || []).join(","))}" />
          </div>
          <div>
            <label>Reusable Block Groups</label>
            <select data-action="limit-groups" data-c="${c}" data-l="${l}" multiple>
              ${blockGroupOptions(limit.blockGroups)}
            </select>
          </div>
        </div>
      `).join("")}
    </div>
  `).join("");
}

function generateXml() {
  const header = '<?xml version="1.0" encoding="UTF-8"?>';

  const groups = `${header}\n<ArrayOfBlockGroup>\n${state.blockGroups.map((g) => `  <BlockGroup>\n    <Name>${escapeXml(g.name)}</Name>\n${g.blockTypes.map((bt) => `    <BlockTypes>\n      <TypeId>${escapeXml(bt.typeId)}</TypeId>\n      <SubtypeId>${escapeXml(bt.subtypeId || "")}</SubtypeId>\n      <CountWeight>${bt.countWeight}</CountWeight>\n    </BlockTypes>`).join("\n")}\n  </BlockGroup>`).join("\n")}\n</ArrayOfBlockGroup>`;

  const manifestFiles = state.shipCores
    .filter((core) => core.subtypeId.trim())
    .map((core) => `Data/Cores/${core.subtypeId}.xml`);

  const manifest = `${header}\n<CoreManifest>\n${manifestFiles.map((f) => `  <ShipCoreFilenames>${escapeXml(f)}</ShipCoreFilenames>`).join("\n")}\n</CoreManifest>`;

  const cores = state.shipCores.map((core) => {
    const body = `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(core.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(core.uniqueName)}</UniqueName>\n  <ForceBroadCast>${core.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${core.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(core.mobilityType)}</MobilityType>\n  <MaxBlocks>${core.maxBlocks}</MaxBlocks>\n  <MaxMass>${core.maxMass}</MaxMass>\n  <MaxPCU>${core.maxPcu}</MaxPCU>\n  <MaxPerPlayer>${core.maxPerPlayer}</MaxPerPlayer>\n${core.blockLimits.map((limit) => `  <BlockLimits>\n    <Name>${escapeXml(limit.name)}</Name>\n${limit.blockGroups.map((g) => `    <BlockGroups>${escapeXml(g)}</BlockGroups>`).join("\n")}\n    <MaxCount>${limit.maxCount}</MaxCount>\n    <PunishByNoFlyZone>${limit.punishByNoFlyZone}</PunishByNoFlyZone>\n    <PunishmentType>${escapeXml(limit.punishmentType)}</PunishmentType>\n${(limit.allowedDirections || []).filter(Boolean).map((d) => `    <AllowedDirections>${escapeXml(d)}</AllowedDirections>`).join("\n")}\n  </BlockLimits>`).join("\n")}\n</ShipCore>`;
    return { file: `${core.subtypeId || "UnnamedCore"}.xml`, body };
  });

  ids("groupsXml").textContent = groups;
  ids("manifestXml").textContent = manifest;
  ids("coresXml").textContent = cores.map((c) => `===== ${c.file} =====\n${c.body}`).join("\n\n");

  return { groups, manifest, cores };
}

function download(filename, content) {
  const blob = new Blob([content], { type: "application/xml" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

document.addEventListener("click", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const g = Number(target.dataset.g);
  const i = Number(target.dataset.i);
  const c = Number(target.dataset.c);
  const l = Number(target.dataset.l);

  if (action === "add-bt") {
    state.blockGroups[g].blockTypes.push({ typeId: "", subtypeId: "", countWeight: 1 });
    renderBlockGroups();
  } else if (action === "remove-bt") {
    state.blockGroups[g].blockTypes.splice(i, 1);
    renderBlockGroups();
  } else if (action === "remove-group") {
    state.blockGroups.splice(g, 1);
    renderBlockGroups();
    renderShipCores();
  } else if (action === "remove-core") {
    state.shipCores.splice(c, 1);
    renderShipCores();
  } else if (action === "add-limit") {
    state.shipCores[c].blockLimits.push({ name: "", maxCount: 0, punishByNoFlyZone: false, punishmentType: "ShutOff", allowedDirections: [], blockGroups: [] });
    renderShipCores();
  } else if (action === "remove-limit") {
    state.shipCores[c].blockLimits.splice(l, 1);
    renderShipCores();
  }
});

document.addEventListener("input", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const g = Number(target.dataset.g);
  const i = Number(target.dataset.i);
  const c = Number(target.dataset.c);
  const l = Number(target.dataset.l);

  if (action === "group-name") state.blockGroups[g].name = target.value;
  if (action === "bt-type") state.blockGroups[g].blockTypes[i].typeId = target.value;
  if (action === "bt-subtype") state.blockGroups[g].blockTypes[i].subtypeId = target.value;
  if (action === "bt-weight") state.blockGroups[g].blockTypes[i].countWeight = Number(target.value || 0);

  if (action === "core-subtype") state.shipCores[c].subtypeId = target.value;
  if (action === "core-unique") state.shipCores[c].uniqueName = target.value;
  if (action === "core-maxblocks") state.shipCores[c].maxBlocks = Number(target.value || -1);
  if (action === "core-maxmass") state.shipCores[c].maxMass = Number(target.value || -1);
  if (action === "core-maxpcu") state.shipCores[c].maxPcu = Number(target.value || -1);
  if (action === "core-maxpp") state.shipCores[c].maxPerPlayer = Number(target.value || -1);
  if (action === "core-fbr") state.shipCores[c].forceBroadcastRange = Number(target.value || 0);

  if (action === "limit-name") state.shipCores[c].blockLimits[l].name = target.value;
  if (action === "limit-max") state.shipCores[c].blockLimits[l].maxCount = Number(target.value || 0);
  if (action === "limit-dir") state.shipCores[c].blockLimits[l].allowedDirections = target.value.split(",").map((v) => v.trim()).filter(Boolean);
});

document.addEventListener("change", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;

  if (action === "core-fb") {
    state.shipCores[Number(target.dataset.c)].forceBroadcast = target.checked;
  }
  if (action === "core-mobility") {
    state.shipCores[Number(target.dataset.c)].mobilityType = target.value;
  }
  if (action === "limit-punish") {
    state.shipCores[Number(target.dataset.c)].blockLimits[Number(target.dataset.l)].punishByNoFlyZone = target.checked;
  }
  if (action === "limit-type") {
    state.shipCores[Number(target.dataset.c)].blockLimits[Number(target.dataset.l)].punishmentType = target.value;
  }
  if (action === "limit-groups") {
    const core = state.shipCores[Number(target.dataset.c)];
    const limit = core.blockLimits[Number(target.dataset.l)];
    limit.blockGroups = Array.from(target.selectedOptions).map((o) => o.value);
  }

  const fileInput = ids("modConfigFile");
  if (target === fileInput) {
    const file = fileInput.files?.[0];
    if (!file) return;
    file.text().then((text) => {
      ids("modConfigInput").value = text;
      const schema = parseModConfigCs(text);
      state.schema = schema;
      ids("schemaPreview").textContent = JSON.stringify(schema, null, 2);
      setParserStatus(`Parsed ${Object.keys(schema).length} classes from ${file.name}.`);
    });
  }
});

ids("addGroup").addEventListener("click", () => addBlockGroup({ name: "", blockTypes: [] }));
ids("addCore").addEventListener("click", () => addShipCore());

ids("loadBundled").addEventListener("click", async () => {
  const response = await fetch("./assets/ModConfig.cs");
  const text = await response.text();
  ids("modConfigInput").value = text;
  state.schema = parseModConfigCs(text);
  ids("schemaPreview").textContent = JSON.stringify(state.schema, null, 2);
  setParserStatus(`Parsed ${Object.keys(state.schema).length} classes from bundled ModConfig.cs.`);
});

ids("generateXml").addEventListener("click", () => generateXml());
ids("downloadGroups").addEventListener("click", () => {
  const xml = generateXml();
  download("ShipCoreConfig_Groups.xml", xml.groups);
});
ids("downloadManifest").addEventListener("click", () => {
  const xml = generateXml();
  download("ShipCoreConfig_Manifest.xml", xml.manifest);
});
ids("downloadCores").addEventListener("click", () => {
  const xml = generateXml();
  const content = xml.cores.map((c) => `### ${c.file}\n${c.body}`).join("\n\n");
  download("ShipCore_Cores_Bundle.txt", content);
});

addBlockGroup({ name: "Weaponry", blockTypes: [{ typeId: "SmallGatlingGun", subtypeId: "", countWeight: 1 }] });
addShipCore({
  subtypeId: "Fighter_Core",
  uniqueName: "Fighter Grid Class",
  mobilityType: "Mobile",
  maxBlocks: 1000,
  maxMass: -1,
  maxPcu: 6000,
  maxPerPlayer: 10,
  forceBroadcast: true,
  forceBroadcastRange: 2000,
  blockLimits: [{
    name: "Weapons",
    maxCount: 8,
    punishByNoFlyZone: true,
    punishmentType: "Delete",
    allowedDirections: ["Forward"],
    blockGroups: ["Weaponry"]
  }]
});

generateXml();
