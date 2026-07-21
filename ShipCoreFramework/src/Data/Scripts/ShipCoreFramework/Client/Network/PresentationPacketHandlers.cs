using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;

namespace ShipCoreFramework
{
    internal partial class PacketSetMainCoreSync
    {
        partial void ReceiveOnClient()
        {
            GroupComponent group;
            if (!Utils.TryFindByGridId(ActionData.CubegridEntityId, out group)) return;

            MyEntity entity;
            if (!MyEntities.TryGetEntityById(ActionData.BlockEntityId, out entity)) return;

            MyCubeBlock block = entity as MyCubeBlock;
            if (block == null) return;

            foreach (var pair in group.CoreDictionary)
            {
                bool isMain = pair.Key == block;
                pair.Value.IsMainCore = isMain;
                if (isMain) group.MainCoreComponent = pair.Value;
            }
        }
    }

    internal partial class PacketNotify
    {
        partial void ReceiveOnClient()
        {
            Text = Cap(Text, 2048);

            if (IsDebugLog)
            {
                LogPriority = Math.Max(0, Math.Min(LogPriority, 3));
                LogTooltip = Cap(LogTooltip, 64);
                Utils.DisplayClientLogMessage(Text, LogPriority, "Server " + LogTooltip);
                return;
            }

            Font = Cap(Font, 32);
            TimeMs = Math.Max(0, Math.Min(TimeMs, 60000));
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                NotificationInstance.ShowNotification(Text, TimeMs, Font));
        }
    }

    internal partial class PacketCountdown
    {
        partial void ReceiveOnClient()
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

    internal sealed partial class PacketMissionScreen
    {
        partial void ReceiveOnClient()
        {
            Title = Cap(Title, 128);
            ObjectivePrefix = Cap(ObjectivePrefix, 128);
            Objective = Cap(Objective, 128);
            Body = Cap(Body, 128 * 1024);
            ButtonText = Cap(ButtonText, 64);
            MyAPIGateway.Utilities.ShowMissionScreen(Title, ObjectivePrefix, Objective, Body, null, ButtonText);
        }
    }

    internal partial class PacketSendConfig
    {
        private const int MaxConfigCharacters = 1024 * 1024;

        partial void ReceiveOnClient()
        {
            if (Session.IsShuttingDown) return;

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

                ModConfig import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(ConfigXml);
                if (import != null)
                    Session.Config.ApplyWorldSettingsFrom(import);

                Session.Config.EnsurePersistedWorldSettings();
                Session.Config.ResolveSelectedNoCore();
                Session.ApplyConfigToDefinitions();
                Session.RefreshGroupsAfterConfigChanged();
                ModAPI.BroadcastConfigReceived();
                Session.RequestRuntimeState();
            }
            catch (Exception exception)
            {
                Utils.Log($"Config sync failed: {exception}", 2, "Config Sync");
            }
        }
    }
}
