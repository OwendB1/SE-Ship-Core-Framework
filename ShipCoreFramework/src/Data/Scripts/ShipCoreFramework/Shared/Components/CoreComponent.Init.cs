using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        internal bool Init(IMyFunctionalBlock coreBlock, GridComponent gridComponent, GroupComponent groupComponent)
        {
            CoreBlock = coreBlock;
            GridComponent = gridComponent;
            _groupComponent = groupComponent;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;

            return Session.IsServer ? InitAuthoritative() : InitClientObserver();
        }
    }
}
