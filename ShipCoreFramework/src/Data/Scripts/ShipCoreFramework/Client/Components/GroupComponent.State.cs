using System;
using System.Collections.Generic;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void ObserveClientBlockCount(int delta)
        {
            if (Session.IsServer) return;
            if (_runtimeStateReceived) return;
            AddGroupBlocksCount(delta);
            InvalidateGameThreadStateCache(true);
        }

        private bool _runtimeStateReceived;
        private long _runtimeOwnerId;
        private int _runtimeCoreCount;
        private string _runtimeCoreSubtypeId = string.Empty;
        private int _runtimePlayerCoreCount;
        private int _runtimeFactionCoreCount;
        private Dictionary<string, int> _runtimeManifestCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private string[] _runtimeSpeedPunishmentReasons = Array.Empty<string>();
        private string[] _runtimeModifierPunishmentReasons = Array.Empty<string>();
        private string[] _runtimeLimitedBlockPunishmentReasons = Array.Empty<string>();
        private long _runtimeMainCoreBlockId;
        private int _runtimeLimitRevision;
        private int _runtimeLimitEnforcementRevision;
    }
}
