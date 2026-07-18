namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private long GetObservedOwnerId()
        {
            if (_runtimeStateReceived) return _runtimeOwnerId;

            var ownerId = ResolveLocalOwnerId();
            return IsIgnoredNpcGroup() ? 0 : ownerId;
        }
    }
}
