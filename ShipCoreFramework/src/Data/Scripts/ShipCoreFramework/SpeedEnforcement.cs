#region
using System;
using VRageMath;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic)
        {

            if (gridLogic?.Grid == null){return;}
            List<IMyCubeGrid> subgrids;
            var MainGrid = gridLogic.Grid.GetMainCubeGrid(out subgrids);
            if(gridLogic.Grid != MainGrid){return;}
            var maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxSpeed;
            if (gridLogic.ShipCore != null && gridLogic.BoostEnabled)//Even if there is no core, speed must still be enforced, hence the move.
            {
                maxSpeed *= gridLogic.Modifiers.MaxBoost;
            }
            var velocity = MainGrid.Physics.LinearVelocity;
            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            MainGrid.Physics.SetSpeeds(velocity, MainGrid.Physics.AngularVelocity);
            
            //if(Constants.IsServer){Utils.Log($"Max Speed: {maxSpeed}, My Velocity: {Math.Sqrt(velocity.LengthSquared())}",3);}
            
            foreach(IMyCubeGrid grid in subgrids)
            {
                grid.Physics.SetSpeeds(velocity, grid.Physics.AngularVelocity);
            }
        }
    }
}