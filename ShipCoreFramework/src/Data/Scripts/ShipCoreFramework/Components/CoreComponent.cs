using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private GroupComponent _groupComponent;
        private bool _isMainCore;
        private bool _eventsAttached;

        internal string SubtypeId;
        internal IMyFunctionalBlock CoreBlock;
        internal GridComponent GridComponent;

        internal bool IsMainCore
        {
            get { return _isMainCore; }
            set
            {
                if (_isMainCore == value) return;
                _isMainCore = value;

                if (Session.IsGameThread)
                {
                    SaveCoreState();
                    CoreBlock?.RefreshCustomInfo();
                    return;
                }

                var blockId = CoreBlock?.EntityId ?? 0;
                ThreadWork.Enqueue(ThreadWork.StateCategory, "core-state:" + blockId,
                    "Persist core main state " + blockId,
                    () => CoreBlock != null &&
                          !CoreBlock.MarkedForClose &&
                          !CoreBlock.Closed &&
                          !Session.IsShuttingDown,
                    delegate
                    {
                        SaveCoreState();
                        CoreBlock.RefreshCustomInfo();
                    });
            }
        }
    }
}
