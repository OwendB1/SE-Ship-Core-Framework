#region

using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class NoFlyZones
    {
        public static void EnforceNoFlyZones(GridLogic gridLogic)
        {
            if (ModSessionManager.Config.NoFlyZones == null || ModSessionManager.Config.NoFlyZones?.Count == 0) return;

            foreach (var zone in ModSessionManager.Config.NoFlyZones)
            {
                var distance = Vector3D.DistanceSquared(zone.Position, gridLogic.Grid.GetPosition());
                if (distance <= zone.Radius * zone.Radius &&
                    zone.AllowedCoresSubtype.Contains(gridLogic.ShipCore.UniqueName)) gridLogic.IsActive = false;
                //TODO: write logic for turning off blocks according to block limits
            }
        }
    }
}