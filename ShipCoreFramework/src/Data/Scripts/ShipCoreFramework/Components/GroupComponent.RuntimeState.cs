using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal GroupRuntimeState BuildRuntimeState(int revision)
        {
            if (!Session.IsServer || _closing) return null;

            var gridIds = new List<long>();
            foreach (var grid in GridDictionary.Keys)
                if (grid != null && !grid.MarkedForClose && !grid.Closed)
                    gridIds.Add(grid.EntityId);
            gridIds.Sort();
            if (gridIds.Count == 0) return null;

            var runtimeLimits = new List<RuntimeLimitState>();
            var configuredLimits = ShipCore == null ? null : ShipCore.BlockLimits;
            if (configuredLimits != null)
            {
                for (var i = 0; i < configuredLimits.Length; i++)
                {
                    var limit = configuredLimits[i];
                    if (limit == null) continue;
                    var total = 0d;
                    LimitBucket bucket;
                    if (Limits.TryGetValue(limit, out bucket) && bucket != null)
                        lock (bucket.BucketLock) total = bucket.TotalWeight;
                    runtimeLimits.Add(new RuntimeLimitState
                    {
                        Name = limit.Name ?? string.Empty,
                        CurrentCount = total,
                        MaxCount = GetEffectiveMaxCount(limit)
                    });
                }
            }
            runtimeLimits.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

            var directionReference = GetDirectionLockReferenceBlock();
            var modifiers = Modifiers;
            var speedModifiers = SpeedModifiers;
            var ownerId = OwnerId;
            var faction = OwningFaction;
            var subtypeId = MainCoreComponent == null ? string.Empty : MainCoreComponent.SubtypeId;
            var countSubtypeId = ShipCore == null ? subtypeId : ShipCore.SubtypeId;
            var manifestCounts = new List<RuntimeManifestCount>();
            foreach (var manifest in PerManifestGroupManager.GetManifestGroups(ShipCore))
            {
                if (manifest == null) continue;
                manifestCounts.Add(new RuntimeManifestCount
                {
                    Name = manifest.Name ?? string.Empty,
                    Count = PerManifestGroupManager.GetCurrentCount(manifest.Name)
                });
            }
            float baseSpeed;
            float effectiveSpeed;
            long speedSourceGridId;
            bool frictionEnabled;
            bool boostActive;
            bool effectiveBoostActive;
            float boostDurationTimer;
            float boostCooldownTimer;
            float frictionMaximumDecelerationOverride;
            float minimumFrictionSpeedAbsoluteOverride;
            float maximumFrictionSpeedAbsoluteOverride;
            float minimumFrictionSpeedModifierOverride;
            float maximumFrictionSpeedModifierOverride;
            lock (SpeedStateLock)
            {
                baseSpeed = BaseSpeedLimitMetersPerSecond;
                effectiveSpeed = EffectiveSpeedLimitMetersPerSecond;
                speedSourceGridId = SpeedSourceGroupGridId;
                frictionEnabled = FrictionEnforcementEnabled;
                boostActive = BoostEnabled;
                effectiveBoostActive = EffectiveBoostEnabled;
                boostDurationTimer = _boostDurationTimer;
                boostCooldownTimer = _boostCooldownTimer;
                frictionMaximumDecelerationOverride = FrictionMaximumDecelerationOverride;
                minimumFrictionSpeedAbsoluteOverride = MinimumFrictionSpeedAbsoluteOverride;
                maximumFrictionSpeedAbsoluteOverride = MaximumFrictionSpeedAbsoluteOverride;
                minimumFrictionSpeedModifierOverride = MinimumFrictionSpeedModifierOverride;
                maximumFrictionSpeedModifierOverride = MaximumFrictionSpeedModifierOverride;
            }
            bool activeDefense;
            float activeDefenseDurationTimer;
            float activeDefenseCooldownTimer;
            bool powerOverclockActive;
            float powerOverclockDurationTimer;
            float powerOverclockCooldownTimer;
            lock (_abilityStateLock)
            {
                activeDefense = _activeDefenseEnabled;
                activeDefenseDurationTimer = _activeDefenseDurationTimer;
                activeDefenseCooldownTimer = _activeDefenseCooldownTimer;
                powerOverclockActive = _powerOverclockActive;
                powerOverclockDurationTimer = _powerOverclockDurationTimer;
                powerOverclockCooldownTimer = _powerOverclockCooldownTimer;
            }
            return new GroupRuntimeState
            {
                GroupId = gridIds[0],
                Revision = revision,
                GridIds = gridIds.ToArray(),
                CoreSubtypeId = subtypeId,
                MainCoreBlockId = MainCoreComponent == null ? 0L : MainCoreComponent.CoreBlock.EntityId,
                CoreCount = CoreDictionary.Count,
                DirectionReferenceBlockId = directionReference == null ? 0L : directionReference.EntityId,
                OwnerId = ownerId,
                Deactivated = Deactivated,
                Ignored = GetCachedIsIgnoredGroup(),
                PunishModifiers = PunishModifiers,
                PunishSpeed = PunishSpeed,
                PunishLimitedBlocks = PunishLimitedBlocks,
                BlockCount = GroupBlocksCount,
                Pcu = GroupPCU,
                Mass = GroupMass,
                DryMass = GroupDryMass,
                MaxBlocks = GetEffectiveMaxBlocks(),
                MaxPcu = GetEffectiveMaxPCU(),
                MaxMass = GetEffectiveMaxMass(),
                Limits = runtimeLimits.ToArray(),
                Modifiers = ModAPI.ConvertToGridModifiersData(modifiers),
                SpeedModifiers = ModAPI.ConvertToSpeedModifiersData(speedModifiers),
                BaseSpeed = baseSpeed,
                EffectiveSpeed = effectiveSpeed,
                SpeedSourceGridId = speedSourceGridId,
                FrictionEnabled = frictionEnabled,
                FrictionMaximumDecelerationOverride = frictionMaximumDecelerationOverride,
                MinimumFrictionSpeedAbsoluteOverride = minimumFrictionSpeedAbsoluteOverride,
                MaximumFrictionSpeedAbsoluteOverride = maximumFrictionSpeedAbsoluteOverride,
                MinimumFrictionSpeedModifierOverride = minimumFrictionSpeedModifierOverride,
                MaximumFrictionSpeedModifierOverride = maximumFrictionSpeedModifierOverride,
                BoostActive = boostActive,
                BoostDurationTimer = boostDurationTimer,
                BoostCooldownTimer = boostCooldownTimer,
                ActiveDefense = activeDefense,
                ActiveDefenseDurationTimer = activeDefenseDurationTimer,
                ActiveDefenseCooldownTimer = activeDefenseCooldownTimer,
                PowerOverclockActive = powerOverclockActive,
                PowerOverclockDurationTimer = powerOverclockDurationTimer,
                PowerOverclockCooldownTimer = powerOverclockCooldownTimer,
                RepresentativeGridId = GetCachedRepresentativeGridId(),
                EffectiveBoostActive = effectiveBoostActive,
                PlayerCoreCount = PerPlayerManager.GetCurrentCount(ownerId, countSubtypeId),
                FactionCoreCount = faction == null ? 0 : PerFactionManager.GetCurrentCount(faction.FactionId, countSubtypeId),
                ManifestCounts = manifestCounts.ToArray(),
                SpeedPunishmentReasons = GetSpeedPunishmentGateDescriptions().ToArray(),
                ModifierPunishmentReasons = GetModifierPunishmentGateDescriptions().ToArray(),
                LimitedBlockPunishmentReasons = GetLimitedBlockPunishmentGateDescriptions().ToArray(),
                LimitRevision = Interlocked.CompareExchange(ref _publishedLimitRevision, 0, 0),
                LimitEnforcementRevision = Interlocked.CompareExchange(ref _limitEnforcementRevision, 0, 0),
                LastBlocksPunished = Interlocked.CompareExchange(ref _lastBlocksPunished, 0, 0)
            };
        }

        internal void ApplyRuntimeState(GroupRuntimeState state)
        {
            if (Session.IsServer || state == null || _closing) return;

            var nextModifiers = FromRuntimeData(state.Modifiers);
            var modifiersChanged = !_runtimeStateReceived || !SameModifiers(_cachedActiveGridModifiers, nextModifiers);
            var broadcastChanges = _runtimeStateReceived;
            var previousCoreBlockId = _runtimeMainCoreBlockId;
            var previousCoreSubtypeId = _runtimeCoreSubtypeId;
            var previousBoostActive = BoostEnabled;
            var previousDefenseActive = _activeDefenseEnabled;
            var previousGridIds = _cachedMechanicalGridIds ?? Array.Empty<long>();
            var previousLimitRevision = _runtimeLimitRevision;
            var previousLimitEnforcementRevision = _runtimeLimitEnforcementRevision;
            _runtimeStateReceived = true;
            _runtimeCoreSubtypeId = state.CoreSubtypeId ?? string.Empty;
            _runtimeOwnerId = state.OwnerId;
            _runtimeMainCoreBlockId = state.MainCoreBlockId;
            _runtimeCoreCount = state.CoreCount;
            _runtimePlayerCoreCount = state.PlayerCoreCount;
            _runtimeFactionCoreCount = state.FactionCoreCount;
            _runtimeManifestCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.ManifestCounts != null)
                for (var i = 0; i < state.ManifestCounts.Length; i++)
                {
                    var count = state.ManifestCounts[i];
                    if (count != null && !string.IsNullOrEmpty(count.Name))
                        _runtimeManifestCounts[count.Name] = count.Count;
                }
            _runtimeSpeedPunishmentReasons = state.SpeedPunishmentReasons ?? Array.Empty<string>();
            _runtimeModifierPunishmentReasons = state.ModifierPunishmentReasons ?? Array.Empty<string>();
            _runtimeLimitedBlockPunishmentReasons = state.LimitedBlockPunishmentReasons ?? Array.Empty<string>();
            _runtimeLimitRevision = state.LimitRevision;
            _runtimeLimitEnforcementRevision = state.LimitEnforcementRevision;
            _lastOwnerId = state.OwnerId;
            Deactivated = state.Deactivated;
            PunishModifiers = state.PunishModifiers;
            PunishSpeed = state.PunishSpeed;
            PunishLimitedBlocks = state.PunishLimitedBlocks;
            lock (_abilityStateLock)
            {
                _activeDefenseEnabled = state.ActiveDefense;
                _activeDefenseDurationTimer = state.ActiveDefenseDurationTimer;
                _activeDefenseCooldownTimer = state.ActiveDefenseCooldownTimer;
                _powerOverclockActive = state.PowerOverclockActive;
                _powerOverclockDurationTimer = state.PowerOverclockDurationTimer;
                _powerOverclockCooldownTimer = state.PowerOverclockCooldownTimer;
            }

            Interlocked.Exchange(ref _groupBlocksCount, Math.Max(0, state.BlockCount));
            Interlocked.Exchange(ref _cachedGroupPCU, Math.Max(0, state.Pcu));
            _cachedConfiguredMass = state.Mass;
            _cachedDryMass = state.DryMass;
            _cachedEffectiveMaxBlocks = state.MaxBlocks;
            _cachedEffectiveMaxPCU = state.MaxPcu;
            _cachedEffectiveMaxMass = state.MaxMass;
            Interlocked.Exchange(ref _cachedRepresentativeGridId,
                state.RepresentativeGridId == 0 ? state.GroupId : state.RepresentativeGridId);
            _cachedMechanicalGridIds = state.GridIds ?? Array.Empty<long>();
            _cachedIsIgnoredGroup = state.Ignored;
            _cachedIsIgnoredByAiOrFactionTag = state.Ignored;
            MyEntity directionEntity;
            _cachedNoCoreDirectionLockReferenceBlock = state.DirectionReferenceBlockId != 0 &&
                                                        MyEntities.TryGetEntityById(state.DirectionReferenceBlockId, out directionEntity)
                ? directionEntity as IMyCubeBlock
                : null;
            _gridStateCacheDirty = false;
            _massCacheDirty = false;
            _ignoredStateCacheDirty = false;
            _directionReferenceCacheDirty = false;

            lock (SpeedStateLock)
            {
                BoostEnabled = state.BoostActive;
                _boostDurationTimer = state.BoostDurationTimer;
                _boostCooldownTimer = state.BoostCooldownTimer;
                BaseSpeedLimitMetersPerSecond = state.BaseSpeed;
                EffectiveSpeedLimitMetersPerSecond = state.EffectiveSpeed;
                SpeedSourceGroupGridId = state.SpeedSourceGridId;
                EffectiveBoostEnabled = state.EffectiveBoostActive;
                FrictionEnforcementEnabled = state.FrictionEnabled;
                FrictionMaximumDecelerationOverride = state.FrictionMaximumDecelerationOverride;
                MinimumFrictionSpeedAbsoluteOverride = state.MinimumFrictionSpeedAbsoluteOverride;
                MaximumFrictionSpeedAbsoluteOverride = state.MaximumFrictionSpeedAbsoluteOverride;
                MinimumFrictionSpeedModifierOverride = state.MinimumFrictionSpeedModifierOverride;
                MaximumFrictionSpeedModifierOverride = state.MaximumFrictionSpeedModifierOverride;
            }

            _cachedActiveGridModifiers = nextModifiers;
            _cachedActiveSpeedModifiers = FromRuntimeData(state.SpeedModifiers);
            _modifierStateCacheDirty = false;
            PublishRuntimeLimits(state.Limits);
            ApplyRuntimeCore(state.MainCoreBlockId);
            if (modifiersChanged) ApplyModifiers(_cachedActiveGridModifiers);
            if (broadcastChanges)
                BroadcastRuntimeStateChanges(state, previousCoreBlockId, previousCoreSubtypeId,
                    previousBoostActive, previousDefenseActive, previousGridIds,
                    previousLimitRevision, previousLimitEnforcementRevision);
        }

        private void BroadcastRuntimeStateChanges(GroupRuntimeState state, long previousCoreBlockId,
            string previousCoreSubtypeId, bool previousBoostActive, bool previousDefenseActive,
            long[] previousGridIds, int previousLimitRevision, int previousLimitEnforcementRevision)
        {
            var eventGridId = state.RepresentativeGridId == 0 ? state.GroupId : state.RepresentativeGridId;
            if (previousCoreBlockId != state.MainCoreBlockId)
            {
                if (previousCoreBlockId != 0 && state.MainCoreBlockId == 0)
                {
                    var oldCore = Session.Config.GetShipCoreByTypeId(previousCoreSubtypeId);
                    ModAPI.BroadcastCoreDeactivated(eventGridId, previousCoreSubtypeId,
                        oldCore == null ? previousCoreSubtypeId : oldCore.UniqueName);
                }
                else if (previousCoreBlockId == 0 && state.MainCoreBlockId != 0)
                {
                    var newCore = Session.Config.GetShipCoreByTypeId(state.CoreSubtypeId ?? string.Empty);
                    ModAPI.BroadcastCoreActivated(eventGridId, state.CoreSubtypeId,
                        newCore == null ? state.CoreSubtypeId : newCore.UniqueName);
                }
            }

            if (previousBoostActive != state.BoostActive)
            {
                if (state.BoostActive)
                {
                    var coreGrid = MainCoreComponent == null || MainCoreComponent.GridComponent == null
                        ? null
                        : MainCoreComponent.GridComponent.Grid;
                    ModAPI.BroadcastBoostActivated(coreGrid == null ? eventGridId : coreGrid.EntityId);
                }
                else ModAPI.BroadcastBoostDeactivated(eventGridId);
            }
            if (previousDefenseActive != state.ActiveDefense)
            {
                if (state.ActiveDefense) ModAPI.BroadcastActiveDefenseActivated(eventGridId);
                else ModAPI.BroadcastActiveDefenseDeactivated(eventGridId);
            }

            var oldGridIds = new HashSet<long>(previousGridIds);
            var newGridIds = new HashSet<long>(state.GridIds ?? Array.Empty<long>());
            foreach (var gridId in newGridIds)
                if (!oldGridIds.Contains(gridId)) ModAPI.BroadcastGridAddedToGroup(gridId);
            foreach (var gridId in oldGridIds)
                if (!newGridIds.Contains(gridId)) ModAPI.BroadcastGridRemovedFromGroup(gridId, eventGridId);

            if (state.LimitRevision > previousLimitRevision)
                ModAPI.BroadcastLimitsRecalculated(eventGridId);
            if (state.LimitEnforcementRevision > previousLimitEnforcementRevision)
                ModAPI.BroadcastLimitsEnforced(eventGridId, state.LastBlocksPunished);
        }

        internal void ClearRuntimeState()
        {
            if (Session.IsServer || !_runtimeStateReceived) return;
            var previousCoreBlockId = _runtimeMainCoreBlockId;
            var previousCoreSubtypeId = _runtimeCoreSubtypeId;
            var previousBoostActive = BoostEnabled;
            var previousDefenseActive = _activeDefenseEnabled;
            var previousGridIds = _cachedMechanicalGridIds ?? Array.Empty<long>();
            var previousRepresentativeGridId = GetCachedRepresentativeGridId();
            _runtimeStateReceived = false;
            _runtimeCoreSubtypeId = string.Empty;
            _runtimeOwnerId = 0;
            _runtimeCoreCount = 0;
            _runtimePlayerCoreCount = 0;
            _runtimeFactionCoreCount = 0;
            _runtimeManifestCounts.Clear();
            _runtimeSpeedPunishmentReasons = Array.Empty<string>();
            _runtimeModifierPunishmentReasons = Array.Empty<string>();
            _runtimeLimitedBlockPunishmentReasons = Array.Empty<string>();
            _runtimeMainCoreBlockId = 0;
            _runtimeLimitRevision = 0;
            _runtimeLimitEnforcementRevision = 0;
            Deactivated = false;
            PunishModifiers = false;
            PunishSpeed = false;
            PunishLimitedBlocks = false;
            MainCoreComponent = null;
            foreach (var pair in CoreDictionary) pair.Value.ApplyAuthoritativeMainState(false);
            PublishLimitsSnapshot(null);
            _cachedEffectiveMaxCounts.Clear();
            _cachedEffectiveMaxBlocks = -1;
            _cachedEffectiveMaxPCU = -1;
            _cachedEffectiveMaxMass = -1f;
            Interlocked.Exchange(ref _groupBlocksCount, 0);
            Interlocked.Exchange(ref _cachedGroupPCU, 0);
            Interlocked.Exchange(ref _cachedRepresentativeGridId, 0L);
            _cachedConfiguredMass = 0f;
            _cachedDryMass = 0f;
            _cachedMechanicalGridIds = Array.Empty<long>();
            _cachedIsIgnoredGroup = false;
            _cachedIsIgnoredByAiOrFactionTag = false;
            _cachedActiveGridModifiers = new GridModifiers();
            _cachedActiveSpeedModifiers = new SpeedModifiers();
            BaseSpeedLimitMetersPerSecond = 100f;
            EffectiveSpeedLimitMetersPerSecond = 100f;
            SpeedSourceGroupGridId = 0;
            EffectiveBoostEnabled = false;
            BoostEnabled = false;
            _boostDurationTimer = 0f;
            _boostCooldownTimer = 0f;
            _activeDefenseEnabled = false;
            _activeDefenseDurationTimer = 0f;
            _activeDefenseCooldownTimer = 0f;
            _powerOverclockActive = false;
            _powerOverclockDurationTimer = 0f;
            _powerOverclockCooldownTimer = 0f;
            FrictionEnforcementEnabled = true;
            FrictionMaximumDecelerationOverride = -1f;
            MinimumFrictionSpeedAbsoluteOverride = -1f;
            MaximumFrictionSpeedAbsoluteOverride = -1f;
            MinimumFrictionSpeedModifierOverride = -1f;
            MaximumFrictionSpeedModifierOverride = -1f;
            ApplyModifiers(_cachedActiveGridModifiers);
            BroadcastRuntimeStateCleared(previousCoreBlockId, previousCoreSubtypeId,
                previousBoostActive, previousDefenseActive, previousGridIds,
                previousRepresentativeGridId);
        }

        private static void BroadcastRuntimeStateCleared(long previousCoreBlockId,
            string previousCoreSubtypeId, bool previousBoostActive, bool previousDefenseActive,
            long[] previousGridIds, long previousRepresentativeGridId)
        {
            var eventGridId = previousRepresentativeGridId;
            if (eventGridId == 0 && previousGridIds.Length > 0) eventGridId = previousGridIds[0];
            if (previousCoreBlockId != 0)
            {
                var oldCore = Session.Config.GetShipCoreByTypeId(previousCoreSubtypeId);
                ModAPI.BroadcastCoreDeactivated(eventGridId, previousCoreSubtypeId,
                    oldCore == null ? previousCoreSubtypeId : oldCore.UniqueName);
            }
            if (previousBoostActive) ModAPI.BroadcastBoostDeactivated(eventGridId);
            if (previousDefenseActive) ModAPI.BroadcastActiveDefenseDeactivated(eventGridId);
            for (var i = 0; i < previousGridIds.Length; i++)
                ModAPI.BroadcastGridRemovedFromGroup(previousGridIds[i], eventGridId);
        }

        private static bool SameModifiers(GridModifiers left, GridModifiers right)
        {
            return left != null && right != null &&
                   left.AssemblerSpeed == right.AssemblerSpeed &&
                   left.DrillHarvestMultiplier == right.DrillHarvestMultiplier &&
                   left.GyroEfficiency == right.GyroEfficiency &&
                   left.GyroForce == right.GyroForce &&
                   left.PowerProducersOutput == right.PowerProducersOutput &&
                   left.RefineEfficiency == right.RefineEfficiency &&
                   left.RefineSpeed == right.RefineSpeed &&
                   left.ThrusterEfficiency == right.ThrusterEfficiency &&
                   left.ThrusterForce == right.ThrusterForce;
        }

        internal int GetCurrentPlayerCoreCount()
        {
            return Session.IsServer
                ? PerPlayerManager.GetCurrentCount(OwnerId, ShipCore.SubtypeId)
                : _runtimePlayerCoreCount;
        }

        internal int GetCurrentFactionCoreCount()
        {
            if (!Session.IsServer) return _runtimeFactionCoreCount;
            var faction = OwningFaction;
            return faction == null ? 0 : PerFactionManager.GetCurrentCount(faction.FactionId, ShipCore.SubtypeId);
        }

        internal int GetCurrentManifestCoreCount(string name)
        {
            if (Session.IsServer) return PerManifestGroupManager.GetCurrentCount(name);
            int count;
            return name != null && _runtimeManifestCounts.TryGetValue(name, out count) ? count : 0;
        }

        private void ApplyRuntimeCore(long mainCoreBlockId)
        {
            MainCoreComponent = null;
            foreach (var pair in CoreDictionary)
            {
                var isMain = pair.Key != null && pair.Key.EntityId == mainCoreBlockId;
                pair.Value.ApplyAuthoritativeMainState(isMain);
                if (isMain) MainCoreComponent = pair.Value;
            }
        }

        private void PublishRuntimeLimits(RuntimeLimitState[] runtimeLimits)
        {
            var limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
            var effectiveCounts = new Dictionary<BlockLimit, float>();
            var configured = ShipCore == null ? null : ShipCore.BlockLimits;
            if (configured != null && runtimeLimits != null)
            {
                for (var i = 0; i < configured.Length; i++)
                {
                    var limit = configured[i];
                    if (limit == null) continue;
                    for (var j = 0; j < runtimeLimits.Length; j++)
                    {
                        var runtime = runtimeLimits[j];
                        if (runtime == null || !string.Equals(limit.Name, runtime.Name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        limits[limit] = new LimitBucket(runtime.CurrentCount);
                        effectiveCounts[limit] = runtime.MaxCount;
                        break;
                    }
                }
            }
            PublishLimitsSnapshot(limits);
            _cachedEffectiveMaxCounts = effectiveCounts;
        }

        private static GridModifiers FromRuntimeData(GridModifiersData value)
        {
            if (value == null) return new GridModifiers();
            return new GridModifiers
            {
                AssemblerSpeed = value.AssemblerSpeed,
                DrillHarvestMultiplier = value.DrillHarvestMultiplier,
                GyroEfficiency = value.GyroEfficiency,
                GyroForce = value.GyroForce,
                PowerProducersOutput = value.PowerProducersOutput,
                RefineEfficiency = value.RefineEfficiency,
                RefineSpeed = value.RefineSpeed,
                ThrusterEfficiency = value.ThrusterEfficiency,
                ThrusterForce = value.ThrusterForce
            };
        }

        private static FrictionCurve FromRuntimeData(FrictionCurveSegmentData[] value)
        {
            if (value == null || value.Length == 0) return null;
            var segments = new FrictionCurveSegment[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                var segment = value[i];
                if (segment == null) continue;
                segments[i] = new FrictionCurveSegment
                {
                    StartSpeed = segment.StartSpeed,
                    EndSpeed = segment.EndSpeed,
                    StartDeceleration = segment.StartDeceleration,
                    EndDeceleration = segment.EndDeceleration
                };
            }
            return new FrictionCurve { Segments = segments };
        }

        private static AtmosphericFrictionSettings FromRuntimeData(AtmosphericFrictionData value)
        {
            if (value == null) return null;
            return new AtmosphericFrictionSettings
            {
                Enabled = value.Enabled,
                FrictionCurve = FromRuntimeData(value.FrictionCurve),
                CruiseFrictionMultiplier = value.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = value.CruiseAccelerationThreshold,
                AirDensityThreshold = value.AirDensityThreshold
            };
        }

        private static SpeedModifiers FromRuntimeData(SpeedModifiersData value)
        {
            if (value == null) return new SpeedModifiers();
            return new SpeedModifiers
            {
                MaxSpeed = value.MaxSpeed,
                MaxBoost = value.MaxBoost,
                BoostDuration = value.BoostDuration,
                BoostCoolDown = value.BoostCoolDown,
                MinimumFrictionSpeedAbsolute = value.MinimumFrictionSpeedAbsolute,
                MaximumFrictionSpeedAbsolute = value.MaximumFrictionSpeedAbsolute,
                MaximumFrictionDeceleration = value.MaximumFrictionDeceleration,
                MinimumFrictionSpeedModifier = value.MinimumFrictionSpeedModifier,
                MaximumFrictionSpeedModifier = value.MaximumFrictionSpeedModifier,
                CruiseFrictionMultiplier = value.CruiseFrictionMultiplier,
                CruiseAccelerationThreshold = value.CruiseAccelerationThreshold,
                FrictionCurve = FromRuntimeData(value.FrictionCurve),
                AtmosphericFriction = FromRuntimeData(value.AtmosphericFriction)
            };
        }
    }
}
