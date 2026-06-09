using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void OnConnectorConnectionChanged(IMyShipConnector connector)
        {
            if (_closing) return;
            if (connector == null) return;
            OnConnectorsChanged();
        }

        internal void OnConnectorsChanged()
        {
            if (_closing) return;

            RebuildConnectorPunishmentLinks();
            if (MainCoreComponent == null) return;
            OnUpgradeModulesChanged();
        }

        private void RebuildConnectorPunishmentLinks()
        {
            lock (_connectedGroupsLock)
            {
                _connectedNoCoreGroups.Clear();
                _connectedCoreGroups.Clear();
            }

            foreach (var grid in GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var connectors = ((IMyCubeGrid)grid).GetFatBlocks<IMyShipConnector>();
                if (connectors == null) continue;

                foreach (var connector in connectors)
                {
                    if (connector == null) continue;

                    try
                    {
                        if (connector.Status != MyShipConnectorStatus.Connected) continue;

                        var otherGrid = connector.OtherConnector?.CubeGrid;
                        var otherGroupData = otherGrid?.GetGridGroup(GridLinkTypeEnum.Mechanical);
                        if (otherGroupData == null || ReferenceEquals(otherGroupData, MyGroup)) continue;

                        GroupComponent otherComp;
                        if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp) || otherComp == null)
                            continue;

                        lock (_connectedGroupsLock)
                        {
                            if (otherComp.MainCoreComponent == null)
                                _connectedNoCoreGroups.Add(otherGroupData);
                            else
                                _connectedCoreGroups.Add(otherGroupData);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        private bool HasConnectedBlacklistedLargerGroup()
        {
            GroupComponent blacklistingGroup;
            return TryGetConnectedBlacklistingGroup(out blacklistingGroup);
        }

        private bool TryGetConnectedBlacklistingGroup(out GroupComponent blacklistingGroup)
        {
            blacklistingGroup = null;

            var selfCore = ShipCore;
            if (MainCoreComponent == null || selfCore == null) return false;

            var connectedCoreGroupData = GetConnectedCoreGroupDataSnapshot();
            if (connectedCoreGroupData.Count == 0) return false;

            var selfSubtypeId = selfCore.SubtypeId;
            if (string.IsNullOrWhiteSpace(selfSubtypeId)) return false;

            var selfBlockCount = GroupBlocksCount;
            var connectedCoreGroups = connectedCoreGroupData
                .Select(otherGroupData =>
                {
                    GroupComponent otherComp;
                    return Session.GroupDict.TryGetValue(otherGroupData, out otherComp) ? otherComp : null;
                })
                .Where(otherComp => otherComp != null && otherComp.MainCoreComponent != null && !ReferenceEquals(otherComp, this))
                .OrderByDescending(otherComp => otherComp.GroupBlocksCount)
                .ThenBy(otherComp => otherComp.GetRepresentativeGridId())
                .ToList();

            foreach (var otherComp in connectedCoreGroups)
            {
                if (otherComp.GroupBlocksCount <= selfBlockCount)
                    continue;

                var otherCore = otherComp.ShipCore;
                if (otherCore == null || !otherCore.IsConnectorBlacklistedCore(selfSubtypeId))
                    continue;

                blacklistingGroup = otherComp;
                return true;
            }

            return false;
        }

        private string GetConnectedBlacklistLimitedBlockPunishmentReason(GroupComponent blacklistingGroup)
        {
            if (blacklistingGroup?.ShipCore == null)
                return "Connected to larger blacklisting core group";

            return
                $"Connected to larger blacklisting core group ({blacklistingGroup.ShipCore.UniqueName} blocks {ShipCore.UniqueName})";
        }

        private void ApplyCrossConnectorPunishment()
        {
            var connectedGroups = GetConnectedNoCoreGroupDataSnapshot();
            if (connectedGroups.Count == 0) return;

            var blockLimits = ShipCore?.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return;

            var punishedLimits = blockLimits.Where(limit => limit != null && limit.CrossConnectorPunishment).ToArray();
            if (punishedLimits.Length == 0) return;

            foreach (var otherGroupData in connectedGroups.Where(otherGroupData => otherGroupData != null))
            {
                GroupComponent otherComp;
                if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp) || otherComp == null) continue;
                if (otherComp.MainCoreComponent != null) continue;

                foreach (var otherGridComp in otherComp.GridDictionary.Values)
                {
                    var blocksCopy = otherGridComp.GetBlocksCopy();
                    foreach (var block in blocksCopy)
                    {
                        if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;

                        var key = GridComponent.KeyOf(block);
                        foreach (var limit in punishedLimits)
                        {
                            var weight = limit.GetWeight(key);
                            if (weight <= 0d) continue;

                            var groupBucket = Limits.GetOrAdd(limit, _ => new LimitBucket(0d));

                            lock (groupBucket.BucketLock)
                            {
                                groupBucket.TotalWeight += weight;
                                groupBucket.Members.Add(block);
                            }
                        }
                    }
                }
            }
        }

        private List<IMyGridGroupData> GetConnectedCoreGroupDataSnapshot()
        {
            lock (_connectedGroupsLock)
                return _connectedCoreGroups.Where(otherGroupData => otherGroupData != null).ToList();
        }

        private List<IMyGridGroupData> GetConnectedNoCoreGroupDataSnapshot()
        {
            lock (_connectedGroupsLock)
                return _connectedNoCoreGroups.Where(otherGroupData => otherGroupData != null).ToList();
        }
    }
}
