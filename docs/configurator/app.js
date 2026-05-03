// app.js build v1010
const state = {
  schema: {},
  blockGroups: [],
  manifestGroups: [],
  shipCores: [],
  upgradeModules: [],
  outputCoreDirectory: "Data/Cores/",
  outputUpgradeModuleDirectory: "Data/UpgradeModules/",
  selectedGroupIndex: 0,
  selectedManifestGroupIndex: 0,
  selectedCoreIndex: 0,
  selectedUpgradeModuleIndex: 0,
  noCoreCore: null,
  expandedLimitPanelsByCore: {},
  // Files from the original upload that are passed through unchanged into Download All
  // Each entry: { name: string (zip path), getText: () => Promise<string> }
  uploadedPassthroughFiles: [],
  // Full output path from manifest per core filename (lowercase) e.g. "frigate_core.xml" -> "Data/Cores/Combat Grids/Frigate_Core.xml"
  manifestCoreFullPathByFilename: new Map()
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


const DEFAULT_UPGRADE_MODULE = {
  subtypeId: "",
  uniqueName: "",
  modifiers: [],
  blockLimitModifiers: []
};

const DEFAULT_UPGRADE_STAT_MODIFIER = {
  stat: "",
  value: 0,
  modifierType: "Multiplicative"
};

const DEFAULT_BLOCK_LIMIT_MODIFIER = {
  blockLimitName: "",
  value: 0,
  modifierType: "Additive"
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
  // Strip the XML declaration — some browsers reject encoding declarations
  // in strings passed to DOMParser with application/xml.
  const stripped = text.replace(/^\s*<\?xml[^?]*\?>\s*/i, "");
  return new DOMParser().parseFromString(stripped, "application/xml");
}

// Get the XML root element, safe against parse errors
function xmlRoot(doc) {
  const root = doc?.documentElement;
  if (!root || root.nodeName === "parsererror" || root.localName === "parsererror") return null;
  return root;
}

// Namespace-safe querySelector: tries plain selector first, falls back to
// getElementsByTagName which ignores namespaces entirely.
function qsel(node, tag) {
  if (!node) return null;
  try {
    const r = node.querySelector(tag);
    if (r) return r;
  } catch (_) {}
  // getElementsByTagName with "*" local name trick — strip any prefix
  const local = tag.split(":").pop();
  const list = node.getElementsByTagName(local);
  if (list.length) return list[0];
  // Also try the wildcard namespace version
  const listStar = node.getElementsByTagNameNS("*", local);
  return listStar.length ? listStar[0] : null;
}

// Namespace-safe querySelectorAll
function qselAll(node, tag) {
  if (!node) return [];
  const local = tag.split(":").pop();
  const list = node.getElementsByTagName(local);
  if (list.length) return Array.from(list);
  const listStar = node.getElementsByTagNameNS("*", local);
  return Array.from(listStar);
}

function cloneBlockGroup(group = { name: "", blockTypes: [] }) {
  return {
    name: String(group.name ?? ""),
    blockTypes: Array.isArray(group.blockTypes) ? group.blockTypes.map((blockType) => cloneBlockType(blockType)) : []
  };
}

function createDefaultManifestGroup() {
  return {
    name: "",
    maxCount: 1
  };
}

function cloneManifestGroup(group = createDefaultManifestGroup()) {
  return {
    ...createDefaultManifestGroup(),
    ...group,
    name: String(group?.name ?? ""),
    maxCount: Number.isFinite(Number(group?.maxCount)) ? Number(group.maxCount) : 1
  };
}

function persistDraftToStorage() {
  try {
    const payload = {
      blockGroups: state.blockGroups.map((group) => cloneBlockGroup(group)),
      manifestGroups: state.manifestGroups.map((group) => cloneManifestGroup(group)),
      shipCores: state.shipCores.map((core) => cloneShipCore(core)),
      upgradeModules: state.upgradeModules.map((module) => cloneUpgradeModule(module)),
      noCoreCore: state.noCoreCore ? cloneShipCore(state.noCoreCore) : null,
      outputCoreDirectory: state.outputCoreDirectory,
      outputUpgradeModuleDirectory: state.outputUpgradeModuleDirectory,
      selectedGroupIndex: state.selectedGroupIndex,
      selectedManifestGroupIndex: state.selectedManifestGroupIndex,
      selectedCoreIndex: state.selectedCoreIndex,
      selectedUpgradeModuleIndex: state.selectedUpgradeModuleIndex,
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
    state.manifestGroups = Array.isArray(parsedDraft.manifestGroups)
      ? parsedDraft.manifestGroups.map((group) => cloneManifestGroup(group))
      : [];
    state.shipCores = Array.isArray(parsedDraft.shipCores)
      ? parsedDraft.shipCores.map((core) => cloneShipCore(core))
      : [];
    state.upgradeModules = Array.isArray(parsedDraft.upgradeModules)
      ? parsedDraft.upgradeModules.map((module) => cloneUpgradeModule(module))
      : [];
    state.noCoreCore = parsedDraft.noCoreCore ? cloneShipCore(parsedDraft.noCoreCore) : null;
    state.outputCoreDirectory = normalizeOutputDirectory(parsedDraft.outputCoreDirectory, "Data/Cores/");
    state.outputUpgradeModuleDirectory = normalizeOutputDirectory(parsedDraft.outputUpgradeModuleDirectory, "Data/UpgradeModules/");
    state.selectedGroupIndex = Number.isInteger(parsedDraft.selectedGroupIndex) ? parsedDraft.selectedGroupIndex : 0;
    state.selectedManifestGroupIndex = Number.isInteger(parsedDraft.selectedManifestGroupIndex) ? parsedDraft.selectedManifestGroupIndex : 0;
    state.selectedCoreIndex = Number.isInteger(parsedDraft.selectedCoreIndex) ? parsedDraft.selectedCoreIndex : 0;
    state.selectedUpgradeModuleIndex = Number.isInteger(parsedDraft.selectedUpgradeModuleIndex) ? parsedDraft.selectedUpgradeModuleIndex : 0;
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
  return qsel(parent, tag)?.textContent?.trim() ?? "";
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

function normalizeOutputDirectory(path, fallbackDirectory) {
  if (typeof path !== "string") return fallbackDirectory;
  const normalizedPath = path.replaceAll("\\", "/").trim().replace(/^\/+/, "");
  if (!normalizedPath) return fallbackDirectory;
  return normalizedPath.endsWith("/") ? normalizedPath : `${normalizedPath}/`;
}

function getDirectoryFromManifestPath(path, fallbackDirectory) {
  if (typeof path !== "string") return fallbackDirectory;
  const normalizedPath = path.replaceAll("\\", "/").trim().replace(/^\/+/, "");
  if (!normalizedPath.includes("/")) return fallbackDirectory;
  return normalizeOutputDirectory(normalizedPath.slice(0, normalizedPath.lastIndexOf("/")), fallbackDirectory);
}

function getManifestDirectory(paths, fallbackDirectory, label, status) {
  const directories = Array.from(new Set(
    paths
      .map((path) => getDirectoryFromManifestPath(path, fallbackDirectory))
      .filter(Boolean)
  ));

  if (!directories.length) return fallbackDirectory;
  if (directories.length > 1) {
    status.push(`Manifest listed multiple ${label} directories; using '${directories[0]}' for generated output zips.`);
  }
  return directories[0];
}

function dedupeStrings(values = []) {
  const seen = new Set();
  return values.filter((value) => {
    const normalized = String(value ?? "").trim();
    if (!normalized) return false;
    const lowered = normalized.toLowerCase();
    if (seen.has(lowered)) return false;
    seen.add(lowered);
    return true;
  });
}

function normalizeManifestGroupNames(groupNames = [], manifestGroups = state.manifestGroups) {
  const canonicalNames = new Map(
    (manifestGroups || [])
      .filter((group) => String(group?.name ?? "").trim())
      .map((group) => [String(group.name).trim().toLowerCase(), String(group.name).trim()])
  );

  return dedupeStrings((groupNames || []).map((groupName) => {
    const normalized = String(groupName ?? "").trim();
    if (!normalized) return "";
    return canonicalNames.get(normalized.toLowerCase()) || normalized;
  }));
}

function parseManifestXml(text) {
  const doc = parseXml(text);
  const root = xmlRoot(doc);
  if (!root || root.localName !== "CoreManifest") {
    return {
      groups: [],
      shipCores: [],
      upgradeModules: [],
      sourceFormat: "invalid"
    };
  }

  const manifestGroups = qselAll(qsel(root, "ManifestGroups"), "Group")
    .map((groupNode) => cloneManifestGroup({
      name: textOf(groupNode, "Name"),
      maxCount: numberOf(groupNode, "MaxCount", 1)
    }))
    .filter((group) => group.name.trim());

  const currentShipCores = qselAll(root, "ShipCore")
    .map((entryNode) => ({
      filename: textOf(entryNode, "Filename"),
      groups: dedupeStrings(qselAll(entryNode, "Group").map((groupNode) => groupNode.textContent.trim()))
    }))
    .filter((entry) => entry.filename);

  const currentUpgradeModules = qselAll(root, "UpgradeModule")
    .map((entryNode) => ({
      filename: textOf(entryNode, "Filename")
    }))
    .filter((entry) => entry.filename);

  const legacyShipCores = qselAll(root, "ShipCoreFilenames")
    .map((node) => node.textContent.trim())
    .filter(Boolean)
    .map((filename) => ({ filename, groups: [] }));

  const legacyUpgradeModules = qselAll(root, "UpgradeModuleFilenames")
    .map((node) => node.textContent.trim())
    .filter(Boolean)
    .map((filename) => ({ filename }));

  const shipCoreMap = new Map();
  [...legacyShipCores, ...currentShipCores].forEach((entry) => {
    shipCoreMap.set(entry.filename.trim().toLowerCase(), {
      filename: entry.filename.trim(),
      groups: dedupeStrings(entry.groups)
    });
  });

  const upgradeModuleMap = new Map();
  [...legacyUpgradeModules, ...currentUpgradeModules].forEach((entry) => {
    upgradeModuleMap.set(entry.filename.trim().toLowerCase(), {
      filename: entry.filename.trim()
    });
  });

  const sourceFormat = currentShipCores.length || currentUpgradeModules.length || manifestGroups.length
    ? "current"
    : (legacyShipCores.length || legacyUpgradeModules.length ? "legacy" : "empty");

  return {
    groups: manifestGroups,
    shipCores: Array.from(shipCoreMap.values()).map((entry) => ({
      ...entry,
      groups: normalizeManifestGroupNames(entry.groups, manifestGroups)
    })),
    upgradeModules: Array.from(upgradeModuleMap.values()),
    sourceFormat
  };
}

function parseModConfigCs(source) {
  const classes = {};
  const classRegex = /public\s+(?:partial\s+)?class\s+(\w+)\s*\{([\s\S]*?)\n\s*\}/g;
  let classMatch;
  while ((classMatch = classRegex.exec(source)) !== null) {
    const className = classMatch[1];
    const body = classMatch[2];
    const fields = [];
    const fieldRegex = /\[(XmlElement|XmlArray)\("([^"]+)"\)\](?:\s*\[XmlArrayItem\("([^"]+)"\)\])?\s*public\s+([^\s]+(?:\[\])?)\s+(\w+)/g;
    let fieldMatch;
    while ((fieldMatch = fieldRegex.exec(body)) !== null) {
      fields.push({ xmlName: fieldMatch[2], type: fieldMatch[4], name: fieldMatch[5] });
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

function createDefaultNoCore() {
  return {
    ...createDefaultCore(),
    subtypeId: "NO_CORE_DEFAULT",
    uniqueName: "No Core",
    outputDirectory: "",
    originalFileName: "ShipCoreConfig_No_Core.xml"
  };
}

function totalCoreOptions() {
  return 1 + state.shipCores.length;
}

function getCoreBySelectorIndex(selectorIndex) {
  if (selectorIndex === 0) {
    if (!state.noCoreCore) state.noCoreCore = createDefaultNoCore();
    return state.noCoreCore;
  }

  return state.shipCores[selectorIndex - 1] || null;
}


function clearGeneratedFilenameForRenamedCore(coreIndex, core) {
  if (!core || coreIndex === 0) return;
  core.originalFileName = "";
}



function normalizeExpandedLimitPanelsForCore(coreIndex) {
  const core = getCoreBySelectorIndex(coreIndex);
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
    outputDirectory: state.outputCoreDirectory || "Data/Cores/",
    mobilityType: "Both",
    maxBlocks: -1,
    minBlocks: -1,
    maxMass: -1,
    maxPcu: -1,
    maxBackupCores: -1,
    maxPerFaction: -1,
    factionPlayersNeededPerCore: -1,
    minPerFaction: -1,
    maxPlayers: -1,
    maxPerPlayer: -1,
    forceBroadcast: false,
    forceBroadcastRange: 2000,
    speedBoostEnabled: false,
    speedLimitType: "Normal",
    enableActiveDefenseModifiers: false,
    manifestGroups: [],
    allowedUpgradeModules: [],
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
  state.selectedManifestGroupIndex = state.manifestGroups.length
    ? Math.min(Math.max(state.selectedManifestGroupIndex, 0), state.manifestGroups.length - 1)
    : -1;

  if (!state.noCoreCore) state.noCoreCore = createDefaultNoCore();

  state.selectedCoreIndex = Math.min(
    Math.max(state.selectedCoreIndex, 0),
    Math.max(totalCoreOptions() - 1, 0)
  );
}

function addBlockGroup(group = { name: "", blockTypes: [] }) {
  state.blockGroups.push(group);
  state.selectedGroupIndex = state.blockGroups.length - 1;
  renderBlockGroups();
  renderManifestGroups();
  renderShipCores();
}

function addManifestGroup(group = createDefaultManifestGroup()) {
  state.manifestGroups.push(cloneManifestGroup(group));
  state.selectedManifestGroupIndex = state.manifestGroups.length - 1;
  renderManifestGroups();
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

function renameManifestGroupReferences(previousName, nextName) {
  if (!previousName || previousName === nextName) return;
  state.shipCores.forEach((core) => {
    core.manifestGroups = dedupeStrings((core.manifestGroups || []).map((groupName) => (groupName === previousName ? nextName : groupName)));
  });
}

function removeManifestGroupReferences(groupNameToRemove) {
  if (!groupNameToRemove) return;
  state.shipCores.forEach((core) => {
    core.manifestGroups = dedupeStrings((core.manifestGroups || []).filter((groupName) => groupName !== groupNameToRemove));
  });
}

function addShipCore(core = createDefaultCore()) {
  state.shipCores.push({ ...createDefaultCore(), ...core });
  state.selectedCoreIndex = state.shipCores.length;
  renderManifestGroups();
  renderShipCores();
}

function cloneUpgradeModule(module = DEFAULT_UPGRADE_MODULE) {
  return {
    ...DEFAULT_UPGRADE_MODULE,
    ...module,
    subtypeId: String(module?.subtypeId ?? ""),
    uniqueName: String(module?.uniqueName ?? ""),
    modifiers: Array.isArray(module?.modifiers)
      ? module.modifiers.map((modifier) => ({
          ...DEFAULT_UPGRADE_STAT_MODIFIER,
          ...modifier,
          stat: String(modifier?.stat ?? ""),
          value: Number(modifier?.value ?? 0),
          modifierType: modifier?.modifierType === "Additive" ? "Additive" : "Multiplicative"
        }))
      : [],
    blockLimitModifiers: Array.isArray(module?.blockLimitModifiers)
      ? module.blockLimitModifiers.map((modifier) => ({
          ...DEFAULT_BLOCK_LIMIT_MODIFIER,
          ...modifier,
          blockLimitName: String(modifier?.blockLimitName ?? ""),
          value: Number(modifier?.value ?? 0),
          modifierType: modifier?.modifierType === "Multiplicative" ? "Multiplicative" : "Additive"
        }))
      : []
  };
}

function createDefaultUpgradeModule() {
  return cloneUpgradeModule(DEFAULT_UPGRADE_MODULE);
}

function addUpgradeModule(module = createDefaultUpgradeModule()) {
  state.upgradeModules.push(cloneUpgradeModule(module));
  state.selectedUpgradeModuleIndex = Math.max(0, state.upgradeModules.length - 1);
  renderUpgradeModules();
}

function getSelectedUpgradeModule() {
  return state.upgradeModules[state.selectedUpgradeModuleIndex] || null;
}

function cloneShipCore(core = createDefaultCore()) {
  return {
    ...createDefaultCore(),
    ...core,
    outputDirectory: normalizeOutputDirectory(core.outputDirectory, state.outputCoreDirectory || "Data/Cores/"),
    manifestGroups: normalizeManifestGroupNames(Array.isArray(core?.manifestGroups) ? core.manifestGroups.map((groupName) => String(groupName ?? "").trim()) : []),
    allowedUpgradeModules: Array.isArray(core.allowedUpgradeModules)
      ? core.allowedUpgradeModules.map((entry) => ({
          subtypeId: String(entry.subtypeId ?? ""),
          maxCount: Number(entry.maxCount ?? 0)
        }))
      : [],
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
  state.manifestGroups = [];
  state.shipCores = [];
  state.upgradeModules = [];
  state.outputCoreDirectory = "Data/Cores/";
  state.outputUpgradeModuleDirectory = "Data/UpgradeModules/";
  state.selectedGroupIndex = 0;
  state.selectedManifestGroupIndex = 0;
  state.selectedCoreIndex = 0;
  state.selectedUpgradeModuleIndex = 0;
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
    renderEditors();
  }

  renderUpgradeModules();
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

function manifestGroupCoreCheckboxes(groupIndex) {
  const manifestGroup = state.manifestGroups[groupIndex];
  if (!manifestGroup) return "";

  return state.shipCores
    .map((core, idx) => {
      const labelBase = core.subtypeId?.trim() || core.uniqueName?.trim() || `Unnamed Core ${idx + 1}`;
      const isSelected = normalizeManifestGroupNames(core.manifestGroups || []).includes(manifestGroup.name);
      return `<label class="group-checklist-item ${isSelected ? "selected" : ""}">
      <input data-action="manifest-core-toggle" data-gm="${groupIndex}" data-c="${idx + 1}" type="checkbox" ${isSelected ? "checked" : ""} />
      <span>${escapeXml(labelBase)}</span>
    </label>`;
    })
    .join("");
}

function manifestGroupCheckboxesForCore(coreIndex, selected = []) {
  return state.manifestGroups
    .filter((group) => group.name.trim())
    .map((group) => {
      const isSelected = selected.includes(group.name);
      return `<label class="group-checklist-item ${isSelected ? "selected" : ""}">
      <input data-action="core-manifest-group-toggle" data-c="${coreIndex}" data-group-name="${escapeXml(group.name)}" type="checkbox" ${isSelected ? "checked" : ""} />
      <span>${escapeXml(group.name)}</span>
    </label>`;
    })
    .join("");
}

function renderManifestGroupSelector() {
  ensureValidSelectedIndexes();
  const selector = ids("selectedManifestGroup");
  if (!selector) return;

  selector.innerHTML = state.manifestGroups
    .map((group, idx) => {
      const label = group.name?.trim() ? `${idx + 1}. ${group.name}` : `${idx + 1}. (Unnamed Manifest Group)`;
      return `<option value="${idx}" ${idx === state.selectedManifestGroupIndex ? "selected" : ""}>${escapeXml(label)}</option>`;
    })
    .join("");
  selector.disabled = state.manifestGroups.length === 0;
}

function renderManifestGroups() {
  renderManifestGroupSelector();

  if (state.selectedManifestGroupIndex < 0) {
    ids("manifestGroups").innerHTML = `<p class="muted">No manifest groups yet. Click <strong>Add Manifest Group</strong>.</p>`;
    return;
  }

  const groupIndex = state.selectedManifestGroupIndex;
  const group = state.manifestGroups[groupIndex];
  const assignedCoreCount = state.shipCores.filter((core) => normalizeManifestGroupNames(core.manifestGroups || []).includes(group.name)).length;

  ids("manifestGroups").innerHTML = `
    <div class="card">
      <div class="row wrap">
        <label class="inline">Group Name <input data-action="manifest-name" data-gm="${groupIndex}" placeholder="Manifest Group Name" value="${escapeXml(group.name)}" /></label>
        <label class="inline">MaxCount <input data-action="manifest-max" data-gm="${groupIndex}" class="small" type="number" min="0" value="${Number(group.maxCount)}" /></label>
        <button data-action="duplicate-manifest-group" data-gm="${groupIndex}">Duplicate Group</button>
        <button data-action="remove-manifest-group" data-gm="${groupIndex}">Delete Group</button>
      </div>
      <p class="muted">Assigned ship cores: ${assignedCoreCount}</p>
      ${state.shipCores.length
        ? `<div class="group-checklist">${manifestGroupCoreCheckboxes(groupIndex)}</div>`
        : `<p class="muted">Add ship cores, then assign them to this manifest group here.</p>`}
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
  const options = [state.noCoreCore, ...state.shipCores];
  selector.innerHTML = options
    .map((core, idx) => {
      const defaultLabel = idx === 0 ? "No Core" : "Unnamed Core";
      const label = core?.subtypeId?.trim() || defaultLabel;
      return `<option value="${idx}" ${idx === state.selectedCoreIndex ? "selected" : ""}>${escapeXml(`${idx}. ${label}`)}</option>`;
    })
    .join("");
  selector.disabled = false;
}

function renderShipCores() {
  ensureValidSelectedIndexes();
  renderCoreSelector();

  const coreIndex = state.selectedCoreIndex;
  const core = getCoreBySelectorIndex(coreIndex);
  if (!core) return;
  const isNoCore = coreIndex === 0;
  if (isNoCore) {
    core.outputDirectory = "";
  }
  const expandedLimitPanels = normalizeExpandedLimitPanelsForCore(coreIndex);

  ids("shipCores").innerHTML = `
    <div class="card">
      <div class="row wrap">
        <label class="inline">Core Subtype <input data-action="core-subtype" data-c="${coreIndex}" class="small" placeholder="SubtypeId" value="${escapeXml(core.subtypeId)}" /></label>
        <label class="inline">Core Name <input data-action="core-unique" data-c="${coreIndex}" class="small" placeholder="UniqueName" value="${escapeXml(core.uniqueName)}" /></label>
        <label class="inline">Output Folder <input data-action="core-output-directory" data-c="${coreIndex}" class="small${isNoCore ? " no-core-output-disabled" : ""}" placeholder="${isNoCore ? "" : "Data/Cores/"}" value="${escapeXml(isNoCore ? "" : core.outputDirectory)}" ${isNoCore ? "disabled title=\"No Core output always stays in the root folder.\"" : ""} /></label>
        <label class="inline">Mobility
          <select data-action="core-mobility" data-c="${coreIndex}" class="small">
            ${["Static", "Mobile", "Both"].map((value) => `<option ${core.mobilityType === value ? "selected" : ""}>${value}</option>`).join("")}
          </select>
        </label>
        <button data-action="duplicate-core" data-c="${coreIndex}">Duplicate Core</button>
        ${isNoCore
    ? `<button data-action="reset-no-core" data-c="${coreIndex}">Reset No Core</button>`
    : `<button data-action="remove-core" data-c="${coreIndex}">Delete Core</button>`}
      </div>
      <div class="row wrap">
        <label class="inline">MaxBlocks <input data-action="core-maxblocks" data-c="${coreIndex}" class="small" type="number" value="${core.maxBlocks}" /></label>
        <label class="inline">MinBlocks <input data-action="core-minblocks" data-c="${coreIndex}" class="small" type="number" value="${core.minBlocks}" /></label>
        <label class="inline">MaxMass <input data-action="core-maxmass" data-c="${coreIndex}" class="small" type="number" value="${core.maxMass}" /></label>
        <label class="inline">MaxPCU <input data-action="core-maxpcu" data-c="${coreIndex}" class="small" type="number" value="${core.maxPcu}" /></label>
      </div>
      <div class="row wrap">
        <label class="inline">MaxBackupCores <input data-action="core-maxbackupcores" data-c="${coreIndex}" class="small" type="number" value="${core.maxBackupCores}" /></label>
        <label class="inline">MaxPerPlayer <input data-action="core-maxpp" data-c="${coreIndex}" class="small" type="number" value="${core.maxPerPlayer}" /></label>
        <label class="inline">MaxPerFaction <input data-action="core-maxpf" data-c="${coreIndex}" class="small" type="number" value="${core.maxPerFaction}" /></label>
        <label class="inline">FactionPlayersNeededPerCore <input data-action="core-faction-players-needed" data-c="${coreIndex}" class="small" type="number" value="${core.factionPlayersNeededPerCore}" /></label>
        <label class="inline">MinPlayers <input data-action="core-minplayers" data-c="${coreIndex}" class="small" type="number" value="${core.minPerFaction}" /></label>
        <label class="inline">MaxPlayers <input data-action="core-maxplayers" data-c="${coreIndex}" class="small" type="number" value="${core.maxPlayers}" /></label>
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
        <button data-action="add-core-upgrade-allowance" data-c="${coreIndex}">Add Allowed Upgrade Module</button>
      </div>

      ${isNoCore ? "" : `
      <h4>Manifest Groups</h4>
      ${state.manifestGroups.filter((group) => group.name.trim()).length
        ? `<div class="group-checklist">${manifestGroupCheckboxesForCore(coreIndex, core.manifestGroups || [])}</div>`
        : `<p class="muted">No manifest groups defined yet. Add them in the Manifest Groups section.</p>`}
      `}

      <h4>Allowed Upgrade Modules</h4>
      ${(core.allowedUpgradeModules || []).map((entry, allowanceIndex) => `
        <div class="row wrap">
          <label class="inline">SubtypeId <input data-action="core-upgrade-subtype" data-c="${coreIndex}" data-au="${allowanceIndex}" class="small" value="${escapeXml(entry.subtypeId)}" /></label>
          <label class="inline">MaxCount <input data-action="core-upgrade-max" data-c="${coreIndex}" data-au="${allowanceIndex}" class="small" type="number" value="${Number(entry.maxCount)}" /></label>
          <button data-action="remove-core-upgrade-allowance" data-c="${coreIndex}" data-au="${allowanceIndex}">Remove</button>
        </div>
      `).join("")}

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
  const limit = getCoreBySelectorIndex(coreIndex)?.blockLimits?.[limitIndex];
  if (!limit) return;

  const listElement = document.querySelector(`[data-limit-group-list][data-c="${coreIndex}"][data-l="${limitIndex}"]`);
  if (!listElement) return;

  listElement.innerHTML = blockGroupCheckboxes(coreIndex, limitIndex, limit.blockGroups, limit.groupSearch || "");
}

function renderUpgradeSelector() {
  const selector = ids("selectedUpgradeModule");
  if (!selector) return;

  if (state.selectedUpgradeModuleIndex >= state.upgradeModules.length) {
    state.selectedUpgradeModuleIndex = Math.max(0, state.upgradeModules.length - 1);
  }

  selector.innerHTML = state.upgradeModules
    .map((module, idx) => {
      const labelBase = module.uniqueName?.trim() || module.subtypeId?.trim() || "Unnamed Upgrade Module";
      return `<option value="${idx}" ${idx === state.selectedUpgradeModuleIndex ? "selected" : ""}>${escapeXml(`${idx + 1}. ${labelBase}`)}</option>`;
    })
    .join("");
  selector.disabled = state.upgradeModules.length === 0;
}

function renderUpgradeModules() {
  const host = ids("upgradeModules");
  if (!host) return;

  renderUpgradeSelector();
  const module = getSelectedUpgradeModule();
  if (!module) {
    host.innerHTML = `<p class="muted">No upgrade modules yet. Click <strong>Add Upgrade Module</strong>.</p>`;
    return;
  }

  const moduleIndex = state.selectedUpgradeModuleIndex;

  host.innerHTML = `
    <div class="card">
      <div class="row wrap">
        <label class="inline">Module Subtype <input data-action="upgrade-subtype" data-u="${moduleIndex}" class="small" placeholder="SubtypeId" value="${escapeXml(module.subtypeId)}" /></label>
        <label class="inline">Module Name <input data-action="upgrade-unique" data-u="${moduleIndex}" class="small" placeholder="UniqueName" value="${escapeXml(module.uniqueName)}" /></label>
        <button data-action="add-upgrade-modifier" data-u="${moduleIndex}">Add Modifier</button>
        <button data-action="add-upgrade-limit-modifier" data-u="${moduleIndex}">Add Block Limit Modifier</button>
        <button data-action="remove-upgrade-module" data-u="${moduleIndex}">Delete Upgrade Module</button>
      </div>

      <h4>Stat Modifiers</h4>
      ${(module.modifiers || []).map((modifier, modifierIndex) => `
        <div class="row wrap">
          <label class="inline">Stat <input data-action="upgrade-mod-stat" data-u="${moduleIndex}" data-m="${modifierIndex}" class="small" value="${escapeXml(modifier.stat)}" /></label>
          <label class="inline">Value <input data-action="upgrade-mod-value" data-u="${moduleIndex}" data-m="${modifierIndex}" class="small" type="number" step="0.01" value="${Number(modifier.value)}" /></label>
          <label class="inline">Type
            <select data-action="upgrade-mod-type" data-u="${moduleIndex}" data-m="${modifierIndex}" class="small">
              ${["Additive", "Multiplicative"].map((value) => `<option ${modifier.modifierType === value ? "selected" : ""}>${value}</option>`).join("")}
            </select>
          </label>
          <button data-action="remove-upgrade-modifier" data-u="${moduleIndex}" data-m="${modifierIndex}">Remove</button>
        </div>
      `).join("")}

      <h4>Block Limit Modifiers</h4>
      ${(module.blockLimitModifiers || []).map((modifier, modifierIndex) => `
        <div class="row wrap">
          <label class="inline">BlockLimitName <input data-action="upgrade-limit-name" data-u="${moduleIndex}" data-bm="${modifierIndex}" class="small" value="${escapeXml(modifier.blockLimitName)}" /></label>
          <label class="inline">Value <input data-action="upgrade-limit-value" data-u="${moduleIndex}" data-bm="${modifierIndex}" class="small" type="number" step="0.01" value="${Number(modifier.value)}" /></label>
          <label class="inline">Type
            <select data-action="upgrade-limit-type" data-u="${moduleIndex}" data-bm="${modifierIndex}" class="small">
              ${["Additive", "Multiplicative"].map((value) => `<option ${modifier.modifierType === value ? "selected" : ""}>${value}</option>`).join("")}
            </select>
          </label>
          <button data-action="remove-upgrade-limit-modifier" data-u="${moduleIndex}" data-bm="${modifierIndex}">Remove</button>
        </div>
      `).join("")}
    </div>
  `;
}

function renderEditors() {
  renderBlockGroups();
  renderManifestGroups();
  renderShipCores();
  renderUpgradeModules();
}

function parseGroupsXml(text) {
  const doc = parseXml(text);
  return qselAll(doc, "BlockGroup")
    .map((groupNode) => ({
      name: textOf(groupNode, "Name"),
      blockTypes: qselAll(groupNode, "BlockTypes").map((typeNode) => ({
        typeId: textOf(typeNode, "TypeId"),
        subtypeId: textOf(typeNode, "SubtypeId"),
        countWeight: numberOf(typeNode, "CountWeight", 1)
      }))
    }))
    .filter((group) => group.name);
}

function parseCoreXml(text, originalFileName = "") {
  const doc = parseXml(text);
  const root = xmlRoot(doc);
  const coreNode = (root && root.localName === "ShipCore") ? root : null;
  if (!coreNode) return null;

  return {
    ...createDefaultCore(),
    originalFileName,
    subtypeId: textOf(coreNode, "SubtypeId"),
    uniqueName: textOf(coreNode, "UniqueName"),
    mobilityType: textOf(coreNode, "MobilityType") || "Both",
    maxBlocks: numberOf(coreNode, "MaxBlocks", -1),
    minBlocks: numberOf(coreNode, "MinBlocks", -1),
    maxMass: numberOf(coreNode, "MaxMass", -1),
    maxPcu: numberOf(coreNode, "MaxPCU", -1),
    maxBackupCores: numberOf(coreNode, "MaxBackupCores", -1),
    maxPerFaction: numberOf(coreNode, "MaxPerFaction", -1),
    factionPlayersNeededPerCore: numberOf(coreNode, "FactionPlayersNeededPerCore", -1),
    minPerFaction: numberOf(coreNode, "MinPlayers", numberOf(coreNode, "MinPerFaction", -1)),
    maxPlayers: numberOf(coreNode, "MaxPlayers", -1),
    maxPerPlayer: numberOf(coreNode, "MaxPerPlayer", -1),
    forceBroadcast: boolOf(coreNode, "ForceBroadCast", false),
    forceBroadcastRange: numberOf(coreNode, "ForceBroadCastRange", 2000),
    speedBoostEnabled: boolOf(coreNode, "SpeedBoostEnabled", false),
    speedLimitType: textOf(coreNode, "SpeedLimitType") || "Normal",
    enableActiveDefenseModifiers: boolOf(coreNode, "EnableActiveDefenseModifiers", false),
    allowedUpgradeModules: qselAll(coreNode, "AllowedUpgradeModules").map((entryNode) => ({
      subtypeId: textOf(entryNode, "SubtypeId"),
      maxCount: numberOf(entryNode, "MaxCount", 0)
    })).filter((entry) => entry.subtypeId),
    modifiers: parseModifierNode(qsel(coreNode, "Modifiers"), DEFAULT_GRID_MODIFIERS),
    speedModifiers: parseModifierNode(qsel(coreNode, "SpeedModifiers"), DEFAULT_SPEED_MODIFIERS),
    passiveDefenseModifiers: parseModifierNode(qsel(coreNode, "PassiveDefenseModifiers"), DEFAULT_DEFENSE_MODIFIERS),
    activeDefenseModifiers: parseModifierNode(qsel(coreNode, "ActiveDefenseModifiers"), DEFAULT_DEFENSE_MODIFIERS),
    blockLimits: qselAll(coreNode, "BlockLimits").map((limitNode) => ({
      name: textOf(limitNode, "Name"),
      maxCount: numberOf(limitNode, "MaxCount", 0),
      crossConnectorPunishment: boolOf(limitNode, "CrossConnectorPunishment", false),
      punishByNoFlyZone: boolOf(limitNode, "PunishByNoFlyZone", boolOf(limitNode, "TurnedOffByNoFlyZone", false)),
      punishmentType: textOf(limitNode, "PunishmentType") || "ShutOff",
      allowedDirections: qselAll(limitNode, "AllowedDirections").map((node) => node.textContent.trim()).filter(Boolean),
      blockGroups: qselAll(limitNode, "BlockGroups").map((node) => node.textContent.trim()).filter(Boolean),
      groupSearch: ""
    }))
  };
}

function writeModifierXml(tag, values, defaults, indent = "  ") {
  return `${indent}<${tag}>\n${Object.keys(defaults)
    .map((name) => `${indent}  <${name}>${Number(values?.[name] ?? defaults[name])}</${name}>`)
    .join("\n")}\n${indent}</${tag}>`;
}

function writeAllowedUpgradeModulesXml(entries = [], indent = "  ") {
  return entries
    .filter((entry) => String(entry?.subtypeId ?? "").trim())
    .map(
      (entry) =>
        `${indent}<AllowedUpgradeModules>\n${indent}  <SubtypeId>${escapeXml(entry.subtypeId)}</SubtypeId>\n${indent}  <MaxCount>${Number(entry.maxCount ?? 0)}</MaxCount>\n${indent}</AllowedUpgradeModules>`
    )
    .join("\n");
}

function writeBlockLimitXml(limit, indent = "  ") {
  const lines = [
    `${indent}<BlockLimits>`,
    `${indent}  <Name>${escapeXml(limit.name)}</Name>`,
    ...(limit.blockGroups || []).map((groupName) => `${indent}  <BlockGroups>${escapeXml(groupName)}</BlockGroups>`),
    `${indent}  <MaxCount>${limit.maxCount}</MaxCount>`,
    `${indent}  <CrossConnectorPunishment>${limit.crossConnectorPunishment}</CrossConnectorPunishment>`,
    `${indent}  <PunishByNoFlyZone>${limit.punishByNoFlyZone}</PunishByNoFlyZone>`,
    `${indent}  <PunishmentType>${escapeXml(limit.punishmentType)}</PunishmentType>`,
    ...(limit.allowedDirections || []).filter(Boolean).map((direction) => `${indent}  <AllowedDirections>${escapeXml(direction)}</AllowedDirections>`),
    `${indent}</BlockLimits>`
  ];

  return lines.join("\n");
}

function parseUpgradeModuleXml(text) {
  // Strip BOM and re-strip the XML declaration independently of parseXml() so
  // that any stray whitespace or encoding issues don't leave DOMParser with a
  // malformed prolog that silently returns a parsererror document.
  const cleaned = text.replace(/^\uFEFF/, "").replace(/^\s*<\?xml[^?]*\?>\s*/i, "").trim();
  const doc = new DOMParser().parseFromString(cleaned, "application/xml");
  const root = doc && doc.documentElement;
  if (!root || root.nodeName === "parsererror" || root.localName === "parsererror") return null;

  // Support both a bare <UpgradeModule> root and any wrapper element (e.g. a
  // future <Definitions> container) by searching the whole document.
  let moduleNode = null;
  if (root.localName === "UpgradeModule") {
    moduleNode = root;
  } else {
    const found = doc.getElementsByTagName("UpgradeModule");
    if (found.length > 0) moduleNode = found[0];
  }
  if (!moduleNode) return null;

  // Use getElementsByTagName with an explicit localName guard so that browsers
  // whose namespace-aware XML DOM let "Modifiers" match "BlockLimitModifiers"
  // (a known Firefox quirk when xmlns: attributes are present) don't pollute
  // the modifiers list with block-limit entries.
  const modifiers = Array.from(moduleNode.getElementsByTagName("Modifiers"))
    .filter((n) => n.localName === "Modifiers")
    .map((n) => ({
      stat:         (n.getElementsByTagName("Stat")[0]?.textContent          || "").trim(),
      value:        Number((n.getElementsByTagName("Value")[0]?.textContent   || "0").trim()),
      modifierType: (n.getElementsByTagName("ModifierType")[0]?.textContent  || "Multiplicative").trim()
    })).filter((m) => m.stat);

  const blockLimitModifiers = Array.from(moduleNode.getElementsByTagName("BlockLimitModifiers"))
    .filter((n) => n.localName === "BlockLimitModifiers")
    .map((n) => ({
      blockLimitName: (n.getElementsByTagName("BlockLimitName")[0]?.textContent || "").trim(),
      value:          Number((n.getElementsByTagName("Value")[0]?.textContent   || "0").trim()),
      modifierType:   (n.getElementsByTagName("ModifierType")[0]?.textContent  || "Additive").trim()
    })).filter((m) => m.blockLimitName);

  // Resolve SubtypeId and UniqueName as direct children of <UpgradeModule>
  // using a parentNode check so deeper descendants are never accidentally matched.
  const subtypeIdNode  = Array.from(moduleNode.getElementsByTagName("SubtypeId")).find((n) => n.localName === "SubtypeId"  && n.parentNode === moduleNode);
  const uniqueNameNode = Array.from(moduleNode.getElementsByTagName("UniqueName")).find((n) => n.localName === "UniqueName" && n.parentNode === moduleNode);

  return cloneUpgradeModule({
    subtypeId:  (subtypeIdNode?.textContent  || "").trim(),
    uniqueName: (uniqueNameNode?.textContent || "").trim(),
    modifiers,
    blockLimitModifiers
  });
}

function writeUpgradeModuleXml(module, indent = "  ") {
  const header = '<?xml version="1.0" encoding="UTF-8"?>';
  const lines = [
    header,
    `<UpgradeModule>`,
    `${indent}<SubtypeId>${escapeXml(module.subtypeId)}</SubtypeId>`,
    `${indent}<UniqueName>${escapeXml(module.uniqueName)}</UniqueName>`,
    ...(module.modifiers || []).filter((modifier) => String(modifier.stat || "").trim()).flatMap((modifier) => [
      `${indent}<Modifiers>`,
      `${indent}  <Stat>${escapeXml(modifier.stat)}</Stat>`,
      `${indent}  <Value>${Number(modifier.value ?? 0)}</Value>`,
      `${indent}  <ModifierType>${escapeXml(modifier.modifierType || "Multiplicative")}</ModifierType>`,
      `${indent}</Modifiers>`
    ]),
    ...(module.blockLimitModifiers || []).filter((modifier) => String(modifier.blockLimitName || "").trim()).flatMap((modifier) => [
      `${indent}<BlockLimitModifiers>`,
      `${indent}  <BlockLimitName>${escapeXml(modifier.blockLimitName)}</BlockLimitName>`,
      `${indent}  <Value>${Number(modifier.value ?? 0)}</Value>`,
      `${indent}  <ModifierType>${escapeXml(modifier.modifierType || "Additive")}</ModifierType>`,
      `${indent}</BlockLimitModifiers>`
    ]),
    `</UpgradeModule>`
  ];

  return lines.join("\n");
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
    factionPlayersNeededPerCore: numberOf(gridClassNode, "FactionPlayersNeededPerCore", -1),
    minPerFaction: numberOf(gridClassNode, "MinPlayers", -1),
    maxPlayers: numberOf(gridClassNode, "MaxPlayers", -1),
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
  const root = xmlRoot(doc);
  if (!root || root.localName !== "ModConfig") return { error: `No <ModConfig> root found in ${sourceName}.` };

  const defaultGridClassNode = qsel(root, "DefaultGridClass");
  const gridClassNodes = qselAll(qsel(root, "GridClasses"), "GridClass");

  if (!defaultGridClassNode && gridClassNodes.length === 0) {
    return { error: `No <DefaultGridClass> or <GridClass> entries found in ${sourceName}.` };
  }

  const noCoreCore = defaultGridClassNode ? parseLegacyGridClass(defaultGridClassNode, "NoCore") : null;
  if (noCoreCore) {
    noCoreCore.subtypeId = "NO_CORE_DEFAULT";
    noCoreCore.uniqueName = noCoreCore.uniqueName || "No Core";
    noCoreCore.outputDirectory = "";
    noCoreCore.originalFileName = "ShipCoreConfig_No_Core.xml";
  }

  const shipCores = gridClassNodes.map((node, index) => parseLegacyGridClass(node, `GridClass_${index + 1}`));
  const blockGroups = applyLegacyGroupDedup(noCoreCore, shipCores, mergeMode);

  const status = [
    `Migrated legacy file ${sourceName}.`,
    noCoreCore ? "Mapped DefaultGridClass into ShipCoreConfig_No_Core.xml." : "No DefaultGridClass found.",
    `Mapped ${shipCores.length} GridClass entries to ShipCore cores.`,
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

function buildCoreOutputPath(core, filename) {
  return `${normalizeOutputDirectory(core?.outputDirectory, state.outputCoreDirectory || "Data/Cores/")}${filename}`;
}

function getUniqueCoreFilenames(cores) {
  const usedNames = new Set();

  return cores.map((core) => {
    const rawName = deriveCoreFilename(core) || "unnamed_core.xml";
    const extensionMatch = rawName.match(/(\.[^.]*)$/);
    const extension = extensionMatch ? extensionMatch[1] : "";
    const baseName = extension ? rawName.slice(0, -extension.length) : rawName;

    let candidate = rawName;
    let suffix = 1;
    while (usedNames.has(candidate.toLowerCase())) {
      candidate = `${baseName}-${suffix}${extension}`;
      suffix += 1;
    }

    usedNames.add(candidate.toLowerCase());
    return candidate;
  });
}

function getNamedManifestGroups() {
  return state.manifestGroups
    .map((group) => cloneManifestGroup(group))
    .filter((group) => group.name.trim());
}

function getValidManifestGroupNamesForCore(core) {
  const validNames = new Set(getNamedManifestGroups().map((group) => group.name.toLowerCase()));
  return normalizeManifestGroupNames(core?.manifestGroups || []).filter((groupName) => validNames.has(groupName.toLowerCase()));
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
  const namedManifestGroups = getNamedManifestGroups();

  const noCore = state.noCoreCore
    ? `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(state.noCoreCore.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(state.noCoreCore.uniqueName)}</UniqueName>\n  <ForceBroadCast>${state.noCoreCore.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${state.noCoreCore.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(state.noCoreCore.mobilityType)}</MobilityType>\n  <MaxBlocks>${state.noCoreCore.maxBlocks}</MaxBlocks>\n  <MinBlocks>${state.noCoreCore.minBlocks}</MinBlocks>\n  <MaxMass>${state.noCoreCore.maxMass}</MaxMass>\n  <MaxPCU>${state.noCoreCore.maxPcu}</MaxPCU>\n  <MaxBackupCores>${state.noCoreCore.maxBackupCores}</MaxBackupCores>\n  <MaxPerPlayer>${state.noCoreCore.maxPerPlayer}</MaxPerPlayer>\n  <MaxPerFaction>${state.noCoreCore.maxPerFaction}</MaxPerFaction>\n  <FactionPlayersNeededPerCore>${state.noCoreCore.factionPlayersNeededPerCore}</FactionPlayersNeededPerCore>\n  <MinPlayers>${state.noCoreCore.minPerFaction}</MinPlayers>\n  <MaxPlayers>${state.noCoreCore.maxPlayers}</MaxPlayers>\n  <SpeedBoostEnabled>${state.noCoreCore.speedBoostEnabled}</SpeedBoostEnabled>\n  <SpeedLimitType>${escapeXml(state.noCoreCore.speedLimitType)}</SpeedLimitType>\n  <EnableActiveDefenseModifiers>${state.noCoreCore.enableActiveDefenseModifiers}</EnableActiveDefenseModifiers>\n${writeAllowedUpgradeModulesXml(state.noCoreCore.allowedUpgradeModules)}${state.noCoreCore.allowedUpgradeModules?.length ? "\n" : ""}${writeModifierXml("Modifiers", state.noCoreCore.modifiers, DEFAULT_GRID_MODIFIERS)}\n${writeModifierXml("SpeedModifiers", state.noCoreCore.speedModifiers, DEFAULT_SPEED_MODIFIERS)}\n${writeModifierXml("PassiveDefenseModifiers", state.noCoreCore.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${writeModifierXml("ActiveDefenseModifiers", state.noCoreCore.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${state.noCoreCore.blockLimits
      .map((limit) => writeBlockLimitXml(limit))
      .join("\n")}\n</ShipCore>`
    : `${header}\n<ShipCore />`;

  const groups = `${header}\n<ArrayOfBlockGroup>\n${state.blockGroups
    .map((group) => `  <BlockGroup>\n    <Name>${escapeXml(group.name)}</Name>\n${group.blockTypes
      .map((bt) => `    <BlockTypes>\n      <TypeId>${escapeXml(bt.typeId)}</TypeId>\n      <SubtypeId>${escapeXml(bt.subtypeId || "")}</SubtypeId>\n      <CountWeight>${bt.countWeight}</CountWeight>\n    </BlockTypes>`)
      .join("\n")}\n  </BlockGroup>`)
    .join("\n")}\n</ArrayOfBlockGroup>`;

  const coreFilenames = getUniqueCoreFilenames(state.shipCores);
  const upgradeModuleFilenames = state.upgradeModules.map((module, moduleIndex) => {
    const rawName = sanitizeFilenamePart(module.subtypeId) || sanitizeFilenamePart(module.uniqueName) || `Upgrade_Module_${moduleIndex + 1}`;
    return `${rawName}.xml`;
  });

  const cores = state.shipCores.map((core, coreIndex) => ({
    core,
    file: coreFilenames[coreIndex],
    outputPath: buildCoreOutputPath(core, coreFilenames[coreIndex]),
    body: `${header}\n<ShipCore>\n  <SubtypeId>${escapeXml(core.subtypeId)}</SubtypeId>\n  <UniqueName>${escapeXml(core.uniqueName)}</UniqueName>\n  <ForceBroadCast>${core.forceBroadcast}</ForceBroadCast>\n  <ForceBroadCastRange>${core.forceBroadcastRange}</ForceBroadCastRange>\n  <MobilityType>${escapeXml(core.mobilityType)}</MobilityType>\n  <MaxBlocks>${core.maxBlocks}</MaxBlocks>\n  <MinBlocks>${core.minBlocks}</MinBlocks>\n  <MaxMass>${core.maxMass}</MaxMass>\n  <MaxPCU>${core.maxPcu}</MaxPCU>\n  <MaxBackupCores>${core.maxBackupCores}</MaxBackupCores>\n  <MaxPerPlayer>${core.maxPerPlayer}</MaxPerPlayer>\n  <MaxPerFaction>${core.maxPerFaction}</MaxPerFaction>\n  <FactionPlayersNeededPerCore>${core.factionPlayersNeededPerCore}</FactionPlayersNeededPerCore>\n  <MinPlayers>${core.minPerFaction}</MinPlayers>\n  <MaxPlayers>${core.maxPlayers}</MaxPlayers>\n  <SpeedBoostEnabled>${core.speedBoostEnabled}</SpeedBoostEnabled>\n  <SpeedLimitType>${escapeXml(core.speedLimitType)}</SpeedLimitType>\n  <EnableActiveDefenseModifiers>${core.enableActiveDefenseModifiers}</EnableActiveDefenseModifiers>\n${writeAllowedUpgradeModulesXml(core.allowedUpgradeModules)}${core.allowedUpgradeModules?.length ? "\n" : ""}${writeModifierXml("Modifiers", core.modifiers, DEFAULT_GRID_MODIFIERS)}\n${writeModifierXml("SpeedModifiers", core.speedModifiers, DEFAULT_SPEED_MODIFIERS)}\n${writeModifierXml("PassiveDefenseModifiers", core.passiveDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${writeModifierXml("ActiveDefenseModifiers", core.activeDefenseModifiers, DEFAULT_DEFENSE_MODIFIERS)}\n${core.blockLimits
      .map((limit) => writeBlockLimitXml(limit))
      .join("\n")}\n</ShipCore>`
  }));

  const upgradeModules = state.upgradeModules.map((module, moduleIndex) => ({
    module,
    file: upgradeModuleFilenames[moduleIndex],
    body: writeUpgradeModuleXml(module)
  }));

  const manifest = `${header}
<CoreManifest>
${namedManifestGroups.length
    ? `  <ManifestGroups>
${namedManifestGroups
      .map((group) => `    <Group>\n      <Name>${escapeXml(group.name)}</Name>\n      <MaxCount>${Number(group.maxCount)}</MaxCount>\n    </Group>`)
      .join("\n")}
  </ManifestGroups>`
    : ""}
${cores
    .filter((entry) => entry.core.subtypeId.trim())
    .map((entry) => {
      const groups = getValidManifestGroupNamesForCore(entry.core);
      return `  <ShipCore>\n    <Filename>${escapeXml(entry.outputPath)}</Filename>${groups.length ? `\n${groups.map((groupName) => `    <Group>${escapeXml(groupName)}</Group>`).join("\n")}` : ""}\n  </ShipCore>`;
    })
    .join("\n")}
${upgradeModules
    .filter((entry) => entry.module.subtypeId.trim())
    .map((entry) => `  <UpgradeModule>\n    <Filename>${escapeXml(state.outputUpgradeModuleDirectory)}${escapeXml(entry.file)}</Filename>\n  </UpgradeModule>`)
    .join("\n")}
</CoreManifest>`;

  ids("noCoreXml").textContent = noCore;
  ids("groupsXml").textContent = groups;
  ids("manifestXml").textContent = manifest;
  ids("coresXml").textContent = [
    ...cores.map((core) => `===== Cores/${core.file} =====
${core.body}`),
    ...upgradeModules.map((module) => `===== UpgradeModules/${module.file} =====
${module.body}`)
  ].join("\n\n");
  if (persistDraft) persistDraftToStorage();
  return { noCore, groups, manifest, cores, upgradeModules };
}

document.addEventListener("click", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const groupIndex = Number(target.dataset.g);
  const manifestGroupIndex = Number(target.dataset.gm);
  const blockTypeIndex = Number(target.dataset.i);
  const coreIndex = Number(target.dataset.c);
  const shipCoreIndex = coreIndex - 1;
  const limitIndex = Number(target.dataset.l);
  const upgradeIndex = Number(target.dataset.u);
  const allowanceIndex = Number(target.dataset.au);
  const upgradeModifierIndex = Number(target.dataset.m);
  const blockLimitModifierIndex = Number(target.dataset.bm);
  const selectedCore = getCoreBySelectorIndex(coreIndex);
  const selectedUpgrade = state.upgradeModules[upgradeIndex] || null;
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
  if (action === "remove-manifest-group") {
    const removedGroupName = state.manifestGroups[manifestGroupIndex]?.name || "";
    removeManifestGroupReferences(removedGroupName);
    state.manifestGroups.splice(manifestGroupIndex, 1);
    if (state.selectedManifestGroupIndex >= state.manifestGroups.length) state.selectedManifestGroupIndex = state.manifestGroups.length - 1;
    didMutate = true;
  }
  if (action === "duplicate-manifest-group") {
    const sourceGroup = state.manifestGroups[manifestGroupIndex];
    if (sourceGroup) {
      const duplicateName = createIncrementedDuplicateName(
        sourceGroup.name,
        state.manifestGroups.map((group) => group.name),
        "ManifestGroup"
      );
      const duplicatedGroup = cloneManifestGroup({
        ...sourceGroup,
        name: duplicateName
      });

      state.manifestGroups.splice(manifestGroupIndex + 1, 0, duplicatedGroup);
      state.shipCores.forEach((core) => {
        if ((core.manifestGroups || []).includes(sourceGroup.name)) {
          core.manifestGroups = dedupeStrings([...(core.manifestGroups || []), duplicateName]);
        }
      });
      state.selectedManifestGroupIndex = manifestGroupIndex + 1;
      didMutate = true;
    }
  }
  if (action === "reset-no-core") {
    state.noCoreCore = createDefaultNoCore();
    state.selectedCoreIndex = 0;
    didMutate = true;
  }
  if (action === "remove-core") {
    if (shipCoreIndex < 0) return;
    state.shipCores.splice(shipCoreIndex, 1);
    shiftExpandedLimitPanelsAfterCoreRemoval(coreIndex);
    if (state.selectedCoreIndex >= totalCoreOptions()) state.selectedCoreIndex = totalCoreOptions() - 1;
    didMutate = true;
  }
  if (action === "duplicate-core") {
    if (!selectedCore) return;

    const duplicatedCore = cloneShipCore(selectedCore);
    if (coreIndex === 0) {
      duplicatedCore.originalFileName = "";
      state.shipCores.unshift(duplicatedCore);
      state.selectedCoreIndex = 1;

      const shifted = {};
      Object.entries(state.expandedLimitPanelsByCore).forEach(([key, value]) => {
        const index = Number(key);
        if (!Number.isInteger(index)) return;
        shifted[index > 0 ? index + 1 : index] = value;
      });
      state.expandedLimitPanelsByCore = shifted;
      state.expandedLimitPanelsByCore[1] = [];
      didMutate = true;
    } else {
      duplicatedCore.originalFileName = "";
      state.shipCores.splice(shipCoreIndex + 1, 0, duplicatedCore);

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
    selectedCore?.blockLimits.push(createDefaultLimit());
    normalizeExpandedLimitPanelsForCore(coreIndex);
    didMutate = true;
  }
  if (action === "duplicate-limit") {
    const sourceLimit = selectedCore?.blockLimits?.[limitIndex];
    if (sourceLimit) {
      selectedCore.blockLimits.splice(limitIndex + 1, 0, cloneLimit(sourceLimit));
      normalizeExpandedLimitPanelsForCore(coreIndex);
      didMutate = true;
    }
  }
  if (action === "remove-limit") {
    selectedCore?.blockLimits.splice(limitIndex, 1);
    normalizeExpandedLimitPanelsForCore(coreIndex);
    didMutate = true;
  }

  if (action === "add-core-upgrade-allowance" && selectedCore) {
    selectedCore.allowedUpgradeModules.push({ subtypeId: "", maxCount: 0 });
    didMutate = true;
  }
  if (action === "remove-core-upgrade-allowance" && selectedCore) {
    const allowanceIndex = Number(target.dataset.au);
    selectedCore.allowedUpgradeModules.splice(allowanceIndex, 1);
    didMutate = true;
  }
  if (action === "add-upgrade-modifier" && selectedUpgrade) {
    selectedUpgrade.modifiers.push({ ...DEFAULT_UPGRADE_STAT_MODIFIER });
    didMutate = true;
  }
  if (action === "add-upgrade-limit-modifier" && selectedUpgrade) {
    selectedUpgrade.blockLimitModifiers.push({ ...DEFAULT_BLOCK_LIMIT_MODIFIER });
    didMutate = true;
  }
  if (action === "remove-upgrade-module") {
    if (upgradeIndex < 0) return;
    state.upgradeModules.splice(upgradeIndex, 1);
    if (state.selectedUpgradeModuleIndex >= state.upgradeModules.length) {
      state.selectedUpgradeModuleIndex = Math.max(0, state.upgradeModules.length - 1);
    }
    didMutate = true;
  }
  if (action === "remove-upgrade-modifier" && selectedUpgrade) {
    selectedUpgrade.modifiers.splice(upgradeModifierIndex, 1);
    didMutate = true;
  }
  if (action === "remove-upgrade-limit-modifier" && selectedUpgrade) {
    selectedUpgrade.blockLimitModifiers.splice(blockLimitModifierIndex, 1);
    didMutate = true;
  }

  if (!didMutate) return;

  renderEditors();
  generateXml();
});


document.addEventListener("input", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;

  const groupIndex = Number(target.dataset.g);
  const manifestGroupIndex = Number(target.dataset.gm);
  const blockTypeIndex = Number(target.dataset.i);
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);
  const upgradeIndex = Number(target.dataset.u);
  const allowanceIndex = Number(target.dataset.au);
  const upgradeModifierIndex = Number(target.dataset.m);
  const blockLimitModifierIndex = Number(target.dataset.bm);
  const selectedCore = getCoreBySelectorIndex(coreIndex);
  const selectedUpgrade = state.upgradeModules[upgradeIndex] || null;

  if (action === "bt-type") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].typeId = target.value;
  if (action === "bt-subtype") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].subtypeId = target.value;
  if (action === "bt-weight") state.blockGroups[groupIndex].blockTypes[blockTypeIndex].countWeight = Number(target.value || 0);
  if (action === "manifest-max") state.manifestGroups[manifestGroupIndex].maxCount = Number(target.value || 0);

  if (!selectedCore && (action.startsWith("core-") || action.startsWith("limit-"))) return;
  if (!selectedUpgrade && action.startsWith("upgrade-")) return;

  if (action === "core-unique") {
    selectedCore.uniqueName = target.value;
    clearGeneratedFilenameForRenamedCore(coreIndex, selectedCore);
  }
  if (action === "core-output-directory") {
    selectedCore.outputDirectory = normalizeOutputDirectory(target.value, state.outputCoreDirectory || "Data/Cores/");
  }
  if (action === "core-maxblocks") selectedCore.maxBlocks = Number(target.value || -1);
  if (action === "core-minblocks") selectedCore.minBlocks = Number(target.value || -1);
  if (action === "core-maxmass") selectedCore.maxMass = Number(target.value || -1);
  if (action === "core-maxpcu") selectedCore.maxPcu = Number(target.value || -1);
  if (action === "core-maxbackupcores") selectedCore.maxBackupCores = Number(target.value || -1);
  if (action === "core-maxpf") selectedCore.maxPerFaction = Number(target.value || -1);
  if (action === "core-faction-players-needed") selectedCore.factionPlayersNeededPerCore = Number(target.value || -1);
  if (action === "core-minpf") selectedCore.minPerFaction = Number(target.value || -1);
  if (action === "core-minplayers") selectedCore.minPerFaction = Number(target.value || -1);
  if (action === "core-maxplayers") selectedCore.maxPlayers = Number(target.value || -1);
  if (action === "core-maxpp") selectedCore.maxPerPlayer = Number(target.value || -1);
  if (action === "core-fbr") selectedCore.forceBroadcastRange = Number(target.value || 0);
  if (action === "core-upgrade-subtype") selectedCore.allowedUpgradeModules[allowanceIndex].subtypeId = target.value;
  if (action === "core-upgrade-max") selectedCore.allowedUpgradeModules[allowanceIndex].maxCount = Number(target.value || 0);

  if (action === "core-modifier-grid") selectedCore.modifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-speed") selectedCore.speedModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-passive-defense") selectedCore.passiveDefenseModifiers[target.dataset.m] = Number(target.value || 0);
  if (action === "core-modifier-active-defense") selectedCore.activeDefenseModifiers[target.dataset.m] = Number(target.value || 0);

  if (action === "limit-name") selectedCore.blockLimits[limitIndex].name = target.value;
  if (action === "limit-max") selectedCore.blockLimits[limitIndex].maxCount = Number(target.value || 0);
  if (action === "limit-group-search") {
    selectedCore.blockLimits[limitIndex].groupSearch = target.value;
    renderLimitGroupChecklist(coreIndex, limitIndex);
  }

  if (action === "upgrade-subtype") selectedUpgrade.subtypeId = target.value;
  if (action === "upgrade-unique") selectedUpgrade.uniqueName = target.value;
  if (action === "upgrade-mod-stat") selectedUpgrade.modifiers[upgradeModifierIndex].stat = target.value;
  if (action === "upgrade-mod-value") selectedUpgrade.modifiers[upgradeModifierIndex].value = Number(target.value || 0);
  if (action === "upgrade-limit-name") selectedUpgrade.blockLimitModifiers[blockLimitModifierIndex].blockLimitName = target.value;
  if (action === "upgrade-limit-value") selectedUpgrade.blockLimitModifiers[blockLimitModifierIndex].value = Number(target.value || 0);

  generateXml();
});

function commitDeferredTextInput(target) {
  if (!(target instanceof HTMLInputElement)) return;

  const action = target.dataset.action;
  const groupIndex = Number(target.dataset.g);
  const manifestGroupIndex = Number(target.dataset.gm);
  const coreIndex = Number(target.dataset.c);
  const selectedCore = getCoreBySelectorIndex(coreIndex);

  if (action === "group-name") {
    const previousName = state.blockGroups[groupIndex].name;
    const requestedName = target.value;
    const siblingNames = state.blockGroups
      .filter((_, idx) => idx !== groupIndex)
      .map((group) => group.name);
    const nextName = siblingNames.some((name) => String(name ?? "").trim().toLowerCase() === requestedName.trim().toLowerCase())
      ? createIncrementedDuplicateName(requestedName, siblingNames, "BlockGroup")
      : requestedName;
    if (previousName === nextName) return;

    state.blockGroups[groupIndex].name = nextName;
    renameBlockGroupReferences(previousName, nextName);
    renderEditors();
    generateXml();
  }

  if (action === "manifest-name") {
    const previousName = state.manifestGroups[manifestGroupIndex].name;
    const requestedName = target.value;
    const siblingNames = state.manifestGroups
      .filter((_, idx) => idx !== manifestGroupIndex)
      .map((group) => group.name);
    const nextName = siblingNames.some((name) => String(name ?? "").trim().toLowerCase() === requestedName.trim().toLowerCase())
      ? createIncrementedDuplicateName(requestedName, siblingNames, "ManifestGroup")
      : requestedName;
    if (previousName === nextName) return;

    state.manifestGroups[manifestGroupIndex].name = nextName;
    renameManifestGroupReferences(previousName, nextName);
    renderEditors();
    generateXml();
  }

  if (action === "core-subtype") {
    if (!selectedCore) return;
    const previousSubtype = selectedCore.subtypeId;
    const nextSubtype = target.value;
    if (previousSubtype === nextSubtype) return;

    selectedCore.subtypeId = nextSubtype;
    clearGeneratedFilenameForRenamedCore(coreIndex, selectedCore);
    renderEditors();
    generateXml();
  }
}

document.addEventListener("keydown", (event) => {
  if (event.key !== "Enter") return;

  const target = event.target;
  if (!(target instanceof HTMLInputElement)) return;

  const action = target.dataset.action;
  if (action !== "group-name" && action !== "manifest-name" && action !== "core-subtype") return;

  target.blur();
});

document.addEventListener("change", (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;
  const action = target.dataset.action;
  if (!action) return;
  const manifestGroupIndex = Number(target.dataset.gm);
  const coreIndex = Number(target.dataset.c);
  const limitIndex = Number(target.dataset.l);
  const upgradeIndex = Number(target.dataset.u);
  const upgradeModifierIndex = Number(target.dataset.m);
  const blockLimitModifierIndex = Number(target.dataset.bm);
  const selectedCore = getCoreBySelectorIndex(coreIndex);
  const selectedUpgrade = state.upgradeModules[upgradeIndex] || null;

  const inputElement = target instanceof HTMLInputElement ? target : null;
  const selectElement = target instanceof HTMLSelectElement ? target : null;

  if (action === "group-name" || action === "manifest-name" || action === "core-subtype") {
    commitDeferredTextInput(target);
    return;
  }

  if (!selectedCore && (action.startsWith("core-") || action.startsWith("limit-"))) return;
  if (!selectedUpgrade && action.startsWith("upgrade-")) return;

  if (action === "core-fb" && inputElement) selectedCore.forceBroadcast = inputElement.checked;
  if (action === "core-mobility" && selectElement) selectedCore.mobilityType = selectElement.value;
  if (action === "core-speedboost" && inputElement) selectedCore.speedBoostEnabled = inputElement.checked;
  if (action === "core-enable-active-defense" && inputElement) selectedCore.enableActiveDefenseModifiers = inputElement.checked;
  if (action === "core-speed-limit-type" && selectElement) selectedCore.speedLimitType = selectElement.value;
  if (action === "limit-punish" && inputElement) {
    selectedCore.blockLimits[limitIndex].punishByNoFlyZone = inputElement.checked;
    renderShipCores();
  }
  if (action === "limit-cross-connector" && inputElement) {
    selectedCore.blockLimits[limitIndex].crossConnectorPunishment = inputElement.checked;
    renderShipCores();
  }
  if (action === "limit-type" && selectElement) {
    selectedCore.blockLimits[limitIndex].punishmentType = selectElement.value;
    renderShipCores();
  }
  if (action === "limit-direction-toggle" && inputElement) {
    const limit = selectedCore.blockLimits[limitIndex];
    const direction = inputElement.dataset.direction || "";
    if (!direction) return;

    const selectedSet = new Set(limit.allowedDirections || []);
    if (inputElement.checked) selectedSet.add(direction);
    else selectedSet.delete(direction);
    limit.allowedDirections = VALID_DIRECTIONS.filter((value) => selectedSet.has(value));
    renderShipCores();
  }
  if (action === "limit-group-toggle" && inputElement) {
    const limit = selectedCore.blockLimits[limitIndex];
    const groupName = inputElement.dataset.groupName || "";
    if (!groupName) return;

    const selectedSet = new Set(limit.blockGroups || []);
    if (inputElement.checked) selectedSet.add(groupName);
    else selectedSet.delete(groupName);
    limit.blockGroups = Array.from(selectedSet);
    renderShipCores();
  }
  if (action === "manifest-core-toggle" && inputElement) {
    const manifestGroup = state.manifestGroups[manifestGroupIndex];
    const selectedShipCore = state.shipCores[coreIndex - 1];
    if (!manifestGroup || !selectedShipCore) return;

    const selectedSet = new Set(selectedShipCore.manifestGroups || []);
    if (inputElement.checked) selectedSet.add(manifestGroup.name);
    else selectedSet.delete(manifestGroup.name);
    selectedShipCore.manifestGroups = normalizeManifestGroupNames(Array.from(selectedSet));
    renderEditors();
  }
  if (action === "core-manifest-group-toggle" && inputElement) {
    const groupName = inputElement.dataset.groupName || "";
    if (!groupName) return;

    const selectedSet = new Set(selectedCore.manifestGroups || []);
    if (inputElement.checked) selectedSet.add(groupName);
    else selectedSet.delete(groupName);
    selectedCore.manifestGroups = normalizeManifestGroupNames(Array.from(selectedSet));
    renderEditors();
  }

  if (action === "upgrade-mod-type" && selectElement) selectedUpgrade.modifiers[upgradeModifierIndex].modifierType = selectElement.value;
  if (action === "upgrade-limit-type" && selectElement) selectedUpgrade.blockLimitModifiers[blockLimitModifierIndex].modifierType = selectElement.value;

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

ids("selectedManifestGroup").addEventListener("change", (event) => {
  state.selectedManifestGroupIndex = Number(event.target.value);
  renderManifestGroups();
  generateXml();
});

ids("selectedCore").addEventListener("change", (event) => {
  state.selectedCoreIndex = Number(event.target.value);
  renderShipCores();
  generateXml();
});

ids("addGroup").addEventListener("click", () => {
  addBlockGroup({ name: "", blockTypes: [] });
  generateXml();
});
ids("addManifestGroup").addEventListener("click", () => {
  addManifestGroup();
  generateXml();
});
ids("addCore").addEventListener("click", () => {
  addShipCore();
  generateXml();
});
ids("addUpgradeModule").addEventListener("click", () => {
  addUpgradeModule();
  generateXml();
});
ids("selectedUpgradeModule").addEventListener("change", (event) => {
  state.selectedUpgradeModuleIndex = Number(event.target.value);
  renderUpgradeModules();
  generateXml();
});
ids("generateXml").addEventListener("click", () => generateXml());
ids("resetEditor").addEventListener("click", () => {
  resetEditor(true);
  setImportStatus(["Editor reset to starter seed data."]);
});

ids("resetDraftStorage").addEventListener("click", () => {
  clearDraftFromStorage();
  setImportStatus(["Cleared autosaved draft from browser storage."]);
});

ids("downloadNoCore").addEventListener("click", () => {
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_No_Core.xml", xml.noCore);
  clearDraftFromStorage();
});
ids("downloadGroups").addEventListener("click", () => {
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_Groups.xml", xml.groups);
  clearDraftFromStorage();
});
ids("downloadManifest").addEventListener("click", () => {
  const xml = generateXml({ persistDraft: false });
  download("ShipCoreConfig_Manifest.xml", xml.manifest);
  clearDraftFromStorage();
});
ids("downloadAllFiles").addEventListener("click", async () => {
  const xml = generateXml({ persistDraft: false });

  // Build a set of zip paths that are being replaced by generated versions
  const generatedPaths = new Set([
    "ShipCoreConfig_No_Core.xml",
    "ShipCoreConfig_Groups.xml",
    "ShipCoreConfig_Manifest.xml",
    ...xml.cores.map((core) => core.outputPath),
    ...xml.upgradeModules.map((module) => `${state.outputUpgradeModuleDirectory}${module.file}`)
  ].map((p) => p.toLowerCase()));

  // Passthrough files that are NOT being replaced (e.g. SBCs, CubeBlock XMLs)
  const passthroughEntries = await Promise.all(
    state.uploadedPassthroughFiles
      .filter((f) => !generatedPaths.has(f.zipPath.toLowerCase()))
      .map(async (f) => ({ name: f.zipPath, content: await f.getText() }))
  );

  const zip = createZip([
    { name: "ShipCoreConfig_No_Core.xml", content: xml.noCore },
    { name: "ShipCoreConfig_Groups.xml", content: xml.groups },
    { name: "ShipCoreConfig_Manifest.xml", content: xml.manifest },
    ...xml.cores.map((core) => ({ name: core.outputPath, content: core.body })),
    ...xml.upgradeModules.map((module) => ({ name: `${state.outputUpgradeModuleDirectory}${module.file}`, content: module.body })),
    ...passthroughEntries
  ]);
  downloadBlob("ShipCore_All_Files.zip", zip);
  clearDraftFromStorage();
});
ids("downloadCores").addEventListener("click", () => {
  const xml = generateXml({ persistDraft: false });
  const zip = createZip([
    ...xml.cores.map((core) => ({ name: core.outputPath, content: core.body })),
    ...xml.upgradeModules.map((module) => ({ name: `${state.outputUpgradeModuleDirectory}${module.file}`, content: module.body }))
  ]);
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
  state.manifestGroups = [];
  state.selectedManifestGroupIndex = -1;
  state.selectedCoreIndex = 0;
  state.upgradeModules = [];
  state.selectedUpgradeModuleIndex = 0;
  state.expandedLimitPanelsByCore = {};

  renderEditors();
  generateXml();
  setImportStatus(migrated.status);
});

function parseSbcDefinitions(text) {
  const doc = parseXml(text);
  const results = [];
  for (const def of Array.from(doc.getElementsByTagName("Definition"))) {
    const rawType  = (def.getAttribute("xsi:type") || "").toLowerCase();
    const typeId   = (def.getElementsByTagName("TypeId")[0]?.textContent   || "").trim().toLowerCase();
    const subtype  = (def.getElementsByTagName("SubtypeId")[0]?.textContent || "").trim();
    const dispName = (def.getElementsByTagName("DisplayName")[0]?.textContent || subtype).trim();
    if (!subtype) continue;
    let blockType = null;
    if (rawType.includes("upgrademodule") || typeId === "upgrademodule") {
      blockType = "upgradeModule";
    } else if (
      rawType.includes("functionalblock") || typeId === "functionalblock" ||
      rawType.includes("beacon")          || typeId === "beacon"          ||
      rawType.includes("cockpit")         || typeId === "cockpit"         ||
      rawType.includes("shipcoreblock")   || typeId === "shipcoreblock"
    ) {
      blockType = "core";
    }
    if (!blockType) continue;

    // Extract upgrade stats from <Upgrades><MyUpgradeModuleInfo> elements
    const modifiers = Array.from(def.getElementsByTagName("MyUpgradeModuleInfo")).map((info) => ({
      stat:         (info.getElementsByTagName("UpgradeType")[0]?.textContent  || "").trim(),
      value:        Number((info.getElementsByTagName("Modifier")[0]?.textContent || "0").trim()),
      modifierType: (info.getElementsByTagName("ModifierType")[0]?.textContent  || "Multiplicative").trim()
    })).filter((m) => m.stat);

    results.push({ subtypeId: subtype, displayName: dispName, blockType, modifiers });
  }
  return results;
}

async function readZipFiles(zipFile) {
  const buffer = await zipFile.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  const view = new DataView(buffer);
  const results = [];

  // Locate End of Central Directory record (signature 0x06054b50)
  let eocdOffset = -1;
  for (let i = bytes.length - 22; i >= Math.max(0, bytes.length - 65558); i--) {
    if (view.getUint32(i, true) === 0x06054b50) { eocdOffset = i; break; }
  }
  if (eocdOffset === -1) throw new Error("Not a valid ZIP file.");

  const cdEntryCount = view.getUint16(eocdOffset + 8, true);
  let cdPos = view.getUint32(eocdOffset + 16, true);

  for (let i = 0; i < cdEntryCount; i++) {
    if (view.getUint32(cdPos, true) !== 0x02014b50) break;
    const compression   = view.getUint16(cdPos + 10, true);
    const compressedSz  = view.getUint32(cdPos + 20, true);
    const filenameLen   = view.getUint16(cdPos + 28, true);
    const extraLen      = view.getUint16(cdPos + 30, true);
    const commentLen    = view.getUint16(cdPos + 32, true);
    const localOffset   = view.getUint32(cdPos + 42, true);
    const filename      = new TextDecoder().decode(bytes.subarray(cdPos + 46, cdPos + 46 + filenameLen));
    cdPos += 46 + filenameLen + extraLen + commentLen;

    const lowerFilename = filename.toLowerCase();
    const isXml = lowerFilename.endsWith(".xml");
    const isSbc = lowerFilename.endsWith(".sbc");
    if ((!isXml && !isSbc) || lowerFilename.endsWith("/")) continue;

    const localFilenameLen = view.getUint16(localOffset + 26, true);
    const localExtraLen    = view.getUint16(localOffset + 28, true);
    const dataStart        = localOffset + 30 + localFilenameLen + localExtraLen;
    const compressed       = bytes.subarray(dataStart, dataStart + compressedSz);

    let text;
    if (compression === 0) {
      text = new TextDecoder().decode(compressed);
    } else if (compression === 8) {
      const ds = new DecompressionStream("deflate-raw");
      const writer = ds.writable.getWriter();
      writer.write(compressed);
      writer.close();
      const chunks = [];
      const reader = ds.readable.getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
      }
      const out = new Uint8Array(chunks.reduce((s, c) => s + c.length, 0));
      let off = 0;
      for (const chunk of chunks) { out.set(chunk, off); off += chunk.length; }
      text = new TextDecoder().decode(out);
    } else {
      continue;
    }

    const name = filename.replace(/\\/g, "/").split("/").pop();
    const capturedText = text;
    const capturedPath = filename.replace(/\\/g, "/");
    results.push({ name, zipPath: capturedPath, text: () => Promise.resolve(capturedText) });
  }

  return results;
}

async function processUploadedXmlFiles(groupsFile, manifestFile, noCoreFile, coreFiles, initialStatus = [], sbcFiles = [], passthroughFiles = [], fileZipPathMap = new Map()) {
  const status = [...initialStatus];
  const manifestCoreDirectoriesByFilename = new Map();
  const manifestCoreGroupsByFilename = new Map();

  resetEditor(false);
  // Store passthrough files (SBCs and unrecognised files) for re-inclusion in Download All
  state.uploadedPassthroughFiles = passthroughFiles.slice();
  state.manifestCoreFullPathByFilename = new Map();

  if (groupsFile) {
    const groups = parseGroupsXml(await groupsFile.text());
    state.blockGroups = groups;
    state.selectedGroupIndex = 0;
    status.push(`Loaded ${groups.length} block groups from ${groupsFile.name}.`);
  }

  if (manifestFile) {
    const manifest = parseManifestXml(await manifestFile.text());
    if (manifest.sourceFormat === "invalid") {
      status.push(`Skipped ${manifestFile.name}: no <CoreManifest> root found.`);
    } else {
      const listed = manifest.shipCores.map((entry) => entry.filename);
      const listedModules = manifest.upgradeModules.map((entry) => entry.filename);
      state.manifestGroups = manifest.groups;
      state.selectedManifestGroupIndex = manifest.groups.length ? 0 : -1;

      manifest.shipCores.forEach((entry) => {
        const manifestPath = entry.filename;
        const normalizedPath = manifestPath.replaceAll("\\", "/").trim();
        const fileName = normalizedPath.split("/").pop();
        if (!fileName) return;
        // Store both directory (for outputDirectory) and full path (for originalFileName)
        manifestCoreDirectoriesByFilename.set(
          fileName.toLowerCase(),
          getDirectoryFromManifestPath(normalizedPath, "Data/Cores/")
        );
        state.manifestCoreFullPathByFilename.set(fileName.toLowerCase(), normalizedPath);
        manifestCoreGroupsByFilename.set(fileName.toLowerCase(), dedupeStrings(entry.groups));
      });

      state.outputCoreDirectory = getManifestDirectory(listed, "Data/Cores/", "core", status);
      state.outputUpgradeModuleDirectory = getManifestDirectory(listedModules, "Data/UpgradeModules/", "upgrade module", status);
      status.push(`Read manifest ${manifestFile.name} with ${listed.length} listed core files, ${listedModules.length} listed upgrade modules, and ${manifest.groups.length} manifest groups.`);
      if (manifest.sourceFormat === "legacy") {
        status.push(`Legacy manifest format detected in ${manifestFile.name}; ported into current manifest structure in editor.`);
      }
      status.push(`Using '${state.outputCoreDirectory}' for generated core files and '${state.outputUpgradeModuleDirectory}' for generated upgrade module files.`);
    }
  }

  if (noCoreFile) {
    const parsedNoCore = parseCoreXml(await noCoreFile.text(), noCoreFile.name);
    if (parsedNoCore) {
      parsedNoCore.originalFileName = "ShipCoreConfig_No_Core.xml";
      parsedNoCore.outputDirectory = "";
      state.noCoreCore = parsedNoCore;
      status.push(`Loaded no-core '${parsedNoCore.subtypeId || noCoreFile.name}'.`);
    } else {
      status.push(`Skipped ${noCoreFile.name}: no <ShipCore> root found.`);
    }
  }

  for (const file of coreFiles) {
    const fileText = await file.text();
    const parsed = parseCoreXml(fileText, file.name);
    if (parsed) {
      const isNoCoreFile = file.name.trim().toLowerCase() === "shipcoreconfig_no_core.xml";
      if (isNoCoreFile) {
        if (noCoreFile) {
          status.push(`Skipped ${file.name}: no-core XML is already loaded from the dedicated No Core upload field.`);
          continue;
        }
        parsed.originalFileName = "ShipCoreConfig_No_Core.xml";
        parsed.outputDirectory = "";
        state.noCoreCore = parsed;
        status.push(`Loaded no-core '${parsed.subtypeId || file.name}'.`);
      } else {
        const fileZipPath = fileZipPathMap.get(file) || file.zipPath || file.name;
        // Use manifest-derived full path if available so the output ZIP mirrors the manifest exactly
        const manifestDir      = manifestCoreDirectoriesByFilename.get(file.name.trim().toLowerCase());
        const manifestFullPath = state.manifestCoreFullPathByFilename.get(file.name.trim().toLowerCase());
        let inferredDir = state.outputCoreDirectory;
        if (!manifestDir) {
          // Use pre-computed zip path (folder-root-relative) to infer output directory
          const norm = "/" + fileZipPath;
          const dataIdx = norm.toLowerCase().indexOf("/data/");
          if (dataIdx !== -1) {
            const fromData = norm.slice(dataIdx + 1);
            const lastSlash = fromData.lastIndexOf("/");
            if (lastSlash > 0) inferredDir = normalizeOutputDirectory(fromData.slice(0, lastSlash), state.outputCoreDirectory);
          }
        }
        parsed.outputDirectory = manifestDir || inferredDir;
        // Keep the original filename from manifest so the ZIP output path is exact
        if (manifestFullPath) {
          const manifestFileName = manifestFullPath.split("/").pop();
          parsed.originalFileName = manifestFileName || file.name;
        }
        parsed.manifestGroups = normalizeManifestGroupNames(manifestCoreGroupsByFilename.get(file.name.trim().toLowerCase()) || [], state.manifestGroups);
        state.shipCores.push(parsed);
        status.push(`Loaded core '${parsed.subtypeId || file.name}'.`);
      }
      continue;
    }

    const parsedUpgrade = parseUpgradeModuleXml(fileText);
    if (parsedUpgrade) {
      state.upgradeModules.push(parsedUpgrade);
      status.push(`Loaded upgrade module '${parsedUpgrade.subtypeId || file.name}' with ${parsedUpgrade.modifiers.length} modifier(s).`);
      continue;
    }

    // Try parsing as a game block-definition SBC (<Definitions><CubeBlocks><Definition
    // xsi:type="MyObjectBuilder_UpgradeModuleDefinition">) and import any upgrade-module
    // definitions found inside it. Block-definition SBCs can contain multiple definitions
    // so we extract all upgradeModule entries and add them. The file is still passed
    // through unchanged so it stays in the output ZIP.
    const sbcDefs = parseSbcDefinitions(fileText);
    const sbcUpgradeDefs = sbcDefs.filter((d) => d.blockType === "upgradeModule");
    if (sbcUpgradeDefs.length > 0) {
      for (const def of sbcUpgradeDefs) {
        const upgradeModule = cloneUpgradeModule({
          subtypeId:  def.subtypeId,
          uniqueName: def.displayName,
          modifiers:  def.modifiers,
          blockLimitModifiers: []
        });
        state.upgradeModules.push(upgradeModule);
      }
      status.push(`Loaded ${sbcUpgradeDefs.length} upgrade module(s) from ${file.name}.`);
      // Still pass through so the original block-definition SBC is included in the output ZIP
      const zipPath = fileZipPathMap.get(file) || file.zipPath || file.name;
      const capturedText = fileText;
      state.uploadedPassthroughFiles.push({ zipPath, getText: () => Promise.resolve(capturedText) });
      continue;
    }

    // Not a recognised config file — pass through unchanged into Download All.
    const zipPath = fileZipPathMap.get(file) || file.zipPath || file.name;
    const capturedText = fileText;
    state.uploadedPassthroughFiles.push({ zipPath, getText: () => Promise.resolve(capturedText) });
    status.push(`Passed through ${file.name}.`);
  }

  state.selectedCoreIndex = 0;
  state.selectedUpgradeModuleIndex = 0;
  state.expandedLimitPanelsByCore = {};

  if (state.noCoreCore) status.push(`Loaded no-core from ${state.noCoreCore.originalFileName || "legacy import"}.`);
  if (state.blockGroups.length === 0) status.push("No block groups loaded (you can still create them manually).");
  if (state.manifestGroups.length === 0) status.push("No manifest groups loaded (you can still create them manually).");
  if (state.shipCores.length === 0) status.push("No cores loaded (you can still add cores manually).");
  if (state.upgradeModules.length === 0) status.push("No upgrade modules loaded (you can still add upgrade modules manually).");

  renderEditors();
  generateXml();
  setImportStatus(status);
}

// Store folder files captured directly from the change event's FileList
let _cachedFolderFiles = [];
ids("folderUpload").addEventListener("change", (evt) => {
  // Capture files immediately from the event — more reliable than reading
  // .files later on click, especially when served from file:// protocol.
  const input = evt.target;
  _cachedFolderFiles = Array.from(input.files || []);
  if (_cachedFolderFiles.length > 0) {
    const label = input.closest("label");
    if (label) {
      const existing = label.querySelector(".folder-file-count");
      if (existing) existing.remove();
      const badge = document.createElement("span");
      badge.className = "folder-file-count";
      badge.textContent = ` (${_cachedFolderFiles.length} files)`;
      label.appendChild(badge);
    }
  }
});

ids("loadUploadedXml").addEventListener("click", async () => {
  let groupsFile   = ids("groupsXmlFile").files?.[0] || null;
  let manifestFile = ids("manifestXmlFile").files?.[0] || null;
  let noCoreFile   = ids("noCoreXmlFile").files?.[0] || null;
  const coreFiles  = Array.from(ids("coreXmlFiles").files || []);
  const sbcFiles   = [];
  const preStatus  = [];
  const passthroughFiles = [];

  const folderFiles = (() => {
    // Merge the cached files (from the change event) with any live files currently
    // in the input — deduplicating by webkitRelativePath so neither source is missed.
    const liveFiles = Array.from(ids("folderUpload").files || []);
    if (liveFiles.length >= _cachedFolderFiles.length) return liveFiles;
    const seen = new Set(liveFiles.map((f) => f.webkitRelativePath || f.name));
    const extra = _cachedFolderFiles.filter((f) => !seen.has(f.webkitRelativePath || f.name));
    return [...liveFiles, ...extra];
  })();

  // Determine the root prefix to strip from webkitRelativePath.
  // webkitRelativePath is always "<uploadedFolderName>/rest/of/path"
  // We want zip paths relative to the contents of the uploaded folder.
  let folderRootPrefix = "";
  if (folderFiles.length > 0 && folderFiles[0].webkitRelativePath) {
    const firstSlash = folderFiles[0].webkitRelativePath.replace(/\\/g, "/").indexOf("/");
    if (firstSlash > 0) {
      folderRootPrefix = folderFiles[0].webkitRelativePath.replace(/\\/g, "/").slice(0, firstSlash + 1);
    }
  }

  function folderZipPath(f) {
    const rel = (f.webkitRelativePath || f.name).replace(/\\/g, "/");
    return folderRootPrefix && rel.startsWith(folderRootPrefix)
      ? rel.slice(folderRootPrefix.length)
      : rel;
  }

  // Map from File object → computed zip path (can't add properties to File objects)
  const fileZipPathMap = new Map();

  for (const f of folderFiles) {
    const lname = f.name.trim().toLowerCase();
    const zp = folderZipPath(f);
    fileZipPathMap.set(f, zp);
    if (lname === "shipcoreconfig_groups.xml"   && !groupsFile)   { groupsFile   = f; continue; }
    if (lname === "shipcoreconfig_manifest.xml" && !manifestFile) { manifestFile = f; continue; }
    if (lname === "shipcoreconfig_no_core.xml"  && !noCoreFile)   { noCoreFile   = f; continue; }
    if (lname.endsWith(".sbc")) {
      // Config SBCs (<UpgradeModule> root) go through the same parse pipeline as XMLs.
      // Block definition SBCs (<Definitions> root) will fail both parsers and be passed through.
      coreFiles.push(f);
      continue;
    }
    if (lname.endsWith(".xml")) {
      coreFiles.push(f);
      continue;
    }
  }

  const zipInputFile = ids("zipUpload").files?.[0] || null;
  if (zipInputFile) {
    try {
      const allZipFiles = await readZipFiles(zipInputFile);
      let zipXmlCount = 0, zipSbcCount = 0;
      for (const f of allZipFiles) {
        const lname = f.name.trim().toLowerCase();
        if (lname === "shipcoreconfig_groups.xml"   && !groupsFile)   { groupsFile   = f; zipXmlCount++; continue; }
        if (lname === "shipcoreconfig_manifest.xml" && !manifestFile) { manifestFile = f; zipXmlCount++; continue; }
        if (lname === "shipcoreconfig_no_core.xml"  && !noCoreFile)   { noCoreFile   = f; zipXmlCount++; continue; }
        if (lname.endsWith(".sbc")) {
          // Config SBCs (<UpgradeModule> root) go through the parse pipeline.
          // Block definition SBCs will fail both parsers and be passed through.
          coreFiles.push(f); zipSbcCount++; continue;
        }
        if (lname.endsWith(".xml")) { coreFiles.push(f); zipXmlCount++; continue; }
      }
      preStatus.push(`Extracted ${zipXmlCount} XML and ${zipSbcCount} SBC file(s) from ${zipInputFile.name}.`);
    } catch (e) {
      preStatus.push(`Error reading ZIP ${zipInputFile.name}: ${e.message}`);
    }
  }

  const hasAnything = groupsFile || manifestFile || noCoreFile || coreFiles.length > 0 || sbcFiles.length > 0;
  if (!hasAnything) {
    setImportStatus([...preStatus, "No files found. Select a folder or ZIP containing your mod Data/ files."]);
    return;
  }

  await processUploadedXmlFiles(groupsFile, manifestFile, noCoreFile, coreFiles, preStatus, sbcFiles, passthroughFiles, fileZipPathMap);
});

(async () => {
  const response = await fetch("./assets/ModConfig.XmlModels.cs", { cache: "no-cache" });
  const text = await response.text();
  state.schema = parseModConfigCs(text);
  ids("schemaPreview").textContent = JSON.stringify(state.schema, null, 2);
  ids("parserStatus").textContent = `Loaded bundled ModConfig.XmlModels.cs and parsed ${Object.keys(state.schema).length} XML classes.`;

  if (restoreDraftFromStorage()) {
    renderEditors();
    generateXml();
    setImportStatus(["Restored autosaved draft from your browser storage."]);
    return;
  }

  resetEditor(true);
  setImportStatus(["Tip: Upload existing XML files to renovate and continue editing."]);
})();
