using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NexusModAPI;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static NexusAPI _myNexusApi;
        private bool _startedNexus;
        private int _tick;
        internal static int CurrentTick;
        private static readonly MyStringId MatSphere = MyStringId.GetOrCompute("HighlightArea");
        private static readonly MyStringId MatLine   = MyStringId.GetOrCompute("GizmoDrawLine");
        
        internal const ushort CommandsSyncId = 32123;
        internal static bool IsClient;
        internal static bool IsServer;
        internal static bool MpActive;
        internal static bool HasStarted;
        internal static bool IsShuttingDown;
        internal static ModConfig Config = new ModConfig();
        internal static Networking Networking = new Networking(32124);
        internal static float AppliedSpeedDifferential;
        internal static readonly ConcurrentDictionary<IMyGridGroupData, GroupComponent> GroupDict = new ConcurrentDictionary<IMyGridGroupData, GroupComponent>();
        internal static readonly Guid CoreStateStorageGUID = new Guid("a8807ad4-524d-441a-a89a-0671fbfb1dd3");
        internal static readonly Guid CoreLastOwnerStorageGUID = new Guid("3521026e-9025-4c62-9de7-98379fd2439d");
        
        internal static IMyPlayer LocalPlayer => MyAPIGateway.Session.LocalHumanPlayer;
    }
}
