using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    public class CubeGridLogic
    {
        public readonly Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>> BlocksPerLimit = new Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>>();
        public readonly HashSet<MyCubeBlock> Blocks = new HashSet<MyCubeBlock>();

        private static Dictionary<long, CubeGridLogic> CubeGridLogics => ModSessionManager.CubeGridLogics;

        private long _gridClassId;
        public IMyCubeGrid Grid;

        public bool EnableBoost;
        public float BoostDuration;
        public float BoostCoolDown;

        public IMyFaction OwningFaction => Grid.GetOwningFaction();

        public long MajorityOwningPlayerId => GetMajorityOwner();

        public ModConfig Config => ModSessionManager.Config;

        public long GridClassId
        {
            get { return _gridClassId; }
            set
            {
                var gridClass = Config.GetGridClassById(value);

                var withinFactionLimit = GridsPerFactionClassManager.WillGridBeWithinFactionLimits(this, value);
                var withinPlayerLimit = GridsPerPlayerClassManager.WillGridBeWithinPlayerLimits(this, value);

                var hasMinimumPlayerCount = true;
                if (gridClass.MinPlayers > 0)
                {
                    if (OwningFaction == null)
                    {
                        hasMinimumPlayerCount = gridClass.MinPlayers <= 1;
                    }
                    else
                    {
                        hasMinimumPlayerCount = gridClass.MinPlayers <= OwningFaction.Members.Count;
                    }
                }
                
                Utils.Log($"Within Faction limit: { withinFactionLimit } | Within Player limit: { withinPlayerLimit }");
                if (!withinFactionLimit)
                {
                    Utils.ShowNotification("Grid is not within allowed limit assigned to the faction!", Grid);
                    return;
                }
                if (!withinPlayerLimit)
                {
                    Utils.ShowNotification("Grid is not within allowed limit assigned to the player!", Grid);
                    return;
                }
                if (!hasMinimumPlayerCount)
                {
                    Utils.ShowNotification($"GridClass min: {gridClass.MinPlayers} Faction count: {OwningFaction.Members.Count}", Grid);
                    Utils.ShowNotification("Faction does not have enough players to use this class!", Grid);
                    return;
                }
                
                var maxBlocks = gridClass.MaxBlocks;
                var maxPCU = gridClass.MaxPCU;
                var maxMass = gridClass.MaxMass;

                List<IMyCubeGrid> subgrids;
                var main = Utils.GetMainCubeGrid(Grid, out subgrids);
                var concreteGrid = main as MyCubeGrid;
                if (concreteGrid == null) return;
                var actualBlocks = concreteGrid.BlocksCount;
                var actualPCU = concreteGrid.BlocksPCU;
                var actualMass = concreteGrid.Mass;

                if (gridClass.LargeGridStatic && gridClass.LargeGridMobile == false && main.IsStatic == false)
                {
                    Utils.ShowNotification($"Can not set grid to class {gridClass.Name}, grid is supposed to be static!", Grid);
                    return;
                }

                foreach (var concreteSubgrid in subgrids.OfType<MyCubeGrid>())
                {
                    actualBlocks += concreteSubgrid.BlocksCount;
                    actualPCU += concreteSubgrid.BlocksPCU;
                    actualMass += concreteSubgrid.Mass;
                }

                if (maxBlocks > 1 && actualBlocks > maxBlocks)
                {
                    Utils.ShowNotification($"Can not set grid to class {gridClass.Name}, grid is {actualBlocks - maxBlocks} blocks over the limit!", Grid);
                    return;
                }
                if (maxPCU > 1 && actualPCU > maxPCU)
                {
                    Utils.ShowNotification($"Can not set grid to class {gridClass.Name}, grid is {actualPCU - maxPCU} PCU over the limit!", Grid);
                    return;
                }
                if (maxMass > 1 && actualMass > maxMass)
                {
                    Utils.ShowNotification($"Can not set grid to class {gridClass.Name}, grid is {actualMass - maxMass} KG over the limit!", Grid);
                    return;
                }

                if (!Config.IsValidGridClassId(value))
                    throw new Exception($"CubeGridLogic:: set GridClassId: invalid grid class id {value}");

                Utils.Log($"CubeGridLogic::GridClassId setting grid class to {value}", 1);
                _gridClassId = value;
                GridClassHasChanged();
            }
        }

        public GridClass GridClass => Config.GetGridClassById(GridClassId);
        public GridModifiers Modifiers => GridClass.Modifiers;
        public GridDamageModifiers DamageModifiers = new GridDamageModifiers();

        public void Initialize(IMyCubeGrid grid)
        {
            Grid = grid;
            if (ModSessionManager.CubeGridLogics.ContainsKey(Grid.EntityId) && 
                _gridClassId == DefaultGridClassConfig.DefaultGridClassDefinition.Id) return;
            
            List<IMyCubeGrid> subs;
            var main = Utils.GetMainCubeGrid(Grid, out subs);
            if (main.EntityId != Grid.EntityId)
            {
                var logic = new CubeGridLogic();
                logic.Initialize(main);
                return;
            }

            //Init event handlers
            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            Grid.OnIsStaticChanged += OnIsStaticChanged;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
            Grid.OnGridMerge += OnGridMerge;
            Grid.SpeedChanged += OnSpeedChanged;
            Grid.GridPresenceTierChanged += EnforceNoFlyZones;
            //Grid.OnMaxThrustChanged += OnSpeedChanged;

            if (OwningFaction != null)
            {
                if (!Config.IncludeAiFactions && OwningFaction.IsEveryoneNpc()) return;
                if (Config.IgnoreFactionTags.Contains(OwningFaction.Tag)) return;
            }
            Blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));

            if (Grid.Storage == null) Grid.Storage = new MyModStorageComponent();
            string value;
            if (Grid.Storage.TryGetValue(Constants.GridClassStorageGUID, out value))
            {
                long id;
                var gridClassId = long.TryParse(value, out id) ? id : 0;

                var gridClass = Config.GetGridClassById(gridClassId);
                if (gridClass.MinPlayers > 0)
                {
                    if (OwningFaction == null && gridClass.MinPlayers > 1)
                    {
                        gridClassId = 0;
                    }
                    else if (gridClass.MinPlayers > OwningFaction.Members.Count)
                    {
                        gridClassId = 0;
                    }
                }
                Utils.Log($"[CubeGridLogic] Assigning GridClassId = {gridClassId}");
                _gridClassId = gridClassId;
            }
            else
            {
                Utils.Log("[CubeGridLogic] Assigning Default Class, grid did not contain a class assignment");
                _gridClassId = DefaultGridClassConfig.DefaultGridClassDefinition.Id;
                Grid.Storage[Constants.GridClassStorageGUID] = DefaultGridClassConfig.DefaultGridClassDefinition.Id.ToString();
            }
            //Speeds
            if (Grid.Storage.TryGetValue(Constants.ConfigurableSpeedGUID, out value))
            {
                float boostVar;
                var shipSpeedData = float.TryParse(value, out boostVar) ? new List<float> { boostVar } : new List<float> { 0, 0 };
                Utils.Log("[CubeGridLogic] Assigning VelocityData");
                BoostDuration=shipSpeedData[0];
                BoostCoolDown=shipSpeedData[0];
            }
            else
            {
                BoostDuration = DefaultGridClassConfig.DefaultGridClassDefinition.Modifiers.BoostDuration;
                BoostCoolDown = DefaultGridClassConfig.DefaultGridClassDefinition.Modifiers.BoostCoolDown;     
                Grid.Storage[Constants.ConfigurableSpeedGUID] = (new List<float>{DefaultGridClassConfig.DefaultGridClassDefinition.Modifiers.BoostDuration,DefaultGridClassConfig.DefaultGridClassDefinition.Modifiers.BoostCoolDown}).ToString();
            }

            if (!AddGridLogic()) return;

            List<IMyCubeGrid> subgrids;
            Utils.GetMainCubeGrid(Grid, out subgrids);
            foreach (var subgrid in subgrids)
            {
                subgrid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
                subgrid.OnIsStaticChanged += OnIsStaticChanged;
                subgrid.OnBlockAdded += OnBlockAdded;
                subgrid.OnBlockRemoved += OnBlockRemoved;
                subgrid.SpeedChanged += OnSpeedChanged;
                subgrid.GridPresenceTierChanged += EnforceNoFlyZones;

                Blocks.UnionWith(subgrid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));
            }

            foreach (var blockLimit in GridClass.BlockLimits)
            {
                var blockVals = new List<KeyValuePair<IMyCubeBlock, double>>();
                foreach (var blockType in blockLimit.BlockTypes)
                {
                    var countingBlocks = Blocks
                        .Where(b => Utils.GetBlockTypeId(b) == blockType.TypeId &&
                                    Utils.GetBlockSubtypeId(b) == blockType.SubtypeId);
                    blockVals.AddRange(countingBlocks.Select(bl => new KeyValuePair<IMyCubeBlock, double>(bl, blockType.CountWeight)));
                }
                BlocksPerLimit[blockLimit] = blockVals;
            }

            foreach (var funcBlock in Blocks.OfType<IMyFunctionalBlock>())
            {
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                funcBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            }

            EnforceBlockPunishment();
            ApplyModifiers();
        }

        private bool AddGridLogic()
        {
            try
            {
                if (this == null) throw new Exception("gridLogic cannot be null");
                if (Grid == null) throw new Exception("gridLogic.Grid cannot be null");
                if (CubeGridLogics == null) throw new Exception("CubeGridLogics cannot be null");

                Utils.Log($"Adding Logic: {Grid.EntityId} | {Grid.CustomName}");

                GridsPerFactionClassManager.AddCubeGrid(this);
                GridsPerPlayerClassManager.AddCubeGrid(this);
                CubeGridLogics[Grid.EntityId] = this;

                var concreteGrid = Grid as MyCubeGrid;
                return concreteGrid != null;
            }
            catch (Exception e)
            {
                Utils.Log("CubeGridLogic::AddGridLogic: caught error", 3);
                Utils.LogException(e);
                return false;
            }
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null) return;
            if (HasFunctioningBeaconIfNeeded() == false)
            {
                DamageModifiers = DefaultGridClassConfig.DefaultGridDamageModifiers2X;
                foreach (var block in Blocks)
                    CubeGridModifiers.ApplyModifiers(block, DefaultGridClassConfig.DefaultGridModifiers);
            }
            if (func.Enabled)
                EnforceBlockPunishment(func);
        }

        private void OnUpgradeValuesChanged()
        {
            ApplyModifiers();
        }

        public void RemoveGridLogic()
        {
            try
            {
                // called when entity is about to be removed for whatever reason (block destroyed, entity deleted, grid despawn because of sync range, etc)
                MyAPIGateway.Session.Factions.FactionStateChanged -= FactionsOnFactionStateChanged;
                foreach (var funcBlock in Blocks.OfType<IMyFunctionalBlock>())
                {
                    funcBlock.EnabledChanged -= FuncBlockOnEnabledChanged;
                    funcBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
                }
                Grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
                Grid.OnIsStaticChanged -= OnIsStaticChanged;
                Grid.OnBlockAdded -= OnBlockAdded;
                Grid.OnBlockRemoved -= OnBlockRemoved;
                Grid.OnGridMerge -= OnGridMerge;
                Grid.SpeedChanged -= OnSpeedChanged;
                Grid.GridPresenceTierChanged -= EnforceNoFlyZones;

                CubeGridLogics.Remove(Grid.EntityId);
                GridsPerFactionClassManager.RemoveCubeGrid(this);
                GridsPerPlayerClassManager.RemoveCubeGrid(this);
            }
            catch (Exception e)
            {
                Utils.Log(e.ToString());
            }
        }

        private void ApplyModifiers(GridModifiers modifiers = null)
        {
            DamageModifiers = GridClass.DamageModifiers;
            foreach (var block in from block in Blocks let terminalBlock = block as IMyTerminalBlock where terminalBlock != null select block)
            {
                CubeGridModifiers.ApplyModifiers(block, modifiers ?? Modifiers);
            }
        }

        private static void OnGridMerge(IMyCubeGrid main, IMyCubeGrid sub)
        {
            if (!CubeGridLogics.ContainsKey(sub.EntityId) ||
                !CubeGridLogics.ContainsKey(main.EntityId)) return;

            CubeGridLogics[sub.EntityId].RemoveGridLogic();
            var mainLogic = CubeGridLogics[main.EntityId];

            sub.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
            sub.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
            sub.OnBlockAdded += mainLogic.OnBlockAdded;
            sub.OnBlockRemoved += mainLogic.OnBlockRemoved;
            sub.SpeedChanged += mainLogic.OnSpeedChanged;
            sub.GridPresenceTierChanged += mainLogic.EnforceNoFlyZones;
            mainLogic.Blocks.Clear();
            mainLogic.Blocks.UnionWith(mainLogic.Grid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));
        }

        //Event handlers
        private void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            if (GridClass.LargeGridStatic && !GridClass.LargeGridMobile && !isStatic) grid.IsStatic = true;
            if (!GridClass.LargeGridStatic && isStatic) grid.IsStatic = false;
        }
        private void GridClassHasChanged()
        {
            try
            {
                GridsPerFactionClassManager.Reset();
                foreach (var gridLogic in CubeGridLogics) GridsPerFactionClassManager.AddCubeGrid(gridLogic.Value);
                GridsPerPlayerClassManager.Reset();
                foreach (var gridLogic in CubeGridLogics) GridsPerPlayerClassManager.AddCubeGrid(gridLogic.Value);
                Grid.Storage[Constants.GridClassStorageGUID] = GridClassId.ToString();

                BlocksPerLimit.Clear();
                foreach (var blockLimit in GridClass.BlockLimits)
                {
                    var blockVals = new List<KeyValuePair<IMyCubeBlock, double>>();
                    foreach (var blockType in blockLimit.BlockTypes)
                    {
                        var countingBlocks = Blocks
                            .Where(b => Utils.GetBlockTypeId(b) == blockType.TypeId &&
                                        Utils.GetBlockSubtypeId(b) == blockType.SubtypeId);
                        blockVals.AddRange(countingBlocks.Select(bl => new KeyValuePair<IMyCubeBlock, double>(bl, blockType.CountWeight)));
                    }
                    BlocksPerLimit[blockLimit] = blockVals;
                }
                EnforceBlockPunishment();
                ApplyModifiers();
            }
            catch (Exception e)
            {
                Utils.Log("CubeGridLogic::OnGridClassChanged: Unable to set Class Because:", 3);
                Utils.LogException(e);
                return;
            }
            Utils.Log($"CubeGridLogic::OnGridClassChanged: new grid class id = {GridClassId}", 2);
        }

        private void OnBlockAdded(IMySlimBlock obj)
        {
            Utils.Log($"{Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
            var concreteGrid = Grid as MyCubeGrid;
            if (concreteGrid?.BlocksCount > GridClass.MaxBlocks)
            {
                Grid.RemoveBlock(obj);
                return;
            }

            var relevantLimits = GetRelevantLimits(obj);
            foreach (var limit in relevantLimits)
            {
                if (!BlocksPerLimit.ContainsKey(limit)) continue;
                var limitBlocks = BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(b => b.Value);
                var countForSpecificBlock = limit.BlockTypes.First(l =>
                    l.TypeId == Utils.GetBlockTypeId(obj) && l.SubtypeId == Utils.GetBlockSubtypeId(obj)).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");

                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    Grid.RemoveBlock(obj);
                    List<IMyCubeGrid> subs;
                    Utils.GetMainCubeGrid(Grid, out subs);
                    foreach (var subgrid in subs)
                    {
                        subgrid.RemoveBlock(obj);
                    }
                    return;
                }
                BlocksPerLimit[limit].Add(new KeyValuePair<IMyCubeBlock, double>(obj.FatBlock, countForSpecificBlock));
            }

            Blocks.Add(obj.FatBlock as MyCubeBlock);

            var funcBlock = obj.FatBlock as IMyFunctionalBlock;
            if (funcBlock != null)
                funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
            ApplyModifiers();
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            if (obj.FatBlock != null && HasFunctioningBeaconIfNeeded() == false)
            {
                DamageModifiers = DefaultGridClassConfig.DefaultGridDamageModifiers2X;
                foreach (var block in Blocks)
                {
                    CubeGridModifiers.ApplyModifiers(block, DefaultGridClassConfig.DefaultGridModifiers);
                }
            }
            else DamageModifiers = GridClass.DamageModifiers;

            var relevantLimits = GetRelevantLimits(obj);
            foreach (var limit in relevantLimits)
            {
                if (!BlocksPerLimit.ContainsKey(limit)) return;
                var index = BlocksPerLimit[limit].FindIndex(b => b.Key == obj.FatBlock);
                if (index >= 0)
                    BlocksPerLimit[limit].RemoveAt(index);
            }

            var concreteGrid = Grid as MyCubeGrid;
            if (concreteGrid?.BlocksCount < GridClass.MinBlocks)
            {
                GridClassId = 0;
            }
            Blocks.Remove(obj.FatBlock as MyCubeBlock);
            ApplyModifiers();
        }

        private void OnBlockOwnershipChanged(IMyCubeGrid obj)
        {
            EnforceBlockPunishment();
        }

        private void OnSpeedChanged(IMyCubeGrid obj)
        {
            EnforceNoFlyZones(obj);
        }

        public void EnforceSpeedLimit(IMyCubeGrid obj)
        {
            var gridLogic = obj.GetMainGridLogic();
            var gridClass = gridLogic?.GridClass;
            if (gridClass == null) return;
            if(gridLogic?.BoostDuration == null || gridLogic?.BoostCoolDown == null)
            {
                string value;
                if (Grid.Storage.TryGetValue(Constants.ConfigurableSpeedGUID, out value))
                {
                    float boostVar;
                    var shipSpeedData = float.TryParse(value, out boostVar) ? new List<float> { boostVar } : 
                        new List<float> { gridClass.Modifiers?.BoostDuration ?? Config.DefaultGridClass.Modifiers.BoostDuration*60.0f, 0 };
                    gridLogic.BoostDuration = shipSpeedData[0];
                    gridLogic.BoostCoolDown = shipSpeedData[1];
                }
            }

            if (gridLogic.BoostDuration == 0 && gridLogic.BoostCoolDown == 0)
            {
                gridLogic.BoostDuration = gridClass.Modifiers.BoostDuration * 60.0f;
            }

            var limitedSpeed = gridClass.Modifiers?.MaxSpeed ?? Config.DefaultGridClass.Modifiers.MaxSpeed;
            var boostSpeed = gridClass.Modifiers?.MaxBoost ?? Config.DefaultGridClass.Modifiers.MaxBoost;

            var myGrid = obj;
            var velocity = myGrid.Physics.LinearVelocity;
            // If cooldown is active, decrement it
            if (gridLogic.BoostCoolDown > 0)
            {
                gridLogic.BoostCoolDown -= 1.0f;
                if (gridLogic.BoostCoolDown <= 0)
                {
                    // Reset boost duration when cooldown ends
                    gridLogic.BoostDuration = gridClass.Modifiers.BoostDuration * 60.0f;
                }
            }
            // Check if boost is enabled and cooldown is inactive
            if (gridLogic.EnableBoost && (gridLogic.BoostDuration>0f))
            {
                limitedSpeed *= boostSpeed;
                gridLogic.BoostDuration -= 1.0f;
                // If boost duration is depleted, start cooldown
                if (gridLogic.BoostDuration <= 0f)
                {
                    gridLogic.BoostCoolDown = gridClass.Modifiers.BoostCoolDown * 60.0f;
                    gridLogic.EnableBoost = false;
                    Utils.ShowNotification("Booster Disengaged!",myGrid,600);
                }
            }

            // Store the current boost status
            Grid.Storage[Constants.ConfigurableSpeedGUID] = new List<float> {gridLogic.BoostDuration, gridLogic.BoostCoolDown}.ToString();

            // Ensure the velocity does not exceed the limited speed
            if (velocity.LengthSquared() > limitedSpeed * limitedSpeed)
            {
                velocity = Vector3.Normalize(velocity) * limitedSpeed;
            }
            // Apply the calculated velocity
            myGrid.Physics.SetSpeeds(velocity, myGrid.Physics.AngularVelocity);
            //Utils.Log($"Cooldown: {gridLogic.BoostCoolDown}\nDuration: {gridLogic.BoostDuration}");
        }

        private void EnforceNoFlyZones(IMyCubeGrid obj)
        {
            if(Config.NoFlyZones == null || Config.NoFlyZones?.Count == 0) return;
            var gridLogic = obj.GetMainGridLogic();
            var gridClassId = gridLogic?.GridClass?.Id ?? 0;
            foreach (var block in from zone in Config.NoFlyZones 
                     let range = Vector3D.Distance(obj.WorldMatrix.Translation,new Vector3D(zone.X, zone.Y, zone.Z)) 
                     where range < zone.Radius 
                     where !zone.AllowedClassesById.Contains(gridClassId) 
                     select obj into myGrid 
                     select myGrid.GetFatBlocks<IMyFunctionalBlock>() into blocksOnGrid 
                     from block in blocksOnGrid 
                     where block != null 
                     where block.Enabled
                     where GridClass.BlockLimits.Any(limit => limit.TurnedOffByNoFlyZone && limit.BlockTypes.Any(type => 
                                                                  type.TypeId == Utils.GetBlockTypeId(block) && 
                                                                  type.SubtypeId == Utils.GetBlockSubtypeId(block)))
                     select block)
            {
                block.Enabled = false;
            }

        }

        private void FactionsOnFactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId)
        {
            if ((action != MyFactionStateChange.FactionMemberAcceptJoin &&
                 action != MyFactionStateChange.FactionMemberLeave &&
                 action != MyFactionStateChange.FactionMemberKick) || Grid.BigOwners[0] != playerId) return;
            if (!GridsPerFactionClassManager.WillGridBeWithinFactionLimits(this, GridClassId)) GridClassId = 0;
        }

        private void EnforceBlockPunishment(IMyCubeBlock block = null)
        {
            if (block != null)
            {
                var relevantLimits = GetRelevantLimits(block.SlimBlock);
                foreach (var limit in relevantLimits)
                {
                    var limitBlocks = BlocksPerLimit[limit];
                    var countWeight = limitBlocks.Sum(l => l.Value);
                    Utils.Log($"Block check: {limit.Name} | {countWeight} | {limit.MaxCount}");
                    if (countWeight <= limit.MaxCount) continue;
                    var func = block as IMyFunctionalBlock;
                    if (func != null) func.Enabled = false;
                    else
                    {
                        var slim = block.SlimBlock;
                        var targetIntegrity = slim.MaxIntegrity * 0.2;
                        var damageRequired = slim.Integrity - targetIntegrity;

                        if (damageRequired < 0)
                        {
                            damageRequired = 0;
                        }
                        slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                    }
                }
            }
            else
            {
                foreach (var limit in GridClass.BlockLimits)
                {
                    if (!BlocksPerLimit.ContainsKey(limit)) return;
                    var limitBlocks = BlocksPerLimit[limit];
                    double countWeight = 0;
                    foreach (var limitBlock in limitBlocks)
                    {
                        countWeight += limitBlock.Value;
                        if (countWeight <= limit.MaxCount) continue;
                        var func = limitBlock.Key as IMyFunctionalBlock;
                        if (func != null) func.Enabled = false;
                        else
                        {
                            var slim = limitBlock.Key.SlimBlock;
                            var targetIntegrity = slim.MaxIntegrity * 0.2;
                            var damageRequired = slim.Integrity - targetIntegrity;

                            if (damageRequired < 0)
                            {
                                damageRequired = 0;
                            }
                            slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                        }
                    }
                }
            }
            
        }

        private bool HasFunctioningBeaconIfNeeded()
        {
            return GridClass.ForceBroadCast == false || Blocks.OfType<IMyFunctionalBlock>().Any(block => block is IMyBeacon && block.Enabled);
        }

        private IEnumerable<BlockLimit> GetRelevantLimits(IMySlimBlock block)
        {
            return GridClass.BlockLimits.Where(limit => limit.BlockTypes
                .Any(type => type.TypeId == Utils.GetBlockTypeId(block) && type.SubtypeId == Utils.GetBlockSubtypeId(block)));
        }

        private long GetMajorityOwner()
        {
            return Grid.BigOwners.FirstOrDefault();
        }
    }
}