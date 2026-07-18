using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Commands
    {
        private static void ServerCommandSwitch(long playerId, string messageText)
        {
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (allArgs.Length < 2 || allArgs[1].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if(Session.LocalPlayer != null) ShowHelp();
                return;
            }

            var args = allArgs.Skip(1).ToArray();
            var sub = args[0].ToLower();
            var modMessage ="";
            switch (sub)
            {
                case "inventory":
                    if(Session.LocalPlayer!=null) Inventory(playerId,args);
                    return;
                case "reloadconfig":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=ReloadConfig();
                    break;
                case "listcores":
                    modMessage+=ListCores();
                    break;
                case "coreinfo":
                    modMessage+=CoreInfo(args);
                    break;
                case "listnocores":
                    modMessage+=ListNoCores();
                    break;
                case "listnfzs":
                    ListNoFlyZones();
                    return;
                case "createnfz":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=CreateNoFlyZone(args);
                    break;
                case "deletenfz":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=DeleteNoFlyZone(args);
                    break;
                case "debug":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=Debug(args);
                    break;
                case "combatlog":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=CombatLog(args);
                    break;
                case "loglevel":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=LogLevel(args);
                    break;
                case "select":
                    if(!CheckIfAdmin(playerId))return;
                    modMessage+=Select(args);
                    break;
                case "setworldspeed":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=SetWorldSpeed(args);
                    break;
                case "ignoretags":
                case "ignoretag":
                    modMessage+=IgnoreTags(playerId,args);
                    break;
                case "ignoreai":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=IgnoreAi();
                    break;
                case "unattachedmodules":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=UnattachedModules(args);
                    break;
                case "info":
                    if(Session.LocalPlayer!=null) CoreInfo(playerId);
                    return;
                default:
                    modMessage += "The command you have typed was not recognized. Did you make a typo?";
                    break;
            }
            if(Session.IsServer)
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: Server:", playerId, "Green");
            }
            else
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: LocalHost:", playerId, "Red");
            }
        }
        private static bool CheckIfAdmin(long playerId)
        {
            if(!Session.MpActive) return true;
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return (from player in players where player.IdentityId == playerId
                select player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner).FirstOrDefault();
        }
    }
}
