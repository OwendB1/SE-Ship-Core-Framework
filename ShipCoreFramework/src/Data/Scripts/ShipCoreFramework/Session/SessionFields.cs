using System;
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
        internal static bool HasSyncedServerConfig;
        internal static bool IsShuttingDown;
        internal static ModConfig Config = new ModConfig();
        internal static Networking Networking = new Networking(32124);
        internal static float AppliedSpeedDifferential;
        internal static readonly Dictionary<IMyGridGroupData, GroupComponent> GroupDict = new Dictionary<IMyGridGroupData, GroupComponent>();
        internal static readonly Guid CoreStateStorageGUID = new Guid("a8807ad4-524d-441a-a89a-0671fbfb1dd3");
        
        internal static IMyPlayer LocalPlayer => MyAPIGateway.Session.LocalHumanPlayer;
    }
}
