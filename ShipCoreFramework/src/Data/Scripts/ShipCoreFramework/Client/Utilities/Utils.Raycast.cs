using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static IMyCubeGrid RaycastForGrid(double maxDistance = 50.0)
        {
            var player = MyAPIGateway.Session?.Player;
            if (player?.Character == null) return null;

            var worldMatrix = player.Character.WorldMatrix;
            var startPos = worldMatrix.Translation + worldMatrix.Forward * 1.5;
            var endPos = startPos + worldMatrix.Forward * maxDistance;

            var hits = new List<IHitInfo>();
            MyAPIGateway.Physics.CastRay(startPos, endPos, hits);

            return hits.Select(hit => hit.HitEntity).OfType<IMyCubeGrid>().FirstOrDefault();
        }
    }
}
