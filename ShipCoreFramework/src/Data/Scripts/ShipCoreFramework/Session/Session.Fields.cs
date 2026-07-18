using System;
using System.Collections.Concurrent;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private int _tick;
        internal static int CurrentTick;
        
        internal const ushort CommandsSyncId = 32123;
        internal static bool IsClient;
        internal static bool IsServer;
        internal static bool MpActive;
        internal static bool IsShuttingDown;
        internal static volatile bool IsInitialGroupScan;
        internal static int GameThreadId;
        internal static ModConfig Config = new ModConfig();
        internal static Networking Networking = new Networking(32124);
        internal static float AppliedSpeedDifferential;
        internal static readonly ConcurrentDictionary<IMyGridGroupData, GroupComponent> GroupDict = new ConcurrentDictionary<IMyGridGroupData, GroupComponent>();
        internal static readonly Guid CoreLastOwnerStorageGUID = new Guid("3521026e-9025-4c62-9de7-98379fd2439d");
        
        internal static IMyPlayer LocalPlayer => MyAPIGateway.Session.LocalHumanPlayer;
        internal static bool IsGameThread => GameThreadId != 0 && Environment.CurrentManagedThreadId == GameThreadId;
    }
}
