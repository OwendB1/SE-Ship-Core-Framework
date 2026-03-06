const state = {
  schema: {},
  blockGroups: [],
  shipCores: [],
  selectedGroupIndex: 0,
  selectedCoreIndex: 0,
  noCoreCore: createDefaultNoCore(),
  expandedLimitPanelsByCore: {}
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
const DRAFT_STORAGE_KEY = "ship-core-configurator-draft-v1";

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

function cloneBlockGroup(group = { name: "", blockTypes: [] }) {
  return {
    name: String(group.name ?? ""),
    blockTypes: Array.isArray(group.blockTypes) ? group.blockTypes.map((blockType) => cloneBlockType(blockType)) : []
  };
}

function persistDraftToStorage() {
  try {
    const payload = {
      blockGroups: state.blockGroups.map((group) => cloneBlockGroup(group)),
      shipCores: state.shipCores.map((core) => cloneShipCore(core)),
      noCoreCore: state.noCoreCore ? cloneShipCore(state.noCoreCore) : null,
      selectedGroupIndex: state.selectedGroupIndex,
      selectedCoreIndex: state.selectedCoreIndex,
      expandedLimitPanelsByCore: state.expandedLimitPanelsByCore
    };
    localStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(payload));
  } catch (error) {
    console.warn("Failed to persist configurator draft to local storage.", error);
  }
}

function clearDraftFromStorage() {
  try {
    localStorage.removeItem(DRAFT_STORAGE_KEY);
  } catch (error) {
    console.warn("Failed to clear configurator draft from local storage.", error);
  }
}

function restoreDraftFromStorage() {
  try {
    const rawDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
    if (!rawDraft) return false;

    const parsedDraft = JSON.parse(rawDraft);
    state.blockGroups = Array.isArray(parsedDraft.blockGroups)
      ? parsedDraft.blockGroups.map((group) => cloneBlockGroup(group))
      : [];
    state.shipCores = Array.isArray(parsedDraft.shipCores)
      ? parsedDraft.shipCores.map((core) => cloneShipCore(core))
      : [];
    state.noCoreCore = parsedDraft.noCoreCore ? cloneShipCore(parsedDraft.noCoreCore) : createDefaultNoCore();
    state.selectedGroupIndex = Number.isInteger(parsedDraft.selectedGroupIndex) ? parsedDraft.selectedGroupIndex : 0;
    state.selectedCoreIndex = Number.isInteger(parsedDraft.selectedCoreIndex) ? parsedDraft.selectedCoreIndex : 0;
    state.expandedLimitPanelsByCore = parsedDraft.expandedLimitPanelsByCore && typeof parsedDraft.expandedLimitPanelsByCore === "object"
      ? parsedDraft.expandedLimitPanelsByCore
      : {};

    ensureValidSelectedIndexes();
    return true;
  } catch (error) {
    console.warn("Failed to restore configurator draft from local storage.", error);
    return false;
  }
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



function normalizeExpandedLimitPanelsForCore(coreIndex) {
  const core = getEditorCoreByIndex(coreIndex);
  if (!core) {
    delete state.expandedLimitPanelsByCore[coreIndex];
    return [];
  }

  const maxLimitIndex = core.blockLimits.length - 1;
  const expanded = Array.isArray(state.expandedLimitPanelsByCore[coreIndex])
    ? state.expandedLimitPanelsByCore[coreIndex]
    : [];

  const normalized = expanded
    .filter((index) => Number.isInteger(index) && index >= 0 && index <= maxLimitIndex)
    .filter((index, position, arr) => arr.indexOf(index) === position)
    .slice(-2);

  state.expandedLimitPanelsByCore[coreIndex] = normalized;
  return normalized;
}


function shiftExpandedLimitPanelsAfterCoreRemoval(removedCoreIndex) {
  const shifted = {};
  Object.entries(state.expandedLimitPanelsByCore).forEach(([key, value]) => {
    const index = Number(key);
    if (!Number.isInteger(index) || index === removedCoreIndex) return;
    shifted[index > removedCoreIndex ? index - 1 : index] = value;
  });
  state.expandedLimitPanelsByCore = shifted;
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
    maxBackupCores: -1,
    maxPerFaction: -1,
    minPerFaction: -1,
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
  if (!state.noCoreCore) state.noCoreCore = createDefaultNoCore();

  state.selectedGroupIndex = state.blockGroups.length
    ? Math.min(Math.max(state.selectedGroupIndex, 0), state.blockGroups.length - 1)
    : -1;

  const editableCoreCount = state.shipCores.length + (state.noCoreCore ? 1 : 0);
  state.selectedCoreIndex = editableCoreCount
    ? Math.min(Math.max(state.selectedCoreIndex, 0), editableCoreCount - 1)
    : -1;
}

function getEditorCoreByIndex(editorCoreIndex) {
  if (editorCoreIndex === 0) return state.noCoreCore;
  return state.shipCores[editorCoreIndex - 1] ?? null;
}

function addBlockGroup(group = { name: "", blockTypes: [] }) {
  state.blockGroups.push(group);
  state.selectedGroupIndex = state.blockGroups.length - 1;
  renderBlockGroups();
  renderShipCores();
}

function cloneBlockType(blockType = {}) {
  return {
    typeId: String(blockType.typeId ?? ""),
    subtypeId: String(blockType.subtypeId ?? ""),
    countWeight: Number(blockType.countWeight ?? 1)
  };
}

function cloneLimit(limit = createDefaultLimit()) {
  return {
    ...createDefaultLimit(),
    ...limit,
    allowedDirections: Array.isArray(limit.allowedDirections) ? [...limit.allowedDirections] : [],
    blockGroups: Array.isArray(limit.blockGroups) ? [...limit.blockGroups] : [],
    groupSearch: String(limit.groupSearch ?? "")
  };
}

function createIncrementedDuplicateName(name, allNames, fallbackBase = "BlockGroup") {
  const trimmedName = String(name ?? "").trim();
  const sourceBase = trimmedName || fallbackBase;
  const baseName = sourceBase.replace(/-\d+$/, "");
  const escapedBase = baseName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const duplicateRegex = new RegExp(`^${escapedBase}-(\\d+)$`);

  let maxDuplicateIndex = 0;
  allNames.forEach((existingNameRaw) => {
    const existingName = String(existingNameRaw ?? "").trim();
    if (existingName === baseName) {
      maxDuplicateIndex = Math.max(maxDuplicateIndex, 0);
      return;
    }

    const match = existingName.match(duplicateRegex);
    if (match) {
      maxDuplicateIndex = Math.max(maxDuplicateIndex, Number(match[1]));
    }
  });

  return `${baseName}-${maxDuplicateIndex + 1}`;
}

function renameBlockGroupReferences(previousName, nextName) {
  if (!previousName || previousName === nextName) return;
  [state.noCoreCore, ...state.shipCores].filter(Boolean).forEach((core) => {
    core.blockLimits.forEach((limit) => {
      if (!Array.isArray(limit.blockGroups)) return;
      limit.blockGroups = limit.blockGroups.map((groupName) => (groupName === previousName ? nextName : groupName));
    });
  });
}

function removeBlockGroupReferences(groupNameToRemove) {
  if (!groupNameToRemove) return;
  [state.noCoreCore, ...state.shipCores].filter(Boolean).forEach((core) => {
    core.blockLimits.forEach((limit) => {
      if (!Array.isArray(limit.blockGroups)) return;
      limit.blockGroups = limit.blockGroups.filter((groupName) => groupName !== groupNameToRemove);
    });
  });
}

function addShipCore(core = createDefaultCore()) {
  state.shipCores.push({ ...createDefaultCore(), ...core });
  state.selectedCoreIndex = state.shipCores.length;
  renderShipCores();
}

function createDefaultNoCore() {
  return {
    ...createDefaultCore(),
    subtypeId: "NO_CORE_DEFAULT",
    uniqueName: "No Core",
    originalFileName: "ShipCoreConfig_No_Core.xml"
  };
}

function cloneShipCore(core = createDefaultCore()) {
  return {
    ...createDefaultCore(),
    ...core,
    modifiers: { ...DEFAULT_GRID_MODIFIERS, ...(core.modifiers || {}) },
    speedModifiers: { ...DEFAULT_SPEED_MODIFIERS, ...(core.speedModifiers || {}) },
    passiveDefenseModifiers: { ...DEFAULT_DEFENSE_MODIFIERS, ...(core.passiveDefenseModifiers || {}) },
    activeDefenseModifiers: { ...DEFAULT_DEFENSE_MODIFIERS, ...(core.activeDefenseModifiers || {}) },
    blockLimits: Array.isArray(core.blockLimits) ? core.blockLimits.map((limit) => cloneLimit(limit)) : []
  };
}

function createDefaultLimit() {
  return {
    name: "",
    maxCount: 0,
    crossConnectorPunishment: false,
    punishByNoFlyZone: false,
    punishmentType: "ShutOff",
    allowedDirections: [],
    blockGroups: [],
    groupSearch: ""
  };
}

function resetEditor(seed = true) {
  state.blockGroups = [];
  state.shipCores = [];
  state.selectedGroupIndex = 0;
  state.selectedCoreIndex = 0;
  state.noCoreCore = createDefaultNoCore();
  state.expandedLimitPanelsByCore = {};

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
        blockGroups: ["Weaponry"],
        groupSearch: ""
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
        <button data-action="duplicate-group" data-g="${groupIndex}">Duplicate Group</button>
        <button data-action="remove-group" data-g="${groupIndex}">Delete Group</button>
      </div>
      ${group.blockTypes.map((bt, i) => blockTypeEditor(groupIndex, bt, i)).join("")}
    </div>
  `;
}

function blockGroupCheckboxes(coreIndex, limitIndex, selected = [], searchText = "") {
  const normalizedSearch = searchText.trim().toLowerCase();
  return state.blockGroups
    .filter((group) => group.name.trim())
    .filter((group) => !normalizedSearch || group.name.toLowerCase().includes(normalizedSearch))
    .map((group) => {
      const isSelected = selected.includes(group.name);
      return `<label class="group-checklist-item ${isSelected ? "selected" : ""}">
      <input data-action="limit-group-toggle" data-c="${coreIndex}" data-l="${limitIndex}" data-group-name="${escapeXml(group.name)}" type="checkbox" ${isSelected ? "checked" : ""} />
      <span>${escapeXml(group.name)}</span>
    </label>`;
    })
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
  const allCores = [state.noCoreCore, ...state.shipCores].filter(Boolean);
  selector.innerHTML = allCores
    .map((core, idx) => {
      const label = idx === 0
        ? `0. ${core.uniqueName?.trim() || "No Core"}`
        : `${idx}. ${core.subtypeId?.trim() || "Unnamed Core"}`;
      return `<option value="${idx}" ${idx === state.selectedCoreIndex ? "selected" : ""}>${escapeXml(label)}</option>`;
    })
    .join("");
  selector.disabled = allCores.length === 0;
}

function renderShipCores() {
  ensureValidSelectedIndexes();
  renderCoreSelector();

  if (state.selectedCoreIndex < 0) {
    ids("shipCores").innerHTML = `<p class="muted">No core configuration is selected.</p>`;
    return;
  }

  const coreIndex = state.selectedCoreIndex;
  const core = getEditorCoreByIndex(coreIndex);
  if (!core) {
    ids("shipCores").innerHTML = `<p class="muted">No core configuration is selected.</p>`;
    return;
  }
  const isNoCore = coreIndex === 0;
  const expandedLimitPanels = normalizeExpandedLimitPanelsForCore(coreIndex);

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
        <button data-action="duplicate-core" data-c="${coreIndex}">Duplicate Core</button>
        <button data-action="remove-core" data-c="${coreIndex}" ${isNoCore ? "disabled" : ""}>Delete Core</button>
      </div>
      <div class="row wrap">
        <label class="inline">MaxBlocks <input data-action="core-maxblocks" data-c="${coreIndex}" class="small" type="number" value="${core.maxBlocks}" /></label>
        <label class="inline">MaxMass <input data-action="core-maxmass" data-c="${coreIndex}" class="small" type="number" value="${core.maxMass}" /></label>
        <label class="inline">MaxPCU <input data-action="core-maxpcu" data-c="${coreIndex}" class="small" type="number" value="${core.maxPcu}" /></label>
      </div>
      <div class="row wrap">
        <label class="inline">MaxBackupCores <input data-action="core-maxbackupcores" data-c="${coreIndex}" class="small" type="number" value="${core.maxBackupCores}" /></label>
        <label class="inline">MaxPerPlayer <input data-action="core-maxpp" data-c="${coreIndex}" class="small" type="number" value="${core.maxPerPlayer}" /></label>
        <label class="inline">MinPerFaction (MinPlayers) <input data-action="core-minpf" data-c="${coreIndex}" class="small" type="number" value="${core.minPerFaction}" /></label>
        <label class="inline">MaxPerFaction <input data-action="core-maxpf" data-c="${coreIndex}" class="small" type="number" value="${core.maxPerFaction}" /></label>
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
        <details class="card block-limit-panel" data-action="limit-toggle" data-c="${coreIndex}" data-l="${limitIndex}" ${expandedLimitPanels.includes(limitIndex) ? "open" : ""}>
          <summary class="block-limit-summary ${Array.isArray(limit.blockGroups) && limit.blockGroups.length === 0 ? "block-limit-summary--missing-groups" : ""}">${escapeXml(limit.name?.trim() || `Block Limit ${limitIndex + 1}`)}</summary>
          <div class="block-limit-content">
          <div class="row wrap">
            <input data-action="limit-name" data-c="${coreIndex}" data-l="${limitIndex}" class="small" placeholder="Limit Name" value="${escapeXml(limit.name)}" />
            <label class="inline">MaxCount <input data-action="limit-max" data-c="${coreIndex}" data-l="${limitIndex}" class="small" type="number" step="0.1" value="${limit.maxCount}" /></label>
            <button data-action="duplicate-limit" data-c="${coreIndex}" data-l="${limitIndex}">Duplicate Limit</button>
            <button data-action="remove-limit" data-c="${coreIndex}" data-l="${limitIndex}">Delete Limit</button>
          </div>
          <div class="row wrap">
            <label class="inline">CrossConnectorPunishment <input data-action="limit-cross-connector" data-c="${coreIndex}" data-l="${limitIndex}" type="checkbox" ${limit.crossConnectorPunishment ? "checked" : ""}/></label>
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
            <input data-action="limit-group-search" data-c="${coreIndex}" data-l="${limitIndex}" class="small group-search" placeholder="Search block groups" value="${escapeXml(limit.groupSearch || "")}" />
            <div class="group-checklist" data-limit-group-list data-c="${coreIndex}" data-l="${limitIndex}">
              ${blockGroupCheckboxes(coreIndex, limitIndex, limit.blockGroups, limit.groupSearch || "")}
            </div>
          </div>
          </div>
        </details>
      `).join("")}
    </div>
  `;
}

function renderLimitGroupChecklist(coreIndex, limitIndex) {
  const limit = getEditorCoreByIndex(coreIndex)?.blockLimits?.[limitIndex];
  if (!limit) return;

  const listElement = document.querySelector(`[data-limit-group-list][data-c="${coreIndex}"][data-l="${limitIndex}"]`);
  if (!listElement) return;

  listElement.innerHTML = blockGroupCheckboxes(coreIndex, limitIndex, limit.blockGroups, limit.groupSearch || "");
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
    maxBackupCores: numberOf(coreNode, "MaxBackupCores", -1),
    maxPerFaction: numberOf(coreNode, "MaxPerFaction", -1),
    minPerFaction: numberOf(coreNode, "MinPlayers", numberOf(coreNode, "MinPerFaction", -1)),
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
      crossConnectorPunishment: boolOf(limitNode, "CrossConnectorPunishment", false),
      punishByNoFlyZone: boolOf(limitNode, "PunishByNoFlyZone", boolOf(limitNode, "TurnedOffByNoFlyZone", false)),
      punishmentType: textOf(limitNode, "PunishmentType") || "ShutOff",
      allowedDirections: Array.from(limitNode.querySelectorAll(":scope > AllowedDirections")).map((node) => node.textContent.trim()).filter(Boolean),
      blockGroups: Array.from(limitNode.querySelectorAll(":scope > BlockGroups")).map((node) => node.textContent.trim()).filter(Boolean),
      groupSearch: ""
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

function sanitizeToken(value) {
  return String(value ?? "")
    .trim()
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "") || "Unnamed";
}

function parseLegacyMobility(gridClassNode) {
  const largeStatic = boolOf(gridClassNode, "LargeGridStatic", true);
  const largeMobile = boolOf(gridClassNode, "LargeGridMobile", true);
  if (largeStatic && !largeMobile) return "Static";
  if (!largeStatic && largeMobile) return "Mobile";
  return "Both";
}

function normalizeBlockTypeSignature(blockTypes, mergeMode = "strict") {
  const normalized = (blockTypes || [])
    .map((blockType) => ({
      typeId: String(blockType?.typeId || "").trim(),
      subtypeId: String(blockType?.subtypeId || "").trim(),
      countWeight: Number(blockType?.countWeight)
    }))
    .filter((blockType) => blockType.typeId || blockType.subtypeId);

  if (mergeMode === "typesOnly") {
    return Array.from(new Set(normalized.map((blockType) => `${blockType.typeId}|${blockType.subtypeId}`)))
      .sort((a, b) => a.localeCompare(b))
      .join("\n");
  }

  return normalized
    .map((blockType) => `${blockType.typeId}|${blockType.subtypeId}|${blockType.countWeight}`)
    .sort((a, b) => a.localeCompare(b))
    .join("\n");
}

function legacyCoreName(core) {
  const uniqueName = String(core?.uniqueName || "").trim();
  if (uniqueName) return uniqueName;
  const subtypeId = String(core?.subtypeId || "").trim();
  if (subtypeId) return subtypeId;
  return "Core";
}

function parseLegacyBlockTypes(limitNode) {
  return Array.from(limitNode.querySelectorAll(":scope > BlockTypes > BlockType"))
    .map((blockNode) => ({
      typeId: textOf(blockNode, "TypeId"),
      subtypeId: textOf(blockNode, "SubtypeId"),
      countWeight: numberOf(blockNode, "CountWeight", 1)
    }))
    .filter((blockType) => blockType.typeId || blockType.subtypeId);
}

function parseLegacyLimit(limitNode) {
  return {
    name: textOf(limitNode, "Name"),
    maxCount: numberOf(limitNode, "MaxCount", 0),
    crossConnectorPunishment: boolOf(limitNode, "CrossConnectorPunishment", false),
    punishByNoFlyZone: boolOf(limitNode, "TurnedOffByNoFlyZone", boolOf(limitNode, "PunishByNoFlyZone", false)),
    punishmentType: textOf(limitNode, "PunishmentType") || "ShutOff",
    allowedDirections: [],
    blockTypes: parseLegacyBlockTypes(limitNode),
    blockGroups: [],
    groupSearch: ""
  };
}

function parseLegacyGridClass(gridClassNode, fallbackSubtype) {
  const subtype = sanitizeToken(textOf(gridClassNode, "Name") || fallbackSubtype);
  const mapped = {
    ...createDefaultCore(),
    subtypeId: subtype,
    uniqueName: textOf(gridClassNode, "Name") || subtype,
    mobilityType: parseLegacyMobility(gridClassNode),
    maxBlocks: numberOf(gridClassNode, "MaxBlocks", -1),
    maxMass: numberOf(gridClassNode, "MaxMass", -1),
    maxPcu: numberOf(gridClassNode, "MaxPCU", -1),
    maxPerFaction: numberOf(gridClassNode, "MaxPerFaction", -1),
    minPerFaction: numberOf(gridClassNode, "MinPlayers", -1),
    maxPerPlayer: numberOf(gridClassNode, "MaxPerPlayer", -1),
    forceBroadcast: boolOf(gridClassNode, "ForceBroadCast", false),
    forceBroadcastRange: numberOf(gridClassNode, "ForceBroadCastRange", 2000),
    speedBoostEnabled: true,
    modifiers: parseModifierNode(gridClassNode.querySelector(":scope > Modifiers"), DEFAULT_GRID_MODIFIERS),
    passiveDefenseModifiers: parseModifierNode(gridClassNode.querySelector(":scope > DamageModifiers"), DEFAULT_DEFENSE_MODIFIERS),
    blockLimits: Array.from(gridClassNode.querySelectorAll(":scope > BlockLimits > BlockLimit")).map(parseLegacyLimit)
  };

  if (mapped.modifiers.DrillHarvestMultiplier === DEFAULT_GRID_MODIFIERS.DrillHarvestMultiplier) {
    const modifiersNode = gridClassNode.querySelector(":scope > Modifiers");
    mapped.modifiers.DrillHarvestMultiplier = numberOf(modifiersNode, "DrillHarvestMultipler", mapped.modifiers.DrillHarvestMultiplier);
  }

  return mapped;
}

function findLegacyDefaultGridClassNode(root) {
  const selectors = [
    ":scope > DefaultGridClass",
    ":scope > GridClasses > DefaultGridClass",
    ":scope > ShipClasses > DefaultGridClass",
    ":scope > ShipClasses > ShipClass > DefaultGridClass"
  ];

  for (const selector of selectors) {
    const found = root.querySelector(selector);
    if (found) return found;
  }

  return null;
}

function findLegacyShipGridClassNodes(root) {
  const selectors = [
    ":scope > GridClasses > GridClass",
    ":scope > ShipClasses > GridClass",
    ":scope > ShipClasses > ShipClass > GridClass"
  ];

  const nodes = [];
  selectors.forEach((selector) => {
    root.querySelectorAll(selector).forEach((node) => {
      if (!nodes.includes(node)) nodes.push(node);
    });
  });

  return nodes;
}

function applyLegacyGroupDedup(noCoreCore, shipCores, mergeMode = "strict") {
  const allCores = [noCoreCore, ...shipCores].filter(Boolean);
  const signatureToGroupName = new Map();
  const generatedGroups = [];

  allCores.forEach((core) => {
    core.blockLimits.forEach((limit) => {
      const signature = normalizeBlockTypeSignature(limit.blockTypes, mergeMode);
      const existing = signatureToGroupName.get(signature);
      if (existing) {
        limit.blockGroups = [existing];
        return;
      }

      const limitName = limit.name?.trim() || "UnnamedLimit";
      const groupName = `${sanitizeToken(limitName)}__${sanitizeToken(legacyCoreName(core))}`;
      signatureToGroupName.set(signature, groupName);
      generatedGroups.push({ name: groupName, blockTypes: limit.blockTypes });
      limit.blockGroups = [groupName];
    });
  });

  const deduped = [];
  const seenByName = new Map();
  generatedGroups.forEach((group) => {
    const signature = normalizeBlockTypeSignature(group.blockTypes, mergeMode);
    const existing = seenByName.get(group.name);
    if (!existing) {
      seenByName.set(group.name, signature);
      deduped.push(group);
      return;
    }

    if (existing === signature) return;

    let suffix = 2;
    let nextName = `${group.name}_${suffix}`;
    while (seenByName.has(nextName)) {
      suffix += 1;
      nextName = `${group.name}_${suffix}`;
    }
    seenByName.set(nextName, signature);
    const oldName = group.name;
    deduped.push({ ...group, name: nextName });

    allCores.forEach((core) => {
      core.blockLimits.forEach((limit) => {
        if (Array.isArray(limit.blockGroups)) {
          limit.blockGroups = limit.blockGroups.map((name) => (name === oldName ? nextName : name));
        }
      });
    });
  });

  allCores.forEach((core) => {
    core.blockLimits.forEach((limit) => { delete limit.blockTypes; });
  });

  return deduped;
}

function migrateLegacyModConfig(text, sourceName = "uploaded xml", mergeMode = "strict") {
  const doc = parseXml(text);
  const root = doc.querySelector("ModConfig");
  if (!root) return { error: `No <ModConfig> root found in ${sourceName}.` };

  const defaultGridClassNode = findLegacyDefaultGridClassNode(root);
  const gridClassNodes = findLegacyShipGridClassNodes(root);

  if (!defaultGridClassNode && gridClassNodes.length === 0) {
    return { error: `No <DefaultGridClass> or <GridClass> entries found in ${sourceName}.` };
  }

  const noCoreCore = defaultGridClassNode ? parseLegacyGridClass(defaultGridClassNode, "NoCore") : null;
  if (noCoreCore) {
    noCoreCore.subtypeId = "NO_CORE_DEFAULT";
    noCoreCore.uniqueName = noCoreCore.uniqueName || "No Core";
    noCoreCore.originalFileName = "ShipCoreConfig_No_Core.xml";
  }

  const shipGridClassNodes = gridClassNodes.filter((node) => node !== defaultGridClassNode);
  const shipCores = shipGridClassNodes.map((node, index) => parseLegacyGridClass(node, `GridClass_${index + 1}`));
  const blockGroups = applyLegacyGroupDedup(noCoreCore, shipCores, mergeMode);

  const status = [
    `Migrated legacy file ${sourceName}.`,
    noCoreCore ? "Mapped DefaultGridClass into ShipCoreConfig_No_Core.xml." : "No DefaultGridClass found.",
    `Mapped ${shipCores.length} GridClass entries to ShipCore cores (excluding the default no-core class).`,
    `Generated ${blockGroups.length} reusable block groups via cross-core limit dedupe (${mergeMode}).`
  ];

  return { noCoreCore, shipCores, blockGroups, status };
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

function generateXml(options = {}) {
  const { persistDraft = true } = options;
  const header = '<?xml version="1.0" encoding="UTF-8"?>';

  const noCore = state.noCoreCore
    ? `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(state.noCoreCore.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(state.noCoreCore.uniqueName)}</UniqueName>\n  <ForceBroadCast>${state.noCoreCore.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${state.noCoreCore.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(state.noCoreCore.mobilityType)}</MobilityType>\n  <MaxBlocks>${state.noCoreCore.maxBlocks}</MaxBlocks>\n  <MaxMass>${state.noCoreCore.maxMass}</MaxMass>\n  <MaxPCU>${state.noCoreCore.maxPcu}</MaxPCU>\n  <MaxBackupCores>${state.noCoreCore.maxBackupCores}</MaxBackupCores>\n  <MaxPerPlayer>${state.noCoreCore.maxPerPlayer}</MaxPerPlayer>\n  <MinPlayers>${state.noCoreCore.minPerFaction}</MinPlayers>\n  <MaxPerFaction>${state.noCoreCore.maxPerFaction}</MaxPerFaction>\n  <SpeedBoostEnabled>${state.noCoreCore.speedBoostEnabled}</SpeedBoostEnabled>\n  <SpeedLimitType>${escapeXml(state.noCoreCore.speedLimitType)}</SpeedLimitType>\n  <EnableActiveDefenseModifiers>${state.noCoreCore.enableActiveDefenseModifiers}</EnableActiveDefenseModifiers>\n${writeModifierXml("Modifiers", state.noCoreCore.modifiers, DEFAULT_GRID_MODIFIERS)}\n${writeModifierXml("SpeedModifiers", state.noCoreCore.speedModifiers, DEFAULT_SPEED_MODIFIERS)}\n${writeModifierXml("PassiveDefenseModifiers", state.noCoreCore.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${writeModifierXml("ActiveDefenseModifiers", state.noCoreCore.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${state.noCoreCore.blockLimits
      .map((limit) => `  <BlockLimits>\n    <Name>${escapeXml(limit.name)}</Name>\n${limit.blockGroups.map((g) => `    <BlockGroups>${escapeXml(g)}</BlockGroups>`).join("\n")}\n    <MaxCount>${limit.maxCount}</MaxCount>\n    <CrossConnectorPunishment>${limit.crossConnectorPunishment}</CrossConnectorPunishment>\n    <PunishByNoFlyZone>${limit.punishByNoFlyZone}</PunishByNoFlyZone>\n    <PunishmentType>${escapeXml(limit.punishmentType)}</PunishmentType>\n${(limit.allowedDirections || []).filter(Boolean).map((d) => `    <AllowedDirections>${escapeXml(d)}</AllowedDirections>`).join("\n")}\n  </BlockLimits>`)
      .join("\n")}\n</ShipCore>`
    : `${header}\n<ShipCore />`;

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
    body: `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(core.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(core.uniqueName)}</UniqueName>\n  <ForceBroadCast>${core.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${core.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(core.mobilityType)}</MobilityType>\n  <MaxBlocks>${core.maxBlocks}</MaxBlocks>\n  <MaxMass>${core.maxMass}</MaxMass>\n  <MaxPCU>${core.maxPcu}</MaxPCU>\n  <MaxBackupCores>${core.maxBackupCores}</MaxBackupCores>\n  <MaxPerPlayer>${core.maxPerPlayer}</MaxPerPlayer>\n  <MinPlayers>${core.minPerFaction}</MinPlayers>\n  <MaxPerFaction>${core.maxPerFaction}</MaxPerFaction>\n  <SpeedBoostEnabled>${core.speedBoostEnabled}</SpeedBoostEnabled>\n  <SpeedLimitType>${escapeXml(core.speedLimitType)}</SpeedLimitType>\n  <EnableActiveDefenseModifiers>${core.enableActiveDefenseModifiers}</EnableActiveDefenseModifiers>\n${writeModifierXml("Modifiers", core.modifiers, DEFAULT_GRID_MODIFIERS)}\n${writeModifierXml("SpeedModifiers", core.speedModifiers, DEFAULT_SPEED_MODIFIERS)}\n${writeModifierXml("PassiveDefenseModifiers", core.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${writeModifierXml("ActiveDefenseModifiers", core.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${core.blockLimits
      .map((limit) => `  <BlockLimits>\n    <Name>${escapeXml(limit.name)}</Name>\n${limit.blockGroups.map((g) => `    <BlockGroups>${escapeXml(g)}</BlockGroups>`).join("\n")}\n    <MaxCount>${limit.maxCount}</MaxCount>\n    <CrossConnectorPunishment>${limit.crossConnectorPunishment}</CrossConnectorPunishment>\n    <PunishByNoFlyZone>${limit.punishByNoFlyZone}</PunishByNoFlyZone>\n    <PunishmentType>${escapeXml(limit.punishmentType)}</PunishmentType>\n${(limit.allowedDirections || []).filter(Boolean).map((d) => `    <AllowedDirections>${escapeXml(d)}</AllowedDirections>`).join("\n")}\n  </BlockLimits>`)
      .join("\n")}\n</ShipCore>`
  }));

  ids("noCoreXml").textContent = noCore;
  ids("groupsXml").textContent = groups;
  ids("manifestXml").textContent = manifest;
  ids("coresXml").textContent = cores.map((core) => `===== ${core.file} =====\n${core.body}`).join("\n\n");
  if (persistDraft) persistDraftToStorage();
  return { noCore, groups, manifest, cores };
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
  if (action === "duplicate-group") {
    const sourceGroup = state.blockGroups[groupIndex];
    if (sourceGroup) {
      const duplicateName = createIncrementedDuplicateName(
        sourceGroup.name,
        state.blockGroups.map((group) => group.name),
        "BlockGroup"
      );
      const duplicatedGroup = {
        name: duplicateName,
        blockTypes: (sourceGroup.blockTypes || []).map((blockType) => cloneBlockType(blockType))
      };

      state.blockGroups.splice(groupIndex + 1, 0, duplicatedGroup);
      state.selectedGroupIndex = groupIndex + 1;
      didMutate = true;
    }
  }
  if (action === "remove-core") {
    if (coreIndex === 0) return;

    state.shipCores.splice(coreIndex - 1, 1);
    shiftExpandedLimitPanelsAfterCoreRemoval(coreIndex);
    const maxEditorIndex = state.shipCores.length + (state.noCoreCore ? 1 : 0) - 1;
    if (state.selectedCoreIndex > maxEditorIndex) state.selectedCoreIndex = maxEditorIndex;
    didMutate = true;
  }
  if (action === "duplicate-core") {
    const sourceCore = getEditorCoreByIndex(coreIndex);
    if (sourceCore) {
      const duplicatedCore = cloneShipCore(sourceCore);
      const insertShipCoreIndex = coreIndex;
      state.shipCores.splice(insertShipCoreIndex, 0, duplicatedCore);

      const shifted = {};
      Object.entries(state.expandedLimitPanelsByCore).forEach(([key, value]) => {
        const index = Number(key);
        if (!Number.isInteger(index)) return;
        shifted[index > coreIndex ? index + 1 : index] = value;
      });
      state.expandedLimitPanelsByCore = shifted;
      state.expandedLimitPanelsByCore[coreIndex + 1] = [];

      state.selectedCoreIndex = coreIndex + 1;
      didMutate = true;
    }
  }
  if (action === "add-limit") {
    const core = getEditorCoreByIndex(coreIndex);
    if (!core) return;
    core.blockLimits.push(createDefaultLimit());
    normalizeExpandedLimitPanelsForCore(coreIndex);
    didMutate = true;
  }
  if (action === "duplicate-limit") {
    const sourceLimit = getEditorCoreByIndex(coreIndex)?.blockLimits?.[limitIndex];
    if (sourceLimit) {
      getEditorCoreByIndex(coreIndex).blockLimits.splice(limitIndex + 1, 0, cloneLimit(sourceLimit));
      normalizeExpandedLimitPanelsForCore(coreIndex);
      didMutate = true;
    }
  }
  if (action === "remove-limit") {
    const core = getEditorCoreByIndex(coreIndex);
    if (!core) return;
    core.blockLimits.splice(limitIndex, 1);
    normalizeExpandedLimitPanelsForCore(coreIndex);
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

  if (action === "bt-type") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].typeId = target.value;
  if (action === "bt-subtype") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].subtypeId = target.value;
  if (action === "bt-weight") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].countWeight = Number(target.value || 0);

  const requiresCore = action.startsWith("core-") || action.startsWith("limit-");
  const core = requiresCore ? getEditorCoreByIndex(coreIndex) : null;
  if (requiresCore && !core) return;

  if (action === "core-unique") core.uniqueName = target.value;
  if (action === "core-maxblocks") core.maxBlocks = Number(target.value || -1);
  if (action === "core-maxmass") core.maxMass = Number(target.value || -1);
  if (action === "core-maxpcu") core.maxPcu = Number(target.value || -1);
  if (action === "core-maxbackupcores") core.maxBackupCores = Number(target.value || -1);
  if (action === "core-maxpf") core.maxPerFaction = Number(target.value || -1);
  if (action === "core-minpf") core.minPerFaction = Number(target.value || -1);
  if (action === "core-maxpp") core.maxPerPlayer = Number(target.value || -1);
  if (action === "core-fbr") core.forceBroadcastRange = Number(target.value || 0);

  if (action === "core-modifier-grid") core.modifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-speed") core.speedModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-passive-defense") core.passiveDefenseModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-active-defense") core.activeDefenseModifiers[target.dataset.m] = Number(target.value || 0);

  if (action === "limit-name") core.blockLimits[limitIndex].name = target.value;
  if (action === "limit-max") core.blockLimits[limitIndex].maxCount = Number(target.value || 0);
  if (action === "limit-group-search") {
    core.blockLimits[limitIndex].groupSearch = target.value;
    renderLimitGroupChecklist(coreIndex, limitIndex);
  }

  generateXml();
});

function commitDeferredTextInput(target) {
  if (!(target instanceof HTMLInputElement)) return;

  const action = target.dataset.action;
  const groupIndex = Number(target.dataset.g);
  const coreIndex = Number(target.dataset.c);

  if (action === "group-name") {
    const previousName = state.blockGroups[groupIndex].name;
    const nextName = target.value;
    if (previousName === nextName) return;

    state.blockGroups[groupIndex].name = nextName;
    renameBlockGroupReferences(previousName, nextName);
    renderGroupSelector();
    renderShipCores();
    generateXml();
  }

  if (action === "core-subtype") {
    const core = getEditorCoreByIndex(coreIndex);
    if (!core) return;
    const previousSubtype = core.subtypeId;
    const nextSubtype = target.value;
    if (previousSubtype === nextSubtype) return;

    core.subtypeId = nextSubtype;
    renderShipCores();
    generateXml();
  }
}

function flushActiveDeferredInput() {
  const activeElement = document.activeElement;
  if (!(activeElement instanceof HTMLInputElement)) return;

  const action = activeElement.dataset.action;
  if (action !== "group-name" && action !== "core-subtype") return;

  commitDeferredTextInput(activeElement);
}

document.addEventListener("keydown", (event) => {
  if (event.key !== "Enter") return;

  const target = event.target;
  if (!(target instanceof HTMLInputElement)) return;

  const action = target.dataset.action;
  if (action !== "group-name" && action !== "core-subtype") return;

  target.blur();
});

document.addEventListener("change", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);

  const inputElement = target instanceof HTMLInputElement ? target : null;
  const selectElement = target instanceof HTMLSelectElement ? target : null;

  if (action === "group-name" || action === "core-subtype") {
    commitDeferredTextInput(target);
    return;
  }

  const core = getEditorCoreByIndex(coreIndex);
  if (!core) return;

  if (action === "core-fb" && inputElement) core.forceBroadcast = inputElement.checked;
  if (action === "core-mobility" && selectElement) core.mobilityType = selectElement.value;
  if (action === "core-speedboost" && inputElement) core.speedBoostEnabled = inputElement.checked;
  if (action === "core-enable-active-defense" && inputElement) core.enableActiveDefenseModifiers = inputElement.checked;
  if (action === "core-speed-limit-type" && selectElement) core.speedLimitType = selectElement.value;
  if (action === "limit-punish" && inputElement) {
    core.blockLimits[limitIndex].punishByNoFlyZone = inputElement.checked;
    renderShipCores();
  }
  if (action === "limit-cross-connector" && inputElement) {
    core.blockLimits[limitIndex].crossConnectorPunishment = inputElement.checked;
    renderShipCores();
  }
  if (action === "limit-type" && selectElement) {
    core.blockLimits[limitIndex].punishmentType = selectElement.value;
    renderShipCores();
  }
  if (action === "limit-direction-toggle" && inputElement) {
    const limit = core.blockLimits[limitIndex];
    const direction = inputElement.dataset.direction || "";
    if (!direction) return;

    const selectedSet = new Set(limit.allowedDirections || []);
    if (inputElement.checked) selectedSet.add(direction);
    else selectedSet.delete(direction);
    limit.allowedDirections = VALID_DIRECTIONS.filter((value) => selectedSet.has(value));
    renderShipCores();
  }
  if (action === "limit-group-toggle" && inputElement) {
    const limit = core.blockLimits[limitIndex];
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

document.addEventListener("toggle", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLDetailsElement)) return;
  if (target.dataset.action !== "limit-toggle") return;

  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);
  if (!Number.isInteger(coreIndex) || !Number.isInteger(limitIndex)) return;

  const expanded = normalizeExpandedLimitPanelsForCore(coreIndex);
  if (target.open) {
    const nextExpanded = [...expanded.filter((index) => index !== limitIndex), limitIndex];
    while (nextExpanded.length > 2) {
      const removedLimitIndex = nextExpanded.shift();
      const detailsToClose = document.querySelector(`details[data-action="limit-toggle"][data-c="${coreIndex}"][data-l="${removedLimitIndex}"]`);
      if (detailsToClose instanceof HTMLDetailsElement) detailsToClose.open = false;
    }
    state.expandedLimitPanelsByCore[coreIndex] = nextExpanded;
    return;
  }

  state.expandedLimitPanelsByCore[coreIndex] = expanded.filter((index) => index !== limitIndex);
}, true);

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
ids("generateXml").addEventListener("click", () => {
  flushActiveDeferredInput();
  generateXml();
});
ids("resetEditor").addEventListener("click", () => {
  resetEditor(true);
  setImportStatus(["Editor reset to starter seed data."]);
});

ids("resetDraftStorage").addEventListener("click", () => {
  clearDraftFromStorage();
  setImportStatus(["Cleared autosaved draft from browser storage."]);
});

ids("downloadNoCore").addEventListener("click", () => {
  flushActiveDeferredInput();
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_No_Core.xml", xml.noCore);
  clearDraftFromStorage();
});
ids("downloadGroups").addEventListener("click", () => {
  flushActiveDeferredInput();
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_Groups.xml", xml.groups);
  clearDraftFromStorage();
});
ids("downloadManifest").addEventListener("click", () => {
  flushActiveDeferredInput();
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_Manifest.xml", xml.manifest);
  clearDraftFromStorage();
});
ids("downloadCores").addEventListener("click", () => {
  flushActiveDeferredInput();
  const xml = generateXml({ persistDraft: false });
  const zip = createZip(xml.cores.map((core) => ({ name: core.file, content: core.body })));
  downloadBlob("ShipCore_XMLs.zip", zip);
  clearDraftFromStorage();
});

ids("loadLegacyModConfig").addEventListener("click", async () => {
  const legacyFile = ids("legacyModConfigFile").files?.[0];
  const mergeMode = ids("legacyLimitMergeMode").value === "typesOnly" ? "typesOnly" : "strict";
  if (!legacyFile) {
    setImportStatus(["No legacy ModConfig XML selected."]);
    return;
  }

  const migrated = migrateLegacyModConfig(await legacyFile.text(), legacyFile.name, mergeMode);
  if (migrated.error) {
    setImportStatus([migrated.error]);
    return;
  }

  state.noCoreCore = migrated.noCoreCore;
  state.shipCores = migrated.shipCores;
  state.blockGroups = migrated.blockGroups;
  state.selectedGroupIndex = 0;
  state.selectedCoreIndex = 0;
  state.expandedLimitPanelsByCore = {};

  renderBlockGroups();
  renderShipCores();
  generateXml();
  setImportStatus(migrated.status);
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

    if (parsed.subtypeId === "NO_CORE_DEFAULT") {
      state.noCoreCore = parsed;
      status.push(`Loaded no-core from ${file.name}.`);
      continue;
    }

    state.shipCores.push(parsed);
    status.push(`Loaded core '${parsed.subtypeId || file.name}'.`);
  }

  state.selectedCoreIndex = 0;
  state.expandedLimitPanelsByCore = {};

  if (state.noCoreCore) status.push(`Loaded no-core from ${state.noCoreCore.originalFileName || "legacy import"}.`);
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

  if (restoreDraftFromStorage()) {
    renderBlockGroups();
    renderShipCores();
    generateXml();
    setImportStatus(["Restored autosaved draft from your browser storage."]);
    return;
  }

  resetEditor(true);
  setImportStatus(["Tip: Upload existing XML files to renovate and continue editing."]);
})();
