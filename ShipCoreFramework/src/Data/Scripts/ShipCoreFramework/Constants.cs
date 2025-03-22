// System
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
// Sandbox
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using Sandbox.Definitions;
// VRage
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI.Network;
using VRage.Sync;

namespace ShipCoreFramework
{
    public static class Constants
    {
        public static readonly string ConfigFilename = "ShipClassConfig.xml";
        public static readonly Guid GridClassStorageGUID = new Guid("a8807ad4-524d-441a-a89a-0671fbfb1dd3");
        public static readonly Guid ConfigurableSpeedGUID = new Guid("f5bad034-f449-4a0a-a1a5-190783244f3d");
        public static bool IsDedicated => MyAPIGateway.Utilities.IsDedicated;
        public static bool IsServer => MyAPIGateway.Multiplayer.IsServer;
        public static bool IsClient => !(IsServer && IsDedicated);
    }
}