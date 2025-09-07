#region

using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic, bool punish)
        {
            if (gridLogic?.Grid == null) return;
            List<IMyCubeGrid> subgrids;
            var mainGrid = gridLogic.Grid.GetLargestConnectedGrid(out subgrids);
            if(gridLogic.Grid != mainGrid) return;
            var maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond * gridLogic.Modifiers.MaxSpeed;
            if (punish) maxSpeed /= 4;
            if (gridLogic.ShipCore != null && gridLogic.BoostEnabled)
            {
                maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxBoost;
            }
            var velocity = mainGrid.Physics.LinearVelocity;
            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            mainGrid.Physics.SetSpeeds(velocity, mainGrid.Physics.AngularVelocity);
            
            foreach(var grid in subgrids)
            {
                grid.Physics.SetSpeeds(velocity, grid.Physics.AngularVelocity);
            }
        }
    }
}