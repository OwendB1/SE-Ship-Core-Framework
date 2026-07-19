namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private long GetObservedOwnerId()
        {
            return _runtimeStateReceived ? _runtimeOwnerId : ResolveLocalOwnerId();
        }
    }
}
