using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

namespace ShipCoreFramework
{
    public partial class GroupComponent
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
            if (!HasCrossConnectorPunishmentLimits()) return;

            RebuildConnectorPunishmentLinks();
            OnUpgradeModulesChanged();
        }

        private bool HasCrossConnectorPunishmentLimits()
        {
            var bl = ShipCore?.BlockLimits;
            if (bl == null || bl.Length == 0) return false;
            return bl.Any(l => l != null && l.CrossConnectorPunishment);
        }

        private void RebuildConnectorPunishmentLinks()
        {
            _connectedNoCoreGroups.Clear();

            foreach (var grid in GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var connectors = ((IMyCubeGrid)grid).GetFatBlocks<IMyShipConnector>();
                if (connectors == null) continue;

                foreach (var connector in connectors)
                {
                    if (connector == null) continue;

                    IMyGridGroupData otherNoCoreGroup = null;
                    try
                    {
                        if (connector.Status == MyShipConnectorStatus.Connected)
                        {
                            var otherGrid = connector.OtherConnector?.CubeGrid;
                            var otherGroupData = otherGrid?.GetGridGroup(GridLinkTypeEnum.Mechanical);
                            if (otherGroupData != null && !ReferenceEquals(otherGroupData, MyGroup))
                            {
                                GroupComponent otherComp;
                                if (Session.GroupDict.TryGetValue(otherGroupData, out otherComp)
                                    && otherComp != null
                                    && otherComp.MainCoreComponent == null)
                                    otherNoCoreGroup = otherGroupData;
                            }
                        }
                    }
                    catch
                    {
                        otherNoCoreGroup = null;
                    }

                    if (otherNoCoreGroup == null) continue;
                    _connectedNoCoreGroups.Add(otherNoCoreGroup);
                }
            }
        }

        private void ApplyCrossConnectorPunishment()
        {
            if (_connectedNoCoreGroups.Count == 0) return;

            var blockLimits = ShipCore?.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return;

            var punishedLimits = blockLimits.Where(limit => limit != null && limit.CrossConnectorPunishment).ToArray();
            if (punishedLimits.Length == 0) return;

            var connectedGroups = _connectedNoCoreGroups.ToList();
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

                            LimitBucket groupBucket;
                            if (!Limits.TryGetValue(limit, out groupBucket))
                            {
                                groupBucket = new LimitBucket(0d);
                                Limits[limit] = groupBucket;
                            }

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
    }
}
