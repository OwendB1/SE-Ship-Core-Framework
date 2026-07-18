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
                    if (Session.IsServer) SaveCoreState();
                    CoreBlock?.RefreshCustomInfo();
                    return;
                }

                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    if (CoreBlock == null || CoreBlock.MarkedForClose || CoreBlock.Closed || Session.IsShuttingDown)
                        return;

                    if (Session.IsServer) SaveCoreState();
                    CoreBlock.RefreshCustomInfo();
                });
            }
        }

        internal void ApplyAuthoritativeMainState(bool value)
        {
            _isMainCore = value;
            if (CoreBlock == null || CoreBlock.MarkedForClose || CoreBlock.Closed) return;
            if (Session.IsGameThread)
            {
                CoreBlock.RefreshCustomInfo();
                return;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(delegate
            {
                if (CoreBlock != null && !CoreBlock.MarkedForClose && !CoreBlock.Closed && !Session.IsShuttingDown)
                    CoreBlock.RefreshCustomInfo();
            });
        }
    }
}
