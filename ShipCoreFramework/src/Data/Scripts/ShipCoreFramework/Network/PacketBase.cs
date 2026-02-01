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
    // [ProtoInclude(5000, typeof(PacketRequestSettings))]
    // [ProtoInclude(6000, typeof(PacketSendSettings))]
    [ProtoContract]
    internal abstract class PacketBase
    {
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
                    old.SaveCoreState();
                    old.CoreBlock?.RefreshCustomInfo();
                }

                newMain.IsMainCore = true;
                newMain.SaveCoreState();
                newMain.CoreBlock?.RefreshCustomInfo();
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
                kv.Value.SaveCoreState();
                var terminal = kv.Key as IMyTerminalBlock;
                terminal?.RefreshCustomInfo();
            }
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
