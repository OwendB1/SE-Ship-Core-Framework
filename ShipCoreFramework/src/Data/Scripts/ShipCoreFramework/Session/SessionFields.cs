using System;
using System.Collections.Generic;
using NexusModAPI;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static NexusAPI _myNexusApi;
        private bool _startedNexus;
        
        internal const ushort CommandsSyncId = 32123;
        internal static bool IsClient;
        internal static bool IsServer;
        internal static bool MpActive;
        internal static bool HasStarted;
        internal static bool IsShuttingDown;
        internal static ModConfig Config = new ModConfig();
        internal static Networking Networking = new Networking(32124);
        internal static readonly Dictionary<IMyGridGroupData, GroupComponent> GroupDict = new Dictionary<IMyGridGroupData, GroupComponent>();
        internal static readonly Guid CoreStateStorageGUID = new Guid("a8807ad4-524d-441a-a89a-0671fbfb1dd3");
        internal static readonly TickScheduler TickScheduler = new TickScheduler();
        
        internal static IMyPlayer LocalPlayer => MyAPIGateway.Session.LocalHumanPlayer;
    }
}