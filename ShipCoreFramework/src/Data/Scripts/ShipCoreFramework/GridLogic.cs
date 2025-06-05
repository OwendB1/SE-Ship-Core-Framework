using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ShipCoreFramework
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class GridLogic : MyGameLogicComponent
    {
        public readonly HashSet<MyCubeBlock> Blocks = new HashSet<MyCubeBlock>();

        public readonly Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>> BlocksPerLimit =
            new Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>>();

        public float BoostCoolDown;
        public float BoostDuration;
        public GridDefenseModifiers DefenseModifiers = new GridDefenseModifiers();

        public bool EnableBoost;

        public IMyCubeGrid Grid;

        public bool IsActive = false;
        public bool IsMainGrid = false;
        public string ShipCoreType = string.Empty;

        public IMyFaction OwningFaction => Grid.GetOwningFaction();

        public long MajorityOwningPlayerId => GetMajorityOwner();

        public ModConfig Config => ModSessionManager.Config;

        public ShipCore ShipCore => Config.GetShipCoreBySubtype(ShipCoreType);
        public GridModifiers Modifiers => ShipCore.Modifiers;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Grid = (IMyCubeGrid)Entity;

            List<IMyCubeGrid> subs;
            var main = Utils.GetMainCubeGrid(Grid, out subs);
            if (main.EntityId == Grid.EntityId) IsMainGrid = true;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            Grid.OnPhysicsChanged += InitOnPhysicsChanged;
        }

        public void Activate(string shipCoreType)
        {
            ShipCoreType = shipCoreType;
            IsActive = true;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (!IsMainGrid || !IsActive) return;
            SpeedEnforcement.EnforceSpeedLimit(this);
        }

        private void InitOnPhysicsChanged(IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            Utils.Log($"PhysicsChanged: change triggered. Initialising logic for {grid.CustomName} (entity id: {grid.EntityId})!");
            
            //Init event handlers
            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            Grid.OnIsStaticChanged += OnIsStaticChanged;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
            Grid.OnBlockIntegrityChanged += OnBlockIntegrityChanged;
            Grid.OnGridMerge += OnGridMerge;

            if (OwningFaction != null)
            {
                if (!Config.IncludeAiFactions && OwningFaction.IsEveryoneNpc()) return;
                if (Config.IgnoreFactionTags.Contains(OwningFaction.Tag)) return;
            }

            Blocks.UnionWith(Grid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));
            
            BoostDuration = DefaultGridClassConfig.DefaultNoCore.Modifiers.BoostDuration;
            BoostCoolDown = DefaultGridClassConfig.DefaultNoCore.Modifiers.BoostCoolDown;

            if (!AddGridLogic()) return;

            List<IMyCubeGrid> subgrids;
            Utils.GetMainCubeGrid(Grid, out subgrids);
            foreach (var subgrid in subgrids)
            {
                subgrid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
                subgrid.OnIsStaticChanged += OnIsStaticChanged;
                subgrid.OnBlockAdded += OnBlockAdded;
                subgrid.OnBlockRemoved += OnBlockRemoved;

                Blocks.UnionWith(subgrid.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false));
            }

            foreach (var blockLimit in ShipCore.BlockLimits)
            {
                var blockVals = new List<KeyValuePair<IMyCubeBlock, double>>();
                foreach (var blockType in blockLimit.GetBlockTypes())
                {
                    var countingBlocks = Blocks
                        .Where(b => Utils.GetBlockTypeId(b) == blockType.TypeId &&
                                    Utils.GetBlockSubtypeId(b) == blockType.SubtypeId);
                    blockVals.AddRange(countingBlocks.Select(bl =>
                        new KeyValuePair<IMyCubeBlock, double>(bl, blockType.CountWeight)));
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

        private void OnBlockIntegrityChanged(IMySlimBlock obj)
        {
            //throw new NotImplementedException(); Owen, that's forbiden :(
        }

        private bool AddGridLogic()
        {
            try
            {
                if (this == null) throw new Exception("gridLogic cannot be null");
                if (Grid == null) throw new Exception("gridLogic.Grid cannot be null");

                Utils.Log($"Adding Logic: {Grid.EntityId} | {Grid.CustomName}");

                GridsPerFactionClassManager.AddCubeGrid(this);
                GridsPerPlayerClassManager.AddCubeGrid(this);
                
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
                DefenseModifiers = DefaultGridClassConfig.DefaultGridDefenseModifiers2X;
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

        private void ApplyModifiers(GridModifiers modifiers = null)
        {
            DefenseModifiers = ShipCore.PassiveDefenseModifiers;
            foreach (var block in from block in Blocks
                     let terminalBlock = block as IMyTerminalBlock
                     where terminalBlock != null
                     select block) CubeGridModifiers.ApplyModifiers(block, modifiers ?? Modifiers);
        }

        //TODO: fix grid merging and subgridding event registration
        private static void OnGridMerge(IMyCubeGrid main, IMyCubeGrid sub)
        {
            var mainLogic = main.GetMainGridLogic();
            if (mainLogic.Grid.EntityId == main.EntityId) // this check makes sure that the blocks and events are always added to the biggest grid instead of the rotor order
            {
                sub.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                sub.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                sub.OnBlockAdded += mainLogic.OnBlockAdded;
                sub.OnBlockRemoved += mainLogic.OnBlockRemoved;
                var fatBlocks = sub.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false);
                mainLogic.Blocks.UnionWith(fatBlocks);
            }
            else
            {
                main.OnBlockOwnershipChanged += mainLogic.OnBlockOwnershipChanged;
                main.OnIsStaticChanged += mainLogic.OnIsStaticChanged;
                main.OnBlockAdded += mainLogic.OnBlockAdded;
                main.OnBlockRemoved += mainLogic.OnBlockRemoved;
                var fatBlocks = main.GetFatBlocks<MyCubeBlock>().Where(b => b.IsPreview == false);
                mainLogic.Blocks.UnionWith(fatBlocks);
            }
            
        }

        //Event handlers
        private void OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            if (ShipCore.LargeGridStatic && !ShipCore.LargeGridMobile && !isStatic) grid.IsStatic = true;
            if (!ShipCore.LargeGridStatic && isStatic) grid.IsStatic = false;
        }

        private void OnBlockAdded(IMySlimBlock obj)
        {
            Utils.Log($"{Utils.GetBlockTypeId(obj)} | {Utils.GetBlockSubtypeId(obj)}");
            var concreteGrid = Grid as MyCubeGrid;
            if (concreteGrid?.BlocksCount > ShipCore.MaxBlocks)
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
                var countForSpecificBlock = limit.GetBlockTypes().First(l =>
                    l.TypeId == Utils.GetBlockTypeId(obj) && l.SubtypeId == Utils.GetBlockSubtypeId(obj)).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");

                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    Grid.RemoveBlock(obj);
                    List<IMyCubeGrid> subs;
                    Utils.GetMainCubeGrid(Grid, out subs);
                    foreach (var subgrid in subs) subgrid.RemoveBlock(obj);
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
                DefenseModifiers = DefaultGridClassConfig.DefaultGridDefenseModifiers2X;
                foreach (var block in Blocks)
                    CubeGridModifiers.ApplyModifiers(block, DefaultGridClassConfig.DefaultGridModifiers);
            }
            else
            {
                DefenseModifiers = ShipCore.PassiveDefenseModifiers;
            }

            var relevantLimits = GetRelevantLimits(obj);
            foreach (var limit in relevantLimits)
            {
                if (!BlocksPerLimit.ContainsKey(limit)) return;
                var index = BlocksPerLimit[limit].FindIndex(b => b.Key == obj.FatBlock);
                if (index >= 0)
                    BlocksPerLimit[limit].RemoveAt(index);
            }

            var concreteGrid = Grid as MyCubeGrid;
            if (concreteGrid?.BlocksCount < ShipCore.MinBlocks)
            {
            }

            Blocks.Remove(obj.FatBlock as MyCubeBlock);
            ApplyModifiers();
        }

        private void OnBlockOwnershipChanged(IMyCubeGrid obj)
        {
            EnforceBlockPunishment();
        }

        /*
        private void FactionsOnFactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId)
        {
            if ((action != MyFactionStateChange.FactionMemberAcceptJoin &&
                 action != MyFactionStateChange.FactionMemberLeave &&
                 action != MyFactionStateChange.FactionMemberKick) || Grid.BigOwners[0] != playerId) return;
            if (!GridsPerFactionClassManager.WillGridBeWithinFactionLimits(this, _shipCoreType)) _shipCoreType = 0;
        }
        */
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
                    if (func != null)
                    {
                        func.Enabled = false;
                    }
                    else
                    {
                        var slim = block.SlimBlock;
                        var targetIntegrity = slim.MaxIntegrity * 0.2;
                        var damageRequired = slim.Integrity - targetIntegrity;

                        if (damageRequired < 0) damageRequired = 0;
                        slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                    }
                }
            }
            else
            {
                foreach (var limit in ShipCore.BlockLimits)
                {
                    if (!BlocksPerLimit.ContainsKey(limit)) return;
                    var limitBlocks = BlocksPerLimit[limit];
                    double countWeight = 0;
                    foreach (var limitBlock in limitBlocks)
                    {
                        countWeight += limitBlock.Value;
                        if (countWeight <= limit.MaxCount) continue;
                        var func = limitBlock.Key as IMyFunctionalBlock;
                        if (func != null)
                        {
                            func.Enabled = false;
                        }
                        else
                        {
                            var slim = limitBlock.Key.SlimBlock;
                            var targetIntegrity = slim.MaxIntegrity * 0.2;
                            var damageRequired = slim.Integrity - targetIntegrity;

                            if (damageRequired < 0) damageRequired = 0;
                            slim.DoDamage((float)damageRequired, MyDamageType.Bullet, true);
                        }
                    }
                }
            }
        }

        private bool HasFunctioningBeaconIfNeeded()
        {
            return ShipCore.ForceBroadCast == false ||
                   Blocks.OfType<IMyFunctionalBlock>().Any(block => block is IMyBeacon && block.Enabled);
        }

        private IEnumerable<BlockLimit> GetRelevantLimits(IMySlimBlock block)
        {
            return ShipCore.BlockLimits.Where(limit => limit.GetBlockTypes()
                .Any(type =>
                    type.TypeId == Utils.GetBlockTypeId(block) && type.SubtypeId == Utils.GetBlockSubtypeId(block)));
        }

        private long GetMajorityOwner()
        {
            return Grid.BigOwners.FirstOrDefault();
        }
    }
}