using System;
using System.Collections.Generic;
using System.Threading;

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
            int limitRevision;
            lock (_limitSnapshotLock)
            {
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
                limitRevision = Interlocked.CompareExchange(ref _publishedLimitRevision, 0, 0);
            }
            runtimeLimits.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

            int limitEnforcementRevision;
            int lastBlocksPunished;
            var limitEnforcementEvents = GetRuntimeLimitEnforcementEvents(
                out limitEnforcementRevision, out lastBlocksPunished);

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
                LimitRevision = limitRevision,
                LimitEnforcementRevision = limitEnforcementRevision,
                LastBlocksPunished = lastBlocksPunished,
                LimitEnforcementEvents = limitEnforcementEvents
            };
        }
    }
}
