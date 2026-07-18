namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private long GetObservedOwnerId()
        {
            if (_runtimeStateReceived) return _runtimeOwnerId;
            return ResolveLocalOwnerId();
        }
    }
}
