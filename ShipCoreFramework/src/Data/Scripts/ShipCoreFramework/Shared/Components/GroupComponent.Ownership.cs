using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private bool IsIgnoredNpcGroup()
        {
            if (!Session.Config.IgnoreAiFactions) return false;

            var mainGrid = MainCoreComponent?.CoreBlock?.CubeGrid;
            return mainGrid?.IsNpcSpawnedGrid ??
                   GridDictionary.Keys.Any(grid => grid != null && grid.IsNpcSpawnedGrid);
        }

        internal long OwnerId
        {
            get
            {
                return Session.IsServer ? GetAuthoritativeOwnerId() : GetObservedOwnerId();
            }
        }

        private long ResolveLocalOwnerId()
        {
            if (MainCoreComponent == null)
                return this.GetMajorityOwnerId();

            var ownerId = MainCoreComponent.CoreBlock.OwnerId;
            ownerId = ownerId == 0 ? MainCoreComponent.CoreBlock.SlimBlock.BuiltBy : ownerId;
            return ownerId == 0 ? GetSavedOwnerIdFromMainCore() : ownerId;
        }

        private long GetSavedOwnerIdFromMainCore()
        {
            var storage = MainCoreComponent?.CoreBlock?.Storage;
            if (storage == null || !storage.ContainsKey(Session.CoreLastOwnerStorageGUID)) return 0;

            long ownerId;
            return long.TryParse(storage[Session.CoreLastOwnerStorageGUID], out ownerId) ? ownerId : 0;
        }

        internal bool IsIgnoredGroup()
        {
            if (!Session.IsServer && _runtimeStateReceived) return GetCachedIsIgnoredGroup();
            return Session.IsGameThread ? ComputeIsIgnoredGroup() : GetCachedIsIgnoredGroup();
        }

        private bool ComputeIsIgnoredGroup()
        {
            if (IsIgnoredByAiOrFactionTag()) return true;
            if (OwnerId == 0) return true;
            var player = MyAPIGateway.Players.TryGetIdentityId(OwnerId);
            return player != null && player.PromoteLevel == MyPromoteLevel.Admin &&
                   MyAPIGateway.Session.IsUserIgnorePCULimit(player.SteamUserId);
        }

        internal bool IsIgnoredByAiOrFactionTag()
        {
            if (IsIgnoredNpcGroup()) return true;

            var faction = OwningFaction;
            if (faction == null) return false;
            return Session.Config.IgnoredFactionTags != null &&
                   Session.Config.IgnoredFactionTags.Contains(faction.Tag);
        }

        internal bool IsIgnoredByAiOrFactionTagThreadSafe()
        {
            return Session.IsGameThread ? IsIgnoredByAiOrFactionTag() : GetCachedIsIgnoredByAiOrFactionTag();
        }

        private long GetRepresentativeGridId()
        {
            if (!Session.IsGameThread)
                return GetCachedRepresentativeGridId();

            if (Session.IsServer) RefreshGridStateCacheIfNeeded(false);

            var cachedRepresentativeGridId = GetCachedRepresentativeGridId();
            if (cachedRepresentativeGridId != 0) return cachedRepresentativeGridId;

            var main = MainCoreComponent?.GridComponent?.Grid;
            var grid = main ?? GridDictionary.Keys.FirstOrDefault();
            return grid?.EntityId ?? 0;
        }
    }
}
