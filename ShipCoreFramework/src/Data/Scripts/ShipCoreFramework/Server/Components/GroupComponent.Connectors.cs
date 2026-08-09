using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
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
            QueueConnectorNetworkRefresh();
        }

        internal void OnConnectorsChanged()
        {
            if (_closing) return;
            QueueConnectorNetworkRefresh();
        }

        internal void QueueConnectorNetworkRefresh()
        {
            if (_closing || Session.IsShuttingDown) return;

            lock (_connectedGroupsLock)
            {
                if (_connectorNetworkRefreshQueued) return;
                _connectorNetworkRefreshQueued = true;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(RefreshConnectorNetwork);
        }

        private void UpdateConnectedLimitContributions(IMySlimBlock block, bool added)
        {
            if (block == null || _closing || Session.IsShuttingDown) return;

            HashSet<GroupComponent> connectedGroups = new HashSet<GroupComponent>();
            AddCachedConnectorGroups(connectedGroups);
            foreach (GroupComponent connectedGroup in connectedGroups)
                if (connectedGroup != null && !ReferenceEquals(connectedGroup, this))
                    connectedGroup.UpdateConnectorLimitContribution(this, block, added);
        }

        private void UpdateConnectorLimitContribution(GroupComponent source, IMySlimBlock block, bool added)
        {
            if (source?.MyGroup == null || block == null || _closing || Session.IsShuttingDown) return;

            var shipCore = ShipCore;
            var blockLimits = shipCore?.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return;

            var sourceHasCore = source.MainCoreComponent != null;
            if (added)
            {
                lock (_connectedGroupsLock)
                {
                    if (sourceHasCore)
                    {
                        if (!_connectedCoreGroups.Contains(source.MyGroup)) return;
                    }
                    else if (!_connectedNoCoreGroups.Contains(source.MyGroup) ||
                             shipCore.CrossConnectorPunishmentWhitelisted)
                        return;
                }

                if (sourceHasCore)
                {
                    GroupComponent owner;
                    if (!source.TryGetConnectedBlacklistingGroup(out owner) || !ReferenceEquals(owner, this)) return;
                }
            }

            var key = GridComponent.KeyOf(block);
            var contributions = new List<KeyValuePair<BlockLimit, double>>();
            foreach (var limit in blockLimits)
            {
                if (limit == null || limit.IsCriticalLimit ||
                    added && !sourceHasCore && !limit.CrossConnectorPunishment)
                    continue;

                var weight = limit.GetWeight(key);
                if (weight > 0d)
                    contributions.Add(new KeyValuePair<BlockLimit, double>(limit, weight));
            }

            if (contributions.Count == 0) return;
            IncrementLimitGeneration();

            var changed = false;
            foreach (var contribution in contributions)
            {
                var bucket = Limits.GetOrAdd(contribution.Key, _ => new LimitBucket(0d));
                lock (bucket.BucketLock)
                {
                    if (added)
                    {
                        if (!bucket.ConnectorMembers.Add(block)) continue;
                        bucket.TotalWeight += contribution.Value;
                        bucket.ConnectorWeight += contribution.Value;
                        bucket.Members.Add(block);
                    }
                    else
                    {
                        if (!bucket.ConnectorMembers.Remove(block)) continue;
                        bucket.TotalWeight -= contribution.Value;
                        bucket.ConnectorWeight -= contribution.Value;
                        bucket.Members.Remove(block);
                    }

                    changed = true;
                }
            }

            if (!changed) return;
            MarkLimitsPublished();
            Session.MarkRuntimeStateDirty(this);
            if (added) EnforceConnectorLimitPunishment();
            if (MyGroup != null) ModAPI.BroadcastLimitsRecalculated(GetRepresentativeGridId());
        }

        private void RefreshConnectorNetwork()
        {
            lock (_connectedGroupsLock)
                _connectorNetworkRefreshQueued = false;

            if (_closing || Session.IsShuttingDown) return;

            HashSet<GroupComponent> affectedGroups = DiscoverConnectorNetworkComponents();
            AddCachedConnectorGroups(affectedGroups);

            foreach (GroupComponent affectedGroup in affectedGroups)
                affectedGroup.RefreshConnectorPunishmentLinksAndState();
        }

        private void AddCachedConnectorGroups(ICollection<GroupComponent> affectedGroups)
        {
            if (affectedGroups == null) return;

            List<IMyGridGroupData> cachedGroups = GetConnectedCoreGroupDataSnapshot();
            cachedGroups.AddRange(GetConnectedNoCoreGroupDataSnapshot());
            foreach (IMyGridGroupData groupData in cachedGroups)
            {
                GroupComponent group;
                if (groupData != null && Session.GroupDict.TryGetValue(groupData, out group) && group != null)
                    affectedGroups.Add(group);
            }
        }

        private void RefreshConnectorPunishmentLinksAndState()
        {
            if (_closing) return;
            IncrementLimitGeneration();

            RebuildConnectorPunishmentLinks();
            if (MainCoreComponent == null) return;
            OnUpgradeModulesChanged(true);
        }

        private void RebuildConnectorPunishmentLinks()
        {
            HashSet<IMyGridGroupData> connectedCoreGroups = new HashSet<IMyGridGroupData>();
            HashSet<IMyGridGroupData> connectedNoCoreGroups = new HashSet<IMyGridGroupData>();

            foreach (GroupComponent otherComp in DiscoverConnectorNetworkComponents())
            {
                if (otherComp == null || ReferenceEquals(otherComp, this) || otherComp.MyGroup == null) continue;
                if (otherComp.MainCoreComponent != null)
                    connectedCoreGroups.Add(otherComp.MyGroup);
            }

            foreach (GroupComponent otherComp in GetDirectConnectedGroupComponents(this))
            {
                if (otherComp != null && otherComp.MainCoreComponent == null && otherComp.MyGroup != null)
                    connectedNoCoreGroups.Add(otherComp.MyGroup);
            }

            lock (_connectedGroupsLock)
            {
                _connectedNoCoreGroups.Clear();
                _connectedCoreGroups.Clear();
                _connectedNoCoreGroups.UnionWith(connectedNoCoreGroups);
                _connectedCoreGroups.UnionWith(connectedCoreGroups);
            }
        }

        private HashSet<GroupComponent> DiscoverConnectorNetworkComponents()
        {
            HashSet<GroupComponent> connectedGroups = new HashSet<GroupComponent>();
            HashSet<IMyGridGroupData> visitedGroups = new HashSet<IMyGridGroupData>();
            Queue<GroupComponent> pendingGroups = new Queue<GroupComponent>();

            connectedGroups.Add(this);
            if (MyGroup != null) visitedGroups.Add(MyGroup);
            pendingGroups.Enqueue(this);

            while (pendingGroups.Count > 0)
            {
                GroupComponent current = pendingGroups.Dequeue();
                foreach (GroupComponent otherComp in GetDirectConnectedGroupComponents(current))
                {
                    if (otherComp?.MyGroup == null || !visitedGroups.Add(otherComp.MyGroup))
                        continue;

                    connectedGroups.Add(otherComp);
                    pendingGroups.Enqueue(otherComp);
                }
            }

            return connectedGroups;
        }

        private static List<GroupComponent> GetDirectConnectedGroupComponents(GroupComponent source)
        {
            List<GroupComponent> connectedGroups = new List<GroupComponent>();
            HashSet<IMyGridGroupData> visitedGroups = new HashSet<IMyGridGroupData>();
            if (source == null || source._closing) return connectedGroups;

            foreach (var grid in source.GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                IEnumerable<IMyShipConnector> connectors = ((IMyCubeGrid)grid).GetFatBlocks<IMyShipConnector>();
                if (connectors == null) continue;

                foreach (IMyShipConnector connector in connectors)
                {
                    if (connector == null) continue;

                    try
                    {
                        if (connector.Status != MyShipConnectorStatus.Connected) continue;

                        IMyCubeGrid otherGrid = connector.OtherConnector?.CubeGrid;
                        IMyGridGroupData otherGroupData = otherGrid?.GetGridGroup(GridLinkTypeEnum.Mechanical);
                        if (otherGroupData == null || ReferenceEquals(otherGroupData, source.MyGroup) ||
                            !visitedGroups.Add(otherGroupData)) continue;

                        GroupComponent otherComp;
                        if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp) || otherComp == null)
                            continue;

                        connectedGroups.Add(otherComp);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return connectedGroups;
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

            GroupComponent bestBlacklistingGroup = null;
            foreach (var otherGroupData in connectedCoreGroupData)
            {
                GroupComponent otherComp;
                if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp)) continue;
                if (otherComp?.MainCoreComponent == null || ReferenceEquals(otherComp, this)) continue;

                var otherCore = otherComp.ShipCore;
                if (otherCore == null || !otherCore.IsConnectorBlacklistedCore(selfSubtypeId))
                    continue;

                if (!DoesCoreGroupOutrankForConnectorBlacklist(otherComp, this))
                    continue;

                if (CompareCoreGroupsForSelection(otherComp, bestBlacklistingGroup, true) > 0)
                    bestBlacklistingGroup = otherComp;
            }

            if (bestBlacklistingGroup == null) return false;

            blacklistingGroup = bestBlacklistingGroup;
            return true;
        }

        private void ApplyConnectorLimitContributions(ConcurrentDictionary<BlockLimit, LimitBucket> targetLimits)
        {
            if (ShipCore == null || targetLimits == null) return;

            var blockLimits = ShipCore.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return;

            var connectorLimits = blockLimits
                .Where(limit => limit != null && !limit.IsCriticalLimit)
                .ToArray();
            if (connectorLimits.Length == 0) return;

            if (!ShipCore.CrossConnectorPunishmentWhitelisted)
            {
                var noCoreLimits = connectorLimits.Where(limit => limit.CrossConnectorPunishment).ToArray();
                if (noCoreLimits.Length > 0)
                    AddConnectorLimitContributions(GetConnectedNoCoreGroupDataSnapshot(), noCoreLimits,
                        targetLimits, false);
            }

            AddConnectorLimitContributions(GetConnectedCoreGroupDataSnapshot(), connectorLimits,
                targetLimits, true);
        }

        private void CopyConnectorLimitContributions(ConcurrentDictionary<BlockLimit, LimitBucket> targetLimits)
        {
            if (targetLimits == null) return;

            foreach (var pair in Limits)
            {
                var limit = pair.Key;
                var sourceBucket = pair.Value;
                if (limit == null || sourceBucket == null) continue;

                lock (sourceBucket.BucketLock)
                {
                    if (sourceBucket.ConnectorWeight <= 0d) continue;

                    var targetBucket = targetLimits.GetOrAdd(limit, _ => new LimitBucket(0d));
                    lock (targetBucket.BucketLock)
                    {
                        targetBucket.TotalWeight += sourceBucket.ConnectorWeight;
                        targetBucket.ConnectorWeight += sourceBucket.ConnectorWeight;
                        targetBucket.ConnectorMembers.UnionWith(sourceBucket.ConnectorMembers);
                        targetBucket.Members.AddRange(sourceBucket.ConnectorMembers);
                    }
                }
            }
        }

        private void AddConnectorLimitContributions(IEnumerable<IMyGridGroupData> connectedGroups,
            BlockLimit[] connectorLimits, ConcurrentDictionary<BlockLimit, LimitBucket> targetLimits,
            bool requireBlacklistOwnership)
        {
            if (connectedGroups == null || connectorLimits == null || connectorLimits.Length == 0) return;

            foreach (var otherGroupData in connectedGroups)
            {
                if (otherGroupData == null) continue;

                GroupComponent otherComp;
                if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp) || otherComp == null) continue;
                if (requireBlacklistOwnership)
                {
                    GroupComponent owner;
                    if (otherComp.MainCoreComponent == null ||
                        !otherComp.TryGetConnectedBlacklistingGroup(out owner) ||
                        !ReferenceEquals(owner, this))
                        continue;
                }
                else if (otherComp.MainCoreComponent != null)
                    continue;

                foreach (var otherGridComp in otherComp.GridDictionary.Values)
                {
                    var blocksCopy = otherGridComp.GetBlocksCopy();
                    foreach (var block in blocksCopy)
                    {
                        if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;

                        var key = GridComponent.KeyOf(block);
                        foreach (var limit in connectorLimits)
                        {
                            var weight = limit.GetWeight(key);
                            if (weight <= 0d) continue;

                            var groupBucket = targetLimits.GetOrAdd(limit, _ => new LimitBucket(0d));

                            lock (groupBucket.BucketLock)
                            {
                                if (!groupBucket.ConnectorMembers.Add(block)) continue;
                                groupBucket.TotalWeight += weight;
                                groupBucket.ConnectorWeight += weight;
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
