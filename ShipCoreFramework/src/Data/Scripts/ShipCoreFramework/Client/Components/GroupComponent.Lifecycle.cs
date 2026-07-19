namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private bool FinalizeObservedGridInitialization()
        {
            InvalidateGameThreadStateCache(true);
            Session.ApplyRuntimeStateForGroup(this);
            return true;
        }

        private void FinalizeObservedGridAdded()
        {
            Session.ApplyRuntimeStateForGroup(this);
        }

        private void FinalizeObservedGridRemoved(CoreComponent removedMain)
        {
            if (removedMain != null) MainCoreComponent = null;
            if (GridCount == 0) _closing = true;
        }

        private void ObserveCoreRemoved(CoreComponent lost)
        {
            if (ReferenceEquals(lost, MainCoreComponent)) MainCoreComponent = null;
        }
    }
}
