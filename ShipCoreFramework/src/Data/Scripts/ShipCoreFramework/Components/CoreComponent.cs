using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private GroupComponent _groupComponent;
        private bool _isMainCore;

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

                SaveCoreState();
                CoreBlock?.RefreshCustomInfo();
            }
        }
    }
}
