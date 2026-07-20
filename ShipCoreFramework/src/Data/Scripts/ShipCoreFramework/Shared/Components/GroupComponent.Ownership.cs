using System.Linq;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal long OwnerId => Session.IsServer ? GetAuthoritativeOwnerId() : GetObservedOwnerId();

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
            if (!Session.IsServer) return GetCachedIsIgnoredGroup();
            return Session.IsGameThread ? ComputeIsIgnoredGroup() : GetCachedIsIgnoredGroup();
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
