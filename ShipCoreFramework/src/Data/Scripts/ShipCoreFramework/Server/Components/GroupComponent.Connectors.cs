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
            OnUpgradeModulesChanged();
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
                    if (otherComp == null || otherComp.MyGroup == null || !visitedGroups.Add(otherComp.MyGroup))
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

        private bool HasConnectedBlacklistingCoreGroup()
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

            GroupComponent bestBlacklistingGroup = null;
            foreach (var otherGroupData in connectedCoreGroupData)
            {
                GroupComponent otherComp;
                if (!Session.GroupDict.TryGetValue(otherGroupData, out otherComp)) continue;
                if (otherComp == null || otherComp.MainCoreComponent == null || ReferenceEquals(otherComp, this)) continue;

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

        private string GetConnectedBlacklistLimitedBlockPunishmentReason(GroupComponent blacklistingGroup)
        {
            if (blacklistingGroup?.ShipCore == null)
                return "Connected to higher-priority or larger blacklisting core group";

            return
                $"Connected to higher-priority or larger blacklisting core group ({blacklistingGroup.ShipCore.UniqueName} blocks {ShipCore.UniqueName})";
        }

        private void ApplyCrossConnectorPunishment()
        {
            ApplyCrossConnectorPunishment(Limits);
        }

        private void ApplyCrossConnectorPunishment(ConcurrentDictionary<BlockLimit, LimitBucket> targetLimits)
        {
            if (ShipCore == null || ShipCore.CrossConnectorPunishmentWhitelisted) return;

            var connectedGroups = GetConnectedNoCoreGroupDataSnapshot();
            if (connectedGroups.Count == 0) return;
            if (targetLimits == null) return;

            var blockLimits = ShipCore.BlockLimits;
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

                            var groupBucket = targetLimits.GetOrAdd(limit, _ => new LimitBucket(0d));

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
