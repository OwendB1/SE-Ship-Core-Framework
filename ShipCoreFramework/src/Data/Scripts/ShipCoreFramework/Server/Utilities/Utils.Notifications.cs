using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        static partial void ForwardServerLogMessage(string msg, int logPriority, string tooltip)
        {
            if (!Session.IsServer || !Session.MpActive || Session.Networking == null ||
                Session.IsShuttingDown) return;

            try
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    var config = Session.Config;
                    if (config == null || !config.DebugMode || logPriority > config.ClientOutputLogLevel ||
                        Session.IsShuttingDown) return;

                    var players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    var localSteamId = Session.LocalPlayer?.SteamUserId ?? 0UL;
                    var packet = new PacketNotify
                    {
                        Text = msg,
                        IsDebugLog = true,
                        LogPriority = logPriority,
                        LogTooltip = tooltip
                    };

                    var admins = new List<ulong>();
                    foreach (var player in players)
                    {
                        if (player == null || player.SteamUserId == 0 || player.SteamUserId == localSteamId) continue;
                        if (player.PromoteLevel != MyPromoteLevel.Admin &&
                            player.PromoteLevel != MyPromoteLevel.Owner) continue;

                        admins.Add(player.SteamUserId);
                    }

                    Session.Networking.SendToPlayers(packet, admins);
                });
            }
            catch
            {
                // Ignore debug forwarding failures during startup/shutdown.
            }
        }

        internal static long GetPlayerIdFromSteamId(ulong steamId)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return players.FirstOrDefault(player => player.SteamUserId == steamId)?.IdentityId ?? 0;
        }

        internal static void ShowNotification(string msg, long playerEntityId = 0, int disappearTime = 5000,
            bool isCombatLog = false, string font = MyFontEnum.Red, Vector3D? combatLogPosition = null)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (isCombatLog)
                {
                    if (!Session.Config.CombatLogging) return;
                    var players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    double range = Session.Config.CombatLoggingBroadcastRangeMeters;
                    double rangeSquared = range * range;
                    var inRange = new List<ulong>();
                    foreach (var player in players)
                    {
                        if (player == null || player.SteamUserId == 0) continue;
                        if (combatLogPosition.HasValue &&
                            Vector3D.DistanceSquared(player.GetPosition(), combatLogPosition.Value) > rangeSquared)
                            continue;
                        inRange.Add(player.SteamUserId);
                    }

                    Session.Networking.SendToPlayers(new PacketNotify(msg, disappearTime, font), inRange);
                }
                else
                {
                    if (!Session.MpActive) playerEntityId = MyAPIGateway.Session.LocalHumanPlayer?.IdentityId ?? 0;
                    if (playerEntityId == 0) return;
                    var steamUserId = MyAPIGateway.Players.TryGetSteamId(playerEntityId);
                    Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), steamUserId);
                }
            });

            Log(msg, 3);
        }
    }
}
