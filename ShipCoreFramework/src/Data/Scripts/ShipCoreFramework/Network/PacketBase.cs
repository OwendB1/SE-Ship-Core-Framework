using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal enum PacketDirection
    {
        ClientToServer,
        ServerToClient
    }

    [ProtoInclude(1000, typeof(PacketAction))]
    [ProtoInclude(2000, typeof(PacketSetMainCore))]
    [ProtoInclude(3000, typeof(PacketSetMainCoreSync))]
    [ProtoInclude(4000, typeof(PacketNotify))]
    [ProtoInclude(5000, typeof(PacketRequestConfig))]
    [ProtoInclude(6000, typeof(PacketSendConfig))]
    [ProtoInclude(7000, typeof(PacketCountdown))]
    [ProtoInclude(8000, typeof(PacketRequestRuntimeState))]
    [ProtoInclude(9000, typeof(PacketRuntimeState))]
    [ProtoContract]
    internal abstract class PacketBase
    {
        [ProtoIgnore]
        internal ulong SenderSteamId;

        [ProtoIgnore]
        internal bool SentFromServer;

        internal PacketBase() { } // Empty constructor required for deserialization
        internal abstract PacketDirection Direction { get; }
        internal abstract void Received();

        internal bool CanReceive()
        {
            return Direction == PacketDirection.ClientToServer
                ? Session.IsServer && !SentFromServer
                : Session.IsClient && SentFromServer;
        }

        protected bool TryGetSender(out IMyPlayer sender)
        {
            sender = null;
            if (SenderSteamId == 0) return false;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].SteamUserId != SenderSteamId) continue;
                sender = players[i];
                return sender.IdentityId != 0;
            }

            return false;
        }

        protected static bool HasAccess(IMyPlayer sender, IMyTerminalBlock block)
        {
            if (sender == null || sender.IdentityId == 0 || block == null) return false;
            if (sender.PromoteLevel == MyPromoteLevel.Admin || sender.PromoteLevel == MyPromoteLevel.Owner)
                return true;
            return block.HasPlayerAccess(sender.IdentityId);
        }

        protected static string Cap(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
    
    [ProtoContract]
    internal class PacketAction : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        [ProtoMember(100)]
        internal ButtonAction ActionData;
        internal PacketAction() { } // Empty constructor required for deserialization
        internal PacketAction(ButtonAction actionData)
        {
            this.ActionData = actionData;
        }
        
        internal override void Received()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;

            Utils.Log($"Received action from {SenderSteamId}: {ActionData.Action}");
            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;
            var mainCore = group.MainCoreComponent;
            if (mainCore == null || !HasAccess(sender, mainCore.CoreBlock)) return;
            switch (ActionData.Action)
            {
                case CoreActionType.Boost:
                    group.ActivateBoost();
                    break;
                case CoreActionType.Defense:
                    group.ActivateDefense();
                    break;
                case CoreActionType.PowerOverclock:
                    group.ActivatePowerOverclock();
                    break;
                default:
                    Utils.Log($"Ignored unknown core action: {ActionData.Action}", 1);
                    break;
            }
        }
    }
    
    [ProtoContract]
    internal class PacketSetMainCore : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        [ProtoMember(200)]
        internal SetMainCoreAction ActionData;

        internal PacketSetMainCore() { } // for deserialization
        internal PacketSetMainCore(SetMainCoreAction actionData)
        {
            this.ActionData = actionData;
        }

        internal override void Received()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;

            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;

            MyEntity ent;
            if (!MyEntities.TryGetEntityById(ActionData.BlockEntityId, out ent))
                return;

            var block = ent as MyCubeBlock;
            if (block == null)
                return;

            CoreComponent newMain;
            if (!group.CoreDictionary.TryGetValue(block, out newMain))
                return;

            var old = group.MainCoreComponent;
            if (!HasAccess(sender, newMain.CoreBlock) ||
                (old != null && !HasAccess(sender, old.CoreBlock))) return;
            if (!ReferenceEquals(old, newMain))
            {
                if (old != null)
                {
                    old.IsMainCore = false;
                }
                
                group.MainCoreComponent = newMain;
                group.MainCoreComponent.IsMainCore = true;
                group.SyncBeaconComponents();
            }
            
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            var sync = new PacketSetMainCoreSync(new SetMainCoreAction {
                CubegridEntityId = ActionData.CubegridEntityId,
                BlockEntityId = ActionData.BlockEntityId
            });
            foreach (var p in players) Session.Networking.SendToPlayer(sync, p.SteamUserId);
        }
    }
    
    [ProtoContract]
    internal class PacketSetMainCoreSync : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(300)]
        internal SetMainCoreAction ActionData;

        internal PacketSetMainCoreSync() { } // for deserialization
        internal PacketSetMainCoreSync(SetMainCoreAction actionData)
        {
            this.ActionData = actionData;
        }

        internal override void Received()
        {
            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;

            MyEntity ent;
            if (!MyEntities.TryGetEntityById(ActionData.BlockEntityId, out ent))
                return;

            var block = ent as MyCubeBlock;
            if (block == null)
                return;

            foreach (var kv in group.CoreDictionary)
            {
                var isMain = kv.Key == block;
                kv.Value.IsMainCore = isMain;
                if (isMain) group.MainCoreComponent = kv.Value;
            }

            group.SyncBeaconComponents();
        }
    }
    
    [ProtoContract]
    internal class PacketNotify : PacketBase {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(400)] internal string Text;
        [ProtoMember(401)] internal int TimeMs;
        [ProtoMember(402)] internal string Font;

        internal PacketNotify() {}  // for deserialization
        internal PacketNotify(string text, int timeMs = 2000, string font = "Red") {
            Text = text; TimeMs = timeMs; Font = font;
        }

        internal override void Received() {
            Text = Cap(Text, 2048);
            Font = Cap(Font, 32);
            TimeMs = Math.Max(0, Math.Min(TimeMs, 60000));
            // Runs on the client that received it
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                NotificationInstance.ShowNotification(Text, TimeMs, Font));
        }
    }

    [ProtoContract]
    internal class PacketCountdown : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(700)] internal string Key;
        [ProtoMember(701)] internal string Text;
        [ProtoMember(702)] internal int RemainingSeconds;
        [ProtoMember(703)] internal string Font;

        internal PacketCountdown() { } // for deserialization

        internal PacketCountdown(string key, string text, int remainingSeconds, string font = "Red")
        {
            Key = key;
            Text = text;
            RemainingSeconds = remainingSeconds;
            Font = font;
        }

        internal override void Received()
        {
            Key = Cap(Key, 128);
            Text = Cap(Text, 2048);
            Font = Cap(Font, 32);
            RemainingSeconds = Math.Max(0, Math.Min(RemainingSeconds, 86400));
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (RemainingSeconds <= 0)
                    NotificationInstance.CancelCountdown(Key);
                else
                    NotificationInstance.StartCountdown(Key, Text, RemainingSeconds, Font);
            });
        }
    }

    [ProtoContract]
    internal class PacketRequestConfig : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        internal PacketRequestConfig() { } // for deserialization

        internal override void Received()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;

            var response = new PacketSendConfig(MyAPIGateway.Utilities.SerializeToXML(Session.Config));

            Session.Networking.SendToPlayer(response, SenderSteamId);
        }
    }

    [ProtoContract]
    internal class PacketSendConfig : PacketBase
    {
        private const int MaxConfigCharacters = 1024 * 1024;
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)]
        internal string ConfigXml;

        internal PacketSendConfig() { } // for deserialization

        internal PacketSendConfig(string configXml)
        {
            ConfigXml = configXml;
        }

        internal override void Received()
        {
            if (Session.IsShuttingDown)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(ConfigXml))
                {
                    Utils.Log("Config sync skipped: received empty config payload.", 2, "Config Sync");
                    return;
                }
                if (ConfigXml.Length > MaxConfigCharacters)
                {
                    Utils.Log("Config sync skipped: config payload exceeded size limit.", 1, "Config Sync");
                    return;
                }

                if (Session.Config == null)
                {
                    Session.Config = new ModConfig();
                    Session.Config.LoadConfig(false);
                }

                var import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(ConfigXml);
                if (import != null)
                {
                    // Only world-level settings are serialized here, so copy them onto the already-loaded config instance.
                    Session.Config.ApplyWorldSettingsFrom(import);
                }

                Session.Config.EnsurePersistedWorldSettings();
                Session.Config.ResolveSelectedNoCore();
                Session.ApplyConfigToDefinitions();
                Session.RefreshGroupsAfterConfigChanged();
                ModAPI.BroadcastConfigReceived();
                Session.RequestRuntimeState();
            }
            catch (Exception e)
            {
                Utils.Log($"Config sync failed: {e}", 2, "Config Sync");
            }
        }
    }
    
    internal enum CoreActionType
    {
        Defense = 0,
        Boost = 1,
        PowerOverclock = 2
    }

    [ProtoContract]
    internal struct ButtonAction
    {
        [ProtoMember(1)]
        internal long CubegridEntityId;
        [ProtoMember(2)]
        internal CoreActionType Action;
    }

    [ProtoContract]
    internal struct SetMainCoreAction
    {
        [ProtoMember(1)] 
        internal long CubegridEntityId;
        [ProtoMember(2)] 
        internal long BlockEntityId;
    }
}
