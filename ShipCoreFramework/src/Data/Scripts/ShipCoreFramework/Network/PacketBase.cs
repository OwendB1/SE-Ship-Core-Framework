using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    [ProtoInclude(1000, typeof(PacketAction))]
    [ProtoInclude(2000, typeof(PacketSetMainCore))]
    [ProtoInclude(3000, typeof(PacketSetMainCoreSync))]
    [ProtoInclude(4000, typeof(PacketNotify))]
    [ProtoInclude(5000, typeof(PacketRequestConfig))]
    [ProtoInclude(6000, typeof(PacketSendConfig))]
    [ProtoContract]
    internal abstract class PacketBase
    {
        [ProtoIgnore]
        internal ulong SenderSteamId;

        [ProtoIgnore]
        internal bool SentFromServer;

        internal PacketBase() { } // Empty constructor required for deserialization
        internal abstract void Received();
    }
    
    [ProtoContract]
    internal class PacketAction : PacketBase
    {
        [ProtoMember(100)]
        internal ButtonAction ActionData;
        internal PacketAction() { } // Empty constructor required for deserialization
        internal PacketAction(ButtonAction actionData)
        {
            this.ActionData = actionData;
        }
        
        internal override void Received()
        {
            Utils.Log($"Received action from {SenderSteamId}: {ActionData.IsBoost}");
            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;
            if (ActionData.IsBoost)
            {
                group.ActivateBoost();
            }
            else
            {
                group.ActivateDefense();
            }
        }
    }
    
    [ProtoContract]
    internal class PacketSetMainCore : PacketBase
    {
        [ProtoMember(200)]
        internal SetMainCoreAction ActionData;

        internal PacketSetMainCore() { } // for deserialization
        internal PacketSetMainCore(SetMainCoreAction actionData)
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

            CoreComponent newMain;
            if (!group.CoreDictionary.TryGetValue(block, out newMain))
                return;

            var old = group.MainCoreComponent;
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
        [ProtoMember(400)] internal string Text;
        [ProtoMember(401)] internal int TimeMs;
        [ProtoMember(402)] internal string Font;

        internal PacketNotify() {}  // for deserialization
        internal PacketNotify(string text, int timeMs = 2000, string font = "Red") {
            Text = text; TimeMs = timeMs; Font = font;
        }

        internal override void Received() {
            // Runs on the client that received it
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                NotificationInstance.ShowNotification(Text, TimeMs, Font));
        }
    }

    [ProtoContract]
    internal class PacketRequestConfig : PacketBase
    {
        internal PacketRequestConfig() { } // for deserialization

        internal override void Received()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            var response = new PacketSendConfig(MyAPIGateway.Utilities.SerializeToXML(Session.Config));

            Session.Networking.SendToPlayer(response, SenderSteamId);
        }
    }

    [ProtoContract]
    internal class PacketSendConfig : PacketBase
    {
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

                if (Session.Config == null)
                {
                    Session.Config = new ModConfig();
                    Session.Config.LoadConfig(false);
                }

                ModConfig import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(ConfigXml);
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
            }
            catch (Exception e)
            {
                Utils.Log($"Config sync failed: {e}", 2, "Config Sync");
            }
        }
    }
    
    [ProtoContract]
    internal struct ButtonAction
    {
        [ProtoMember(1)]
        internal long CubegridEntityId;
        [ProtoMember(2)]
        internal bool IsBoost;
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
