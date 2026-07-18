using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class PacketAction
    {
        partial void ReceiveOnServer()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;

            Utils.Log($"Received action from {SenderSteamId}: {ActionData.Action}");
            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;
            CoreComponent mainCore = group.MainCoreComponent;
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

    internal partial class PacketSetMainCore
    {
        partial void ReceiveOnServer()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;

            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;

            MyEntity entity;
            if (!MyEntities.TryGetEntityById(ActionData.BlockEntityId, out entity)) return;

            MyCubeBlock block = entity as MyCubeBlock;
            if (block == null) return;

            CoreComponent newMain;
            if (!group.CoreDictionary.TryGetValue(block, out newMain)) return;

            CoreComponent oldMain = group.MainCoreComponent;
            if (!HasAccess(sender, newMain.CoreBlock) ||
                (oldMain != null && !HasAccess(sender, oldMain.CoreBlock))) return;

            if (!ReferenceEquals(oldMain, newMain))
                group.Activate(newMain);
            if (!ReferenceEquals(group.MainCoreComponent, newMain)) return;

            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            PacketSetMainCoreSync sync = new PacketSetMainCoreSync(new SetMainCoreAction
            {
                CubegridEntityId = ActionData.CubegridEntityId,
                BlockEntityId = ActionData.BlockEntityId
            });
            foreach (IMyPlayer player in players)
                Session.Networking.SendToPlayer(sync, player.SteamUserId);
        }
    }

    internal partial class PacketRequestConfig
    {
        partial void ReceiveOnServer()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;
            if (!Session.CanServeConfigRequest(SenderSteamId)) return;

            PacketSendConfig response = new PacketSendConfig(
                MyAPIGateway.Utilities.SerializeToXML(Session.Config));
            Session.Networking.SendToPlayer(response, SenderSteamId);
        }
    }
}
