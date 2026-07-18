using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal static partial class CubeGridModifiers
    {
        internal static readonly ConcurrentDictionary<long, GridDefenseModifiers> DefenseModifiers =
            new ConcurrentDictionary<long, GridDefenseModifiers>();
        private static readonly List<MyEntity> ExplosionEntities = new List<MyEntity>();
        private static readonly MyStringHash EnergyDamageType = MyStringHash.GetOrCompute("Energy");
        private static readonly MyStringHash KineticDamageType = MyStringHash.GetOrCompute("Kinetic");

        public static void GridCoreDamageHandler(object target, ref MyDamageInformation damageInfo)
        {
            var myBlock = target as IMySlimBlock;
            var cubeGrid = myBlock?.CubeGrid ?? target as IMyCubeGrid;
            if (cubeGrid == null) return;

            GridDefenseModifiers modifiers;
            if (!DefenseModifiers.TryGetValue(cubeGrid.EntityId, out modifiers)) return;

            if (damageInfo.Type == MyDamageType.Bullet)
            {
                if(damageInfo.ExtraInfo == EnergyDamageType) damageInfo.Amount *= modifiers.Energy;
                else if(damageInfo.ExtraInfo == KineticDamageType) damageInfo.Amount *= modifiers.Kinetic;
                else damageInfo.Amount *= modifiers.PostShield;
            }
            if (damageInfo.Type == MyDamageType.Rocket) damageInfo.Amount *= modifiers.Rocket;
            if (damageInfo.Type == MyDamageType.Explosion) damageInfo.Amount *= modifiers.Explosion;
            if (damageInfo.Type == MyDamageType.Environment || damageInfo.Type == MyDamageType.Deformation) damageInfo.Amount *= modifiers.Environment;
            if (damageInfo.Type == MyDamageType.Drill) damageInfo.Amount *= modifiers.PostShield;
            Utils.Log($"AttackerId: {damageInfo.AttackerId} Type: {damageInfo.Type} Amount: {damageInfo.Amount} Extra: {damageInfo.ExtraInfo}");
        }

        public static void HandleLightningExplosions(ref MyExplosionInfo explosionInfo)
        {
            var closestPlanet = MyGamePruningStructure.GetClosestPlanet(explosionInfo.ExplosionSphere.Center);
            var vanilla = closestPlanet != null &&
                          explosionInfo.VoxelCutoutScale == 0 &&
                          explosionInfo.OwnerEntity == closestPlanet;
            var nebula = explosionInfo.PlayerDamage == 50 &&
                         explosionInfo.Damage == 300 &&
                         explosionInfo.ExplosionType == MyExplosionTypeEnum.CUSTOM &&
                         explosionInfo.AffectVoxels == false; //For Jaks nebula mod

            if (!vanilla && !nebula) return;

            var grid = FindClosestAffectedGrid(ref explosionInfo.ExplosionSphere);
            if (grid == null) return;

            GridDefenseModifiers modifiers;
            if (!DefenseModifiers.TryGetValue(grid.EntityId, out modifiers)) return;

            explosionInfo.Damage *= modifiers.Environment;
        }

        private static MyCubeGrid FindClosestAffectedGrid(ref BoundingSphereD explosionSphere)
        {
            ExplosionEntities.Clear();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref explosionSphere, ExplosionEntities);

            MyCubeGrid closestGrid = null;
            var closestDistanceSquared = double.MaxValue;

            foreach (var entity in ExplosionEntities)
            {
                var grid = entity as MyCubeGrid;
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var distanceSquared = Vector3D.DistanceSquared(grid.PositionComp.WorldAABB.Center, explosionSphere.Center);
                if (distanceSquared >= closestDistanceSquared) continue;

                closestDistanceSquared = distanceSquared;
                closestGrid = grid;
            }

            ExplosionEntities.Clear();
            return closestGrid;
        }
    }
}
