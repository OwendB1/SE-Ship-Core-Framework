using System;
using System.Threading;
using VRage.Game.ModAPI;
using VRageMath;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal struct CachedGridState
        {
            internal long EntityId;
            internal string CustomName;
            internal Vector3D Position;
            internal long FirstOwnerId;
        }

        internal GridModifiers Modifiers => GetCachedActiveGridModifiers();
        internal SpeedModifiers SpeedModifiers => GetCachedActiveSpeedModifiers();
        private const int DefaultGridStateCacheCapacity = 4;
        private const int GridStateCacheRefreshIntervalTicks = 10;
        private const int MassCacheRefreshIntervalTicks = 60;
        private const int IgnoredStateCacheRefreshIntervalTicks = 60;

        internal int GroupPCU {
            get
            {
                if (!Session.IsServer)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
                if (!Session.IsGameThread)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);

                RefreshGridStateCacheIfNeeded(true);
                return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
            }
        }

        internal float GroupMass {
            get
            {
                if (!Session.IsServer) return _cachedConfiguredMass;
                if (!Session.IsGameThread)
                    return _cachedConfiguredMass;

                RefreshMassCacheIfDirty();
                return _cachedConfiguredMass;
            }
        }

        internal float GroupDryMass {
            get
            {
                if (!Session.IsServer) return _cachedDryMass;
                if (!Session.IsGameThread)
                    return _cachedDryMass;

                RefreshMassCacheIfDirty();
                return _cachedDryMass;
            }
        }

        private float _cachedDryMass;
        private float _cachedConfiguredMass;
        private int _cachedGroupPCU;
        private long _cachedRepresentativeGridId;
        private MyCubeGrid[] _cachedMovableGrids = Array.Empty<MyCubeGrid>();
        private long[] _cachedMechanicalGridIds = Array.Empty<long>();
        private CachedGridState[] _cachedGridStates = Array.Empty<CachedGridState>();
        private bool _cachedIsIgnoredGroup;
        private GridModifiers _cachedActiveGridModifiers = new GridModifiers();
        private SpeedModifiers _cachedActiveSpeedModifiers = new SpeedModifiers();
        private GridDefenseModifiers _cachedPassiveDefenseModifiers = new GridDefenseModifiers();
        private GridDefenseModifiers _cachedActiveDefenseModifiers = new GridDefenseModifiers();
        private IMyCubeBlock _cachedNoCoreDirectionLockReferenceBlock;
        private bool _cachedIsIgnoredByAiOrFactionTag;
        private bool _gridStateCacheDirty = true;
        private bool _massCacheDirty = true;
        private bool _directionReferenceCacheDirty = true;
        private bool _modifierStateCacheDirty = true;
        private bool _ignoredStateCacheDirty = true;
        private int _nextGridStateCacheRefreshTick;
        private int _nextMassCacheRefreshTick;
        private int _nextIgnoredStateCacheRefreshTick;

        internal void InvalidateGameThreadStateCache(bool directionReferenceMayChange)
        {
            _gridStateCacheDirty = true;
            _massCacheDirty = true;
            _ignoredStateCacheDirty = true;

            if (directionReferenceMayChange)
                _directionReferenceCacheDirty = true;
        }

        internal IMyCubeBlock GetDirectionLockReferenceBlock()
        {
            var mainCoreBlock = MainCoreComponent?.CoreBlock;
            if (mainCoreBlock != null) return mainCoreBlock;
            if (Deactivated) return null;

            if (Session.IsServer)
                RefreshAuthoritativeDirectionLockReference();

            return GetCachedDirectionLockReferenceBlock();
        }

        private IMyCubeBlock GetCachedDirectionLockReferenceBlock()
        {
            var referenceBlock = _cachedNoCoreDirectionLockReferenceBlock;
            if (referenceBlock == null || referenceBlock.MarkedForClose || referenceBlock.Closed ||
                referenceBlock.CubeGrid == null)
                return null;

            return referenceBlock;
        }

        internal long GetCachedRepresentativeGridId()
        {
            return Interlocked.CompareExchange(ref _cachedRepresentativeGridId, 0L, 0L);
        }

        internal long[] GetCachedMechanicalGridIds()
        {
            return _cachedMechanicalGridIds ?? Array.Empty<long>();
        }

        internal MyCubeGrid[] GetCachedMovableGrids()
        {
            return _cachedMovableGrids ?? Array.Empty<MyCubeGrid>();
        }

        internal CachedGridState[] GetCachedGridStates()
        {
            return _cachedGridStates ?? Array.Empty<CachedGridState>();
        }

        internal bool GetCachedIsIgnoredGroup()
        {
            return _cachedIsIgnoredGroup;
        }

        internal bool GetCachedIsIgnoredByAiOrFactionTag()
        {
            return _cachedIsIgnoredByAiOrFactionTag;
        }

        internal GridModifiers GetCachedActiveGridModifiers()
        {
            return _cachedActiveGridModifiers ?? new GridModifiers();
        }

        internal SpeedModifiers GetCachedActiveSpeedModifiers()
        {
            return _cachedActiveSpeedModifiers ?? new SpeedModifiers();
        }

        internal GridDefenseModifiers GetCachedPassiveDefenseModifiers()
        {
            return _cachedPassiveDefenseModifiers ?? new GridDefenseModifiers();
        }

        internal GridDefenseModifiers GetCachedActiveDefenseModifiers()
        {
            return _cachedActiveDefenseModifiers ?? new GridDefenseModifiers();
        }
    }
}
