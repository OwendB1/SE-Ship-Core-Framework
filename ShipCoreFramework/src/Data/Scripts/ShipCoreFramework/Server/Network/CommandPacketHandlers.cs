using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
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
                group.Activate(newMain);

            if (!ReferenceEquals(group.MainCoreComponent, newMain)) return;
            
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
    internal class PacketRequestConfig : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        internal PacketRequestConfig() { } // for deserialization

        internal override void Received()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;
            if (!Session.CanServeConfigRequest(SenderSteamId)) return;

            var response = new PacketSendConfig(MyAPIGateway.Utilities.SerializeToXML(Session.Config));

            Session.Networking.SendToPlayer(response, SenderSteamId);
        }
    }
}

