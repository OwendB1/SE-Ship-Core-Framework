namespace ShipCoreFramework
{
    public static class BroadcastEnforcement
    {
        public static void EnforceBroadcast(GroupComponent groupComponent)
        {
            var core = groupComponent.ShipCore;
            if (core.ForceBroadCast)
            {
                if (groupComponent.MainCoreComponent == null) return;
                var block = groupComponent.MainCoreComponent.CoreBlock;
                block.Enabled = true;
                block.Radius = core.ForceBroadCastRange;
                if(!block.HudText.Contains(core.UniqueName)) block.HudText = $"{block.CubeGrid.DisplayName} : {core.UniqueName}";
            }
            groupComponent.EnforceOverCapacity();
        }
    }
}