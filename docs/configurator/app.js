const state = {
  schema: {},
  blockGroups: [],
  shipCores: [],
  selectedGroupIndex: 0,
  selectedCoreIndex: 0
};

const DEFAULT_GRID_MODIFIERS = {
  AssemblerSpeed: 1,
  DrillHarvestMultiplier: 1,
  GyroEfficiency: 1,
  GyroForce: 1,
  PowerProducersOutput: 1,
  RefineEfficiency: 1,
  RefineSpeed: 1,
  ThrusterEfficiency: 1,
  ThrusterForce: 1
};

const DEFAULT_SPEED_MODIFIERS = {
  MaxSpeed: 0.3,
  MaxBoost: 0.5,
  BoostDuration: 10,
  BoostCoolDown: 60,
  MinimumFrictionSpeedAbsolute: 100,
  MaximumFrictionSpeedAbsolute: 290,
  MinimumFrictionSpeedModifier: 0.3,
  MaximumFrictionSpeedModifier: 0.8,
  MaximumFrictionDeceleration: 1
};

const DEFAULT_DEFENSE_MODIFIERS = {
  Bullet: 1,
  PostShield: 1,
  Duration: 0,
  Cooldown: 0,
  Rocket: 1,
  Explosion: 1,
  Environment: 1,
  Energy: 1,
  Kinetic: 1
};


const VALID_DIRECTIONS = ["Forward", "Backward", "Up", "Down", "Left", "Right"];

const ids = (id) => document.getElementById(id);

function escapeXml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', '&quot;')
    .replaceAll("'", "&apos;");
}

function parseXml(text) {
  return new DOMParser().parseFromString(text, "application/xml");
}

function textOf(parent, tag) {
  return parent?.querySelector(tag)?.textContent?.trim() ?? "";
}

function numberOf(parent, tag, fallback = 0) {
  const value = Number(textOf(parent, tag));
  return Number.isFinite(value) ? value : fallback;
}

function boolOf(parent, tag, fallback = false) {
  const value = textOf(parent, tag).toLowerCase();
  if (value === "true") return true;
  if (value === "false") return false;
  return fallback;
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
    if (fields.length) classes[className] = fields;
  }
  return classes;
}

function setImportStatus(lines) {
  ids("importStatus").textContent = lines.join("\n");
}

function parseModifierNode(node, defaults) {
  const parsed = { ...defaults };
  if (!node) return parsed;

  Object.keys(defaults).forEach((key) => {
    parsed[key] = numberOf(node, key, defaults[key]);
  });

  return parsed;
}

function createDefaultCore() {
  return {
    subtypeId: "",
    uniqueName: "",
    originalFileName: "",
    mobilityType: "Both",
    maxBlocks: -1,
    maxMass: -1,
    maxPcu: -1,
    maxPerPlayer: -1,
    forceBroadcast: false,
    forceBroadcastRange: 2000,
    speedBoostEnabled: false,
    speedLimitType: "Normal",
    enableActiveDefenseModifiers: false,
    modifiers: { ...DEFAULT_GRID_MODIFIERS },
    speedModifiers: { ...DEFAULT_SPEED_MODIFIERS },
    passiveDefenseModifiers: { ...DEFAULT_DEFENSE_MODIFIERS },
    activeDefenseModifiers: { ...DEFAULT_DEFENSE_MODIFIERS },
    blockLimits: []
  };
}

function ensureValidSelectedIndexes() {
  state.selectedGroupIndex = state.blockGroups.length
    ? Math.min(Math.max(state.selectedGroupIndex, 0), state.blockGroups.length - 1)
    : -1;
  state.selectedCoreIndex = state.shipCores.length
    ? Math.min(Math.max(state.selectedCoreIndex, 0), state.shipCores.length - 1)
    : -1;
}

function addBlockGroup(group = { name: "", blockTypes: [] }) {
  state.blockGroups.push(group);
  state.selectedGroupIndex = state.blockGroups.length - 1;
  renderBlockGroups();
  renderShipCores();
}

function renameBlockGroupReferences(previousName, nextName) {
  if (!previousName || previousName === nextName) return;
  state.shipCores.forEach((core) => {
    core.blockLimits.forEach((limit) => {
      if (!Array.isArray(limit.blockGroups)) return;
      limit.blockGroups = limit.blockGroups.map((groupName) => (groupName === previousName ? nextName : groupName));
    });
  });
}

function removeBlockGroupReferences(groupNameToRemove) {
  if (!groupNameToRemove) return;
  state.shipCores.forEach((core) => {
    core.blockLimits.forEach((limit) => {
      if (!Array.isArray(limit.blockGroups)) return;
      limit.blockGroups = limit.blockGroups.filter((groupName) => groupName !== groupNameToRemove);
    });
  });
}

function addShipCore(core = createDefaultCore()) {
  state.shipCores.push({ ...createDefaultCore(), ...core });
  state.selectedCoreIndex = state.shipCores.length - 1;
  renderShipCores();
}

function resetEditor(seed = true) {
  state.blockGroups = [];
  state.shipCores = [];
  state.selectedGroupIndex = 0;
  state.selectedCoreIndex = 0;

  if (seed) {
    addBlockGroup({ name: "Weaponry", blockTypes: [{ typeId: "SmallGatlingGun", subtypeId: "", countWeight: 1 }] });
    addShipCore({
      subtypeId: "Fighter",
      uniqueName: "Fighter Grid Class",
      mobilityType: "Mobile",
      maxBlocks: 1000,
      maxMass: -1,
      maxPcu: 6000,
      maxPerPlayer: 10,
      forceBroadcast: true,
      forceBroadcastRange: 2000,
      modifiers: {
        ...DEFAULT_GRID_MODIFIERS,
        ThrusterForce: 1.15,
        GyroForce: 1.1
      },
      blockLimits: [{
        name: "Weapons",
        maxCount: 8,
        punishByNoFlyZone: true,
        punishmentType: "Delete",
        allowedDirections: ["Forward"],
        blockGroups: ["Weaponry"]
      }]
    });
  } else {
    renderBlockGroups();
    renderShipCores();
  }

  generateXml();
}

function blockTypeEditor(groupIdx, blockType, blockTypeIdx) {
  return `<div class="row wrap">
    <label class="inline">TypeId <input data-action="bt-type" data-g="${groupIdx}" data-i="${blockTypeIdx}" class="small" placeholder="TypeId" value="${escapeXml(blockType.typeId)}" /></label>
    <label class="inline">SubtypeId <input data-action="bt-subtype" data-g="${groupIdx}" data-i="${blockTypeIdx}" class="small" placeholder="SubtypeId (or any)" value="${escapeXml(blockType.subtypeId)}" /></label>
    <label class="inline">CountWeight <input data-action="bt-weight" data-g="${groupIdx}" data-i="${blockTypeIdx}" class="small" type="number" step="0.1" value="${blockType.countWeight}" /></label>
    <button data-action="remove-bt" data-g="${groupIdx}" data-i="${blockTypeIdx}">Remove BlockType</button>
  </div>`;
}

function renderGroupSelector() {
  ensureValidSelectedIndexes();
  const selector = ids("selectedGroup");
  selector.innerHTML = state.blockGroups
    .map((group, idx) => {
      const label = group.name?.trim() ? `${idx + 1}. ${group.name}` : `${idx + 1}. (Unnamed Group)`;
      return `<option value="${idx}" ${idx === state.selectedGroupIndex ? "selected" : ""}>${escapeXml(label)}</option>`;
    })
    .join("");
  selector.disabled = state.blockGroups.length === 0;
}

function renderBlockGroups() {
  ensureValidSelectedIndexes();
  renderGroupSelector();

  if (state.selectedGroupIndex < 0) {
    ids("blockGroups").innerHTML = `<p class="muted">No block groups yet. Click <strong>Add Block Group</strong>.</p>`;
    return;
  }

  const groupIndex = state.selectedGroupIndex;
  const group = state.blockGroups[groupIndex];

  ids("blockGroups").innerHTML = `
    <div class="card">
      <div class="row wrap">
        <label class="inline">Group Name <input data-action="group-name" data-g="${groupIndex}" placeholder="Group Name" value="${escapeXml(group.name)}" /></label>
        <button data-action="add-bt" data-g="${groupIndex}">Add BlockType</button>
        <button data-action="remove-group" data-g="${groupIndex}">Delete Group</button>
      </div>
      ${group.blockTypes.map((bt, i) => blockTypeEditor(groupIndex, bt, i)).join("")}
    </div>
  `;
}

function blockGroupCheckboxes(coreIndex, limitIndex, selected = []) {
  return state.blockGroups
    .filter((group) => group.name.trim())
    .map((group) => `<label class="group-checklist-item">
      <input data-action="limit-group-toggle" data-c="${coreIndex}" data-l="${limitIndex}" data-group-name="${escapeXml(group.name)}" type="checkbox" ${selected.includes(group.name) ? "checked" : ""} />
      <span>${escapeXml(group.name)}</span>
    </label>`)
    .join("");
}

function directionCheckboxes(coreIndex, limitIndex, selected = []) {
  return VALID_DIRECTIONS
    .map((direction) => `<label class="group-checklist-item">
      <input data-action="limit-direction-toggle" data-c="${coreIndex}" data-l="${limitIndex}" data-direction="${escapeXml(direction)}" type="checkbox" ${selected.includes(direction) ? "checked" : ""} />
      <span>${escapeXml(direction)}</span>
    </label>`)
    .join("");
}
function modifierFieldColumn({ title, fields, action, step = 0.01, dataAttrs = "" }) {
  return `
    <div class="modifier-column card">
      <h5>${escapeXml(title)}</h5>
      ${fields
        .map((field) => `<label class="modifier-field">${escapeXml(field.name)}<input data-action="${action}" ${dataAttrs} data-m="${field.name}" class="small" type="number" step="${step}" value="${field.value}" /></label>`)
        .join("")}
    </div>
  `;
}

function mapModifierFields(values, defaults) {
  return Object.keys(defaults).map((name) => ({
    name,
    value: Number(values?.[name] ?? defaults[name])
  }));
}

function renderCoreSelector() {
  ensureValidSelectedIndexes();
  const selector = ids("selectedCore");
  selector.innerHTML = state.shipCores
    .map((core, idx) => {
      const label = core.subtypeId?.trim() || "Unnamed Core";
      return `<option value="${idx}" ${idx === state.selectedCoreIndex ? "selected" : ""}>${escapeXml(`${idx + 1}. ${label}`)}</option>`;
    })
    .join("");
  selector.disabled = state.shipCores.length === 0;
}

function renderShipCores() {
  ensureValidSelectedIndexes();
  renderCoreSelector();

  if (state.selectedCoreIndex < 0) {
    ids("shipCores").innerHTML = `<p class="muted">No ship cores yet. Click <strong>Add Ship Core</strong>.</p>`;
    return;
  }

  const coreIndex = state.selectedCoreIndex;
  const core = state.shipCores[coreIndex];

  ids("shipCores").innerHTML = `
    <div class="card">
      <div class="row wrap">
        <label class="inline">Core Subtype <input data-action="core-subtype" data-c="${coreIndex}" class="small" placeholder="SubtypeId" value="${escapeXml(core.subtypeId)}" /></label>
        <label class="inline">Core Name <input data-action="core-unique" data-c="${coreIndex}" class="small" placeholder="UniqueName" value="${escapeXml(core.uniqueName)}" /></label>
        <label class="inline">Mobility
          <select data-action="core-mobility" data-c="${coreIndex}" class="small">
            ${["Static", "Mobile", "Both"].map((value) => `<option ${core.mobilityType === value ? "selected" : ""}>${value}</option>`).join("")}
          </select>
        </label>
        <button data-action="remove-core" data-c="${coreIndex}">Delete Core</button>
      </div>
      <div class="row wrap">
        <label class="inline">MaxBlocks <input data-action="core-maxblocks" data-c="${coreIndex}" class="small" type="number" value="${core.maxBlocks}" /></label>
        <label class="inline">MaxMass <input data-action="core-maxmass" data-c="${coreIndex}" class="small" type="number" value="${core.maxMass}" /></label>
        <label class="inline">MaxPCU <input data-action="core-maxpcu" data-c="${coreIndex}" class="small" type="number" value="${core.maxPcu}" /></label>
        <label class="inline">MaxPerPlayer <input data-action="core-maxpp" data-c="${coreIndex}" class="small" type="number" value="${core.maxPerPlayer}" /></label>
      </div>
      <div class="row wrap">
        <label class="inline">ForceBroadcast <input data-action="core-fb" data-c="${coreIndex}" type="checkbox" ${core.forceBroadcast ? "checked" : ""}/></label>
        <label class="inline">BroadcastRange <input data-action="core-fbr" data-c="${coreIndex}" class="small" type="number" value="${core.forceBroadcastRange}" /></label>
        <label class="inline">SpeedBoostEnabled <input data-action="core-speedboost" data-c="${coreIndex}" type="checkbox" ${core.speedBoostEnabled ? "checked" : ""}/></label>
        <label class="inline">EnableActiveDefense <input data-action="core-enable-active-defense" data-c="${coreIndex}" type="checkbox" ${core.enableActiveDefenseModifiers ? "checked" : ""}/></label>
        <label class="inline">SpeedLimitType
          <select data-action="core-speed-limit-type" data-c="${coreIndex}" class="small">
            ${["Normal", "Friction"].map((value) => `<option ${core.speedLimitType === value ? "selected" : ""}>${value}</option>`).join("")}
          </select>
        </label>
        <button data-action="add-limit" data-c="${coreIndex}">Add Block Limit</button>
      </div>

      <div class="modifier-row four-columns">
        ${modifierFieldColumn({
          title: "Grid Modifiers",
          action: "core-modifier-grid",
          dataAttrs: `data-c="${coreIndex}"`,
          fields: mapModifierFields(core.modifiers, DEFAULT_GRID_MODIFIERS)
        })}
        ${modifierFieldColumn({
          title: "Speed Modifiers",
          action: "core-modifier-speed",
          dataAttrs: `data-c="${coreIndex}"`,
          fields: mapModifierFields(core.speedModifiers, DEFAULT_SPEED_MODIFIERS)
        })}
        ${modifierFieldColumn({
          title: "Passive Defense Modifiers",
          action: "core-modifier-passive-defense",
          dataAttrs: `data-c="${coreIndex}"`,
          fields: mapModifierFields(core.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)
        })}
        ${modifierFieldColumn({
          title: "Active Defense Modifiers",
          action: "core-modifier-active-defense",
          dataAttrs: `data-c="${coreIndex}"`,
          fields: mapModifierFields(core.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)
        })}
      </div>

      ${core.blockLimits.map((limit, limitIndex) => `
        <div class="card">
          <div class="row wrap">
            <input data-action="limit-name" data-c="${coreIndex}" data-l="${limitIndex}" class="small" placeholder="Limit Name" value="${escapeXml(limit.name)}" />
            <label class="inline">MaxCount <input data-action="limit-max" data-c="${coreIndex}" data-l="${limitIndex}" class="small" type="number" step="0.1" value="${limit.maxCount}" /></label>
            <button data-action="remove-limit" data-c="${coreIndex}" data-l="${limitIndex}">Delete Limit</button>
          </div>
          <div class="row wrap">
            <label class="inline">PunishByNoFlyZone <input data-action="limit-punish" data-c="${coreIndex}" data-l="${limitIndex}" type="checkbox" ${limit.punishByNoFlyZone ? "checked" : ""}/></label>
            <label class="inline">Punishment Type
              <select data-action="limit-type" data-c="${coreIndex}" data-l="${limitIndex}" class="small">
                ${["ShutOff", "Damage", "Delete", "Explode"].map((value) => `<option ${limit.punishmentType === value ? "selected" : ""}>${value}</option>`).join("")}
              </select>
            </label>
          </div>
          <div>
            <label>Valid Directions</label>
            <div class="group-checklist">
              ${directionCheckboxes(coreIndex, limitIndex, limit.allowedDirections || [])}
            </div>
          </div>
          <div>
            <label>Reusable Block Groups</label>
            <div class="group-checklist">
              ${blockGroupCheckboxes(coreIndex, limitIndex, limit.blockGroups)}
            </div>
          </div>
        </div>
      `).join("")}
    </div>
  `;
}

function parseGroupsXml(text) {
  const doc = parseXml(text);
  return Array.from(doc.querySelectorAll("BlockGroup"))
    .map((groupNode) => ({
      name: textOf(groupNode, "Name"),
      blockTypes: Array.from(groupNode.querySelectorAll(":scope > BlockTypes")).map((typeNode) => ({
        typeId: textOf(typeNode, "TypeId"),
        subtypeId: textOf(typeNode, "SubtypeId"),
        countWeight: numberOf(typeNode, "CountWeight", 1)
      }))
    }))
    .filter((group) => group.name);
}

function parseCoreXml(text, originalFileName = "") {
  const doc = parseXml(text);
  const coreNode = doc.querySelector("ShipCore");
  if (!coreNode) return null;

  return {
    ...createDefaultCore(),
    originalFileName,
    subtypeId: textOf(coreNode, "SubtypeId"),
    uniqueName: textOf(coreNode, "UniqueName"),
    mobilityType: textOf(coreNode, "MobilityType") || "Both",
    maxBlocks: numberOf(coreNode, "MaxBlocks", -1),
    maxMass: numberOf(coreNode, "MaxMass", -1),
    maxPcu: numberOf(coreNode, "MaxPCU", -1),
    maxPerPlayer: numberOf(coreNode, "MaxPerPlayer", -1),
    forceBroadcast: boolOf(coreNode, "ForceBroadCast", false),
    forceBroadcastRange: numberOf(coreNode, "ForceBroadCastRange", 2000),
    speedBoostEnabled: boolOf(coreNode, "SpeedBoostEnabled", false),
    speedLimitType: textOf(coreNode, "SpeedLimitType") || "Normal",
    enableActiveDefenseModifiers: boolOf(coreNode, "EnableActiveDefenseModifiers", false),
    modifiers: parseModifierNode(coreNode.querySelector(":scope > Modifiers"), DEFAULT_GRID_MODIFIERS),
    speedModifiers: parseModifierNode(coreNode.querySelector(":scope > SpeedModifiers"), DEFAULT_SPEED_MODIFIERS),
    passiveDefenseModifiers: parseModifierNode(coreNode.querySelector(":scope > PassiveDefenseModifiers"), DEFAULT_DEFENSE_MODIFIERS),
    activeDefenseModifiers: parseModifierNode(coreNode.querySelector(":scope > ActiveDefenseModifiers"), DEFAULT_DEFENSE_MODIFIERS),
    blockLimits: Array.from(coreNode.querySelectorAll(":scope > BlockLimits")).map((limitNode) => ({
      name: textOf(limitNode, "Name"),
      maxCount: numberOf(limitNode, "MaxCount", 0),
      punishByNoFlyZone: boolOf(limitNode, "PunishByNoFlyZone", boolOf(limitNode, "TurnedOffByNoFlyZone", false)),
      punishmentType: textOf(limitNode, "PunishmentType") || "ShutOff",
      allowedDirections: Array.from(limitNode.querySelectorAll(":scope > AllowedDirections")).map((node) => node.textContent.trim()).filter(Boolean),
      blockGroups: Array.from(limitNode.querySelectorAll(":scope > BlockGroups")).map((node) => node.textContent.trim()).filter(Boolean)
    }))
  };
}

function writeModifierXml(tag, values, defaults, indent = "  ") {
  return `${indent}<${tag}>\n${Object.keys(defaults)
    .map((name) => `${indent}  <${name}>${Number(values?.[name] ?? defaults[name])}</${name}>`)
    .join("\n")}\n${indent}</${tag}>`;
}

function sanitizeFilenamePart(value) {
  return String(value ?? "")
    .trim()
    .replace(/\s+/g, "_")
    .replace(/[^a-zA-Z0-9_-]/g, "")
    .replace(/^_+|_+$/g, "");
}

function deriveCoreFilename(core) {
  if (core.originalFileName?.trim()) return core.originalFileName.trim();

  const base = sanitizeFilenamePart(core.subtypeId || core.uniqueName || "unnamed");
  const withoutSuffix = base.replace(/_core$/i, "");
  return `${withoutSuffix || "unnamed"}_core.xml`;
}

function createCrc32Table() {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i += 1) {
    let crc = i;
    for (let j = 0; j < 8; j += 1) {
      crc = (crc & 1) ? (0xedb88320 ^ (crc >>> 1)) : (crc >>> 1);
    }
    table[i] = crc >>> 0;
  }
  return table;
}

const CRC32_TABLE = createCrc32Table();

function crc32(bytes) {
  let crc = 0xffffffff;
  for (let i = 0; i < bytes.length; i += 1) {
    crc = CRC32_TABLE[(crc ^ bytes[i]) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function dosDateTime(date = new Date()) {
  const year = Math.max(1980, date.getFullYear());
  const dosTime = (date.getSeconds() >> 1) | (date.getMinutes() << 5) | (date.getHours() << 11);
  const dosDate = date.getDate() | ((date.getMonth() + 1) << 5) | ((year - 1980) << 9);
  return { dosTime, dosDate };
}

function pushUint16(buffer, value) {
  buffer.push(value & 0xff, (value >>> 8) & 0xff);
}

function pushUint32(buffer, value) {
  buffer.push(value & 0xff, (value >>> 8) & 0xff, (value >>> 16) & 0xff, (value >>> 24) & 0xff);
}

function createZip(entries) {
  const encoder = new TextEncoder();
  const localParts = [];
  const centralParts = [];
  let localOffset = 0;
  const { dosTime, dosDate } = dosDateTime();

  for (const entry of entries) {
    const nameBytes = encoder.encode(entry.name);
    const dataBytes = encoder.encode(entry.content);
    const crc = crc32(dataBytes);

    const localHeader = [];
    pushUint32(localHeader, 0x04034b50);
    pushUint16(localHeader, 20);
    pushUint16(localHeader, 0);
    pushUint16(localHeader, 0);
    pushUint16(localHeader, dosTime);
    pushUint16(localHeader, dosDate);
    pushUint32(localHeader, crc);
    pushUint32(localHeader, dataBytes.length);
    pushUint32(localHeader, dataBytes.length);
    pushUint16(localHeader, nameBytes.length);
    pushUint16(localHeader, 0);

    const localHeaderBytes = new Uint8Array(localHeader);
    localParts.push(localHeaderBytes, nameBytes, dataBytes);

    const centralHeader = [];
    pushUint32(centralHeader, 0x02014b50);
    pushUint16(centralHeader, 20);
    pushUint16(centralHeader, 20);
    pushUint16(centralHeader, 0);
    pushUint16(centralHeader, 0);
    pushUint16(centralHeader, dosTime);
    pushUint16(centralHeader, dosDate);
    pushUint32(centralHeader, crc);
    pushUint32(centralHeader, dataBytes.length);
    pushUint32(centralHeader, dataBytes.length);
    pushUint16(centralHeader, nameBytes.length);
    pushUint16(centralHeader, 0);
    pushUint16(centralHeader, 0);
    pushUint16(centralHeader, 0);
    pushUint16(centralHeader, 0);
    pushUint32(centralHeader, 0);
    pushUint32(centralHeader, localOffset);

    const centralHeaderBytes = new Uint8Array(centralHeader);
    centralParts.push(centralHeaderBytes, nameBytes);

    localOffset += localHeaderBytes.length + nameBytes.length + dataBytes.length;
  }

  const centralSize = centralParts.reduce((sum, part) => sum + part.length, 0);
  const centralOffset = localOffset;
  const endRecord = [];
  pushUint32(endRecord, 0x06054b50);
  pushUint16(endRecord, 0);
  pushUint16(endRecord, 0);
  pushUint16(endRecord, entries.length);
  pushUint16(endRecord, entries.length);
  pushUint32(endRecord, centralSize);
  pushUint32(endRecord, centralOffset);
  pushUint16(endRecord, 0);

  return new Blob([...localParts, ...centralParts, new Uint8Array(endRecord)], { type: "application/zip" });
}

function downloadBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

function download(filename, content) {
  downloadBlob(filename, new Blob([content], { type: "application/xml" }));
}

function generateXml() {
  const header = '<?xml version="1.0" encoding="UTF-8"?>';

  const groups = `${header}\n<ArrayOfBlockGroup>\n${state.blockGroups
    .map((group) => `  <BlockGroup>\n    <Name>${escapeXml(group.name)}</Name>\n${group.blockTypes
      .map((bt) => `    <BlockTypes>\n      <TypeId>${escapeXml(bt.typeId)}</TypeId>\n      <SubtypeId>${escapeXml(bt.subtypeId || "")}</SubtypeId>\n      <CountWeight>${bt.countWeight}</CountWeight>\n    </BlockTypes>`)
      .join("\n")}\n  </BlockGroup>`)
    .join("\n")}\n</ArrayOfBlockGroup>`;

  const manifest = `${header}\n<CoreManifest>\n${state.shipCores
    .filter((core) => core.subtypeId.trim())
    .map((core) => `  <ShipCoreFilenames>Data/Cores/${escapeXml(deriveCoreFilename(core))}</ShipCoreFilenames>`)
    .join("\n")}\n</CoreManifest>`;

  const cores = state.shipCores.map((core) => ({
    file: deriveCoreFilename(core),
    body: `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(core.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(core.uniqueName)}</UniqueName>\n  <ForceBroadCast>${core.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${core.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(core.mobilityType)}</MobilityType>\n  <MaxBlocks>${core.maxBlocks}</MaxBlocks>\n  <MaxMass>${core.maxMass}</MaxMass>\n  <MaxPCU>${core.maxPcu}</MaxPCU>\n  <MaxPerPlayer>${core.maxPerPlayer}</MaxPerPlayer>\n  <SpeedBoostEnabled>${core.speedBoostEnabled}</SpeedBoostEnabled>\n  <SpeedLimitType>${escapeXml(core.speedLimitType)}</SpeedLimitType>\n  <EnableActiveDefenseModifiers>${core.enableActiveDefenseModifiers}</EnableActiveDefenseModifiers>\n${writeModifierXml("Modifiers", core.modifiers, DEFAULT_GRID_MODIFIERS)}\n${writeModifierXml("SpeedModifiers", core.speedModifiers, DEFAULT_SPEED_MODIFIERS)}\n${writeModifierXml("PassiveDefenseModifiers", core.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${writeModifierXml("ActiveDefenseModifiers", core.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${core.blockLimits
      .map((limit) => `  <BlockLimits>\n    <Name>${escapeXml(limit.name)}</Name>\n${limit.blockGroups.map((g) => `    <BlockGroups>${escapeXml(g)}</BlockGroups>`).join("\n")}\n    <MaxCount>${limit.maxCount}</MaxCount>\n    <PunishByNoFlyZone>${limit.punishByNoFlyZone}</PunishByNoFlyZone>\n    <PunishmentType>${escapeXml(limit.punishmentType)}</PunishmentType>\n${(limit.allowedDirections || []).filter(Boolean).map((d) => `    <AllowedDirections>${escapeXml(d)}</AllowedDirections>`).join("\n")}\n  </BlockLimits>`)
      .join("\n")}\n</ShipCore>`
  }));

  ids("groupsXml").textContent = groups;
  ids("manifestXml").textContent = manifest;
  ids("coresXml").textContent = cores.map((core) => `===== ${core.file} =====\n${core.body}`).join("\n\n");
  return { groups, manifest, cores };
}

document.addEventListener("click", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const groupIndex = Number(target.dataset.g);
  const blockTypeIndex = Number(target.dataset.i);
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);
  let didMutate = false;

  if (action === "add-bt") {
    state.blockGroups[groupIndex].blockTypes.push({ typeId: "", subtypeId: "", countWeight: 1 });
    didMutate = true;
  }
  if (action === "remove-bt") {
    state.blockGroups[groupIndex].blockTypes.splice(blockTypeIndex, 1);
    didMutate = true;
  }
  if (action === "remove-group") {
    const removedGroupName = state.blockGroups[groupIndex]?.name || "";
    removeBlockGroupReferences(removedGroupName);
    state.blockGroups.splice(groupIndex, 1);
    if (state.selectedGroupIndex >= state.blockGroups.length) state.selectedGroupIndex = state.blockGroups.length - 1;
    didMutate = true;
  }
  if (action === "remove-core") {
    state.shipCores.splice(coreIndex, 1);
    if (state.selectedCoreIndex >= state.shipCores.length) state.selectedCoreIndex = state.shipCores.length - 1;
    didMutate = true;
  }
  if (action === "add-limit") {
    state.shipCores[coreIndex].blockLimits.push({ name: "", maxCount: 0, punishByNoFlyZone: false, punishmentType: "ShutOff", allowedDirections: [], blockGroups: [] });
    didMutate = true;
  }
  if (action === "remove-limit") {
    state.shipCores[coreIndex].blockLimits.splice(limitIndex, 1);
    didMutate = true;
  }

  if (!didMutate) return;

  renderBlockGroups();
  renderShipCores();
  generateXml();
});

document.addEventListener("input", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const groupIndex = Number(target.dataset.g);
  const blockTypeIndex = Number(target.dataset.i);
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);

  if (action === "group-name") {
    const previousName = state.blockGroups[groupIndex].name;
    state.blockGroups[groupIndex].name = target.value;
    renameBlockGroupReferences(previousName, target.value);
  }
  if (action === "bt-type") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].typeId = target.value;
  if (action === "bt-subtype") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].subtypeId = target.value;
  if (action === "bt-weight") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].countWeight = Number(target.value || 0);

  if (action === "core-subtype") {
    state.shipCores[coreIndex].subtypeId = target.value;
    if (!state.shipCores[coreIndex].originalFileName) renderShipCores();
  }
  if (action === "core-unique") state.shipCores[coreIndex].uniqueName = target.value;
  if (action === "core-maxblocks") state.shipCores[coreIndex].maxBlocks = Number(target.value || -1);
  if (action === "core-maxmass") state.shipCores[coreIndex].maxMass = Number(target.value || -1);
  if (action === "core-maxpcu") state.shipCores[coreIndex].maxPcu = Number(target.value || -1);
  if (action === "core-maxpp") state.shipCores[coreIndex].maxPerPlayer = Number(target.value || -1);
  if (action === "core-fbr") state.shipCores[coreIndex].forceBroadcastRange = Number(target.value || 0);

  if (action === "core-modifier-grid") state.shipCores[coreIndex].modifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-speed") state.shipCores[coreIndex].speedModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-passive-defense") state.shipCores[coreIndex].passiveDefenseModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-active-defense") state.shipCores[coreIndex].activeDefenseModifiers[target.dataset.m] = Number(target.value || 0);

  if (action === "limit-name") state.shipCores[coreIndex].blockLimits[limitIndex].name = target.value;
  if (action === "limit-max") state.shipCores[coreIndex].blockLimits[limitIndex].maxCount = Number(target.value || 0);

  if (action === "group-name") {
    renderGroupSelector();
    renderShipCores();
  }

  if (action === "core-subtype") {
    renderShipCores();
  }

  generateXml();
});

document.addEventListener("change", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);

  const inputElement = target instanceof HTMLInputElement ? target : null;
  const selectElement = target instanceof HTMLSelectElement ? target : null;

  if (action === "core-fb" && inputElement) state.shipCores[coreIndex].forceBroadcast = inputElement.checked;
  if (action === "core-mobility" && selectElement) state.shipCores[coreIndex].mobilityType = selectElement.value;
  if (action === "core-speedboost" && inputElement) state.shipCores[coreIndex].speedBoostEnabled = inputElement.checked;
  if (action === "core-enable-active-defense" && inputElement) state.shipCores[coreIndex].enableActiveDefenseModifiers = inputElement.checked;
  if (action === "core-speed-limit-type" && selectElement) state.shipCores[coreIndex].speedLimitType = selectElement.value;
  if (action === "limit-punish" && inputElement) {
    state.shipCores[coreIndex].blockLimits[limitIndex].punishByNoFlyZone = inputElement.checked;
    renderShipCores();
  }
  if (action === "limit-type" && selectElement) {
    state.shipCores[coreIndex].blockLimits[limitIndex].punishmentType = selectElement.value;
    renderShipCores();
  }
  if (action === "limit-direction-toggle" && inputElement) {
    const limit = state.shipCores[coreIndex].blockLimits[limitIndex];
    const direction = inputElement.dataset.direction || "";
    if (!direction) return;

    const selectedSet = new Set(limit.allowedDirections || []);
    if (inputElement.checked) selectedSet.add(direction);
    else selectedSet.delete(direction);
    limit.allowedDirections = VALID_DIRECTIONS.filter((value) => selectedSet.has(value));
    renderShipCores();
  }
  if (action === "limit-group-toggle" && inputElement) {
    const limit = state.shipCores[coreIndex].blockLimits[limitIndex];
    const groupName = inputElement.dataset.groupName || "";
    if (!groupName) return;

    const selectedSet = new Set(limit.blockGroups || []);
    if (inputElement.checked) selectedSet.add(groupName);
    else selectedSet.delete(groupName);
    limit.blockGroups = Array.from(selectedSet);
    renderShipCores();
  }

  generateXml();
});

ids("selectedGroup").addEventListener("change", (event) => {
  state.selectedGroupIndex = Number(event.target.value);
  renderBlockGroups();
  generateXml();
});

ids("selectedCore").addEventListener("change", (event) => {
  state.selectedCoreIndex = Number(event.target.value);
  renderShipCores();
  generateXml();
});

ids("addGroup").addEventListener("click", () => addBlockGroup({ name: "", blockTypes: [] }));
ids("addCore").addEventListener("click", () => addShipCore());
ids("generateXml").addEventListener("click", () => generateXml());
ids("resetEditor").addEventListener("click", () => {
  resetEditor(true);
  setImportStatus(["Editor reset to starter seed data."]);
});

ids("downloadGroups").addEventListener("click", () => download("ShipCoreConfig_Groups.xml", generateXml().groups));
ids("downloadManifest").addEventListener("click", () => download("ShipCoreConfig_Manifest.xml", generateXml().manifest));
ids("downloadCores").addEventListener("click", () => {
  const xml = generateXml();
  const zip = createZip(xml.cores.map((core) => ({ name: core.file, content: core.body })));
  downloadBlob("ShipCore_XMLs.zip", zip);
});

ids("loadUploadedXml").addEventListener("click", async () => {
  const groupsFile = ids("groupsXmlFile").files?.[0];
  const manifestFile = ids("manifestXmlFile").files?.[0];
  const coreFiles = Array.from(ids("coreXmlFiles").files || []);
  const status = [];

  if (!groupsFile && !manifestFile && coreFiles.length === 0) {
    setImportStatus(["No XML files selected."]);
    return;
  }

  resetEditor(false);

  if (groupsFile) {
    const groups = parseGroupsXml(await groupsFile.text());
    state.blockGroups = groups;
    state.selectedGroupIndex = 0;
    status.push(`Loaded ${groups.length} block groups from ${groupsFile.name}.`);
  }

  if (manifestFile) {
    const manifestDoc = parseXml(await manifestFile.text());
    const listed = Array.from(manifestDoc.querySelectorAll("ShipCoreFilenames")).map((n) => n.textContent.trim()).filter(Boolean);
    status.push(`Read manifest ${manifestFile.name} with ${listed.length} listed core files.`);
  }

  for (const file of coreFiles) {
    const parsed = parseCoreXml(await file.text(), file.name);
    if (!parsed) {
      status.push(`Skipped ${file.name}: no <ShipCore> root found.`);
      continue;
    }
    state.shipCores.push(parsed);
    status.push(`Loaded core '${parsed.subtypeId || file.name}'.`);
  }

  state.selectedCoreIndex = 0;

  if (state.blockGroups.length === 0) status.push("No block groups loaded (you can still create them manually).");
  if (state.shipCores.length === 0) status.push("No cores loaded (you can still add cores manually).");

  renderBlockGroups();
  renderShipCores();
  generateXml();
  setImportStatus(status);
});

(async () => {
  const response = await fetch("./assets/ModConfig.cs", { cache: "no-cache" });
  const text = await response.text();
  state.schema = parseModConfigCs(text);
  ids("schemaPreview").textContent = JSON.stringify(state.schema, null, 2);
  ids("parserStatus").textContent = `Loaded bundled ModConfig.cs and parsed ${Object.keys(state.schema).length} XML classes.`;

  resetEditor(true);
  setImportStatus(["Tip: Upload existing XML files to renovate and continue editing."]);
})();
