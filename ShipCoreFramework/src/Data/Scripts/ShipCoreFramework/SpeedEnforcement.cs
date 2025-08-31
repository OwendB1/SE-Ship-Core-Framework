#region

using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic)
        {

            if (gridLogic?.Grid == null){return;}
            List<IMyCubeGrid> subgrids;
            var mainGrid = gridLogic.Grid.GetMainCubeGrid(out subgrids);
            if(gridLogic.Grid != mainGrid){return;}
            var maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxSpeed;
            if (gridLogic.ShipCore != null && gridLogic.BoostEnabled)//Even if there is no core, speed must still be enforced, hence the move.
            {
                maxSpeed *= gridLogic.Modifiers.MaxBoost;
            }
            var velocity = mainGrid.Physics.LinearVelocity;
            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            mainGrid.Physics.SetSpeeds(velocity, mainGrid.Physics.AngularVelocity);
            
            //if(Constants.IsServer){Utils.Log($"Max Speed: {maxSpeed}, My Velocity: {Math.Sqrt(velocity.LengthSquared())}",3);}
            
            foreach(var grid in subgrids)
            {
                grid.Physics.SetSpeeds(velocity, grid.Physics.AngularVelocity);
            }
        }
    }
}