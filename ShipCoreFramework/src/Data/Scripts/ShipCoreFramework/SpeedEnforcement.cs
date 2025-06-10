#region

using System.Collections.Generic;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic)
        {
            var gridClass = gridLogic?.ShipCore;
            if (gridClass == null) return;
            if (gridLogic?.BoostDuration == null || gridLogic?.BoostCoolDown == null)
            {
                string value;
                if (gridLogic.Grid.Storage.TryGetValue(Constants.ConfigurableSpeedGUID, out value))
                {
                    float boostVar;
                    var shipSpeedData = float.TryParse(value, out boostVar)
                        ? new List<float> { boostVar }
                        : new List<float>
                        {
                            gridClass.Modifiers?.BoostDuration ??
                            ModSessionManager.Config.DefaultNoCore.Modifiers.BoostDuration * 60.0f,
                            0
                        };
                    gridLogic.BoostDuration = shipSpeedData[0];
                    gridLogic.BoostCoolDown = shipSpeedData[1];
                }
            }

            if (gridLogic.BoostDuration == 0 && gridLogic.BoostCoolDown == 0)
                gridLogic.BoostDuration = gridClass.Modifiers.BoostDuration * 60.0f;

            var limitedSpeed = gridClass.Modifiers?.MaxSpeed ??
                               ModSessionManager.Config.DefaultNoCore.Modifiers.MaxSpeed;
            var boostSpeed = gridClass.Modifiers?.MaxBoost ?? ModSessionManager.Config.DefaultNoCore.Modifiers.MaxBoost;

            var myGrid = gridLogic.Grid;
            var velocity = myGrid.Physics.LinearVelocity;
            // If cooldown is active, decrement it
            if (gridLogic.BoostCoolDown > 0)
            {
                gridLogic.BoostCoolDown -= 1.0f;
                if (gridLogic.BoostCoolDown <= 0)
                    // Reset boost duration when cooldown ends
                    gridLogic.BoostDuration = gridClass.Modifiers.BoostDuration * 60.0f;
            }

            // Check if boost is enabled and cooldown is inactive
            if (gridLogic.EnableBoost && gridLogic.BoostDuration > 0f)
            {
                limitedSpeed *= boostSpeed;
                gridLogic.BoostDuration -= 1.0f;
                // If boost duration is depleted, start cooldown
                if (gridLogic.BoostDuration <= 0f)
                {
                    gridLogic.BoostCoolDown = gridClass.Modifiers.BoostCoolDown * 60.0f;
                    gridLogic.EnableBoost = false;
                    Utils.ShowNotification("Booster Disengaged!", 1000);
                }
            }

            // TODO: Store the current boost status in ShipCoreLogic
            // Grid.Storage[Constants.ConfigurableSpeedGUID] = new List<float> {gridLogic.BoostDuration, gridLogic.BoostCoolDown}.ToString();

            // Ensure the velocity does not exceed the limited speed
            if (velocity.LengthSquared() > limitedSpeed * limitedSpeed)
                velocity = Vector3.Normalize(velocity) * limitedSpeed;
            // Apply the calculated velocity
            myGrid.Physics.SetSpeeds(velocity, myGrid.Physics.AngularVelocity);
            //Utils.Log($"Cooldown: {gridLogic.BoostCoolDown}\nDuration: {gridLogic.BoostDuration}");
        }
    }
}