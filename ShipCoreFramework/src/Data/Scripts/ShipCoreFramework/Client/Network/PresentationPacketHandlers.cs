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
        partial void ReceiveOnClient()
        {
            if (Session.IsShuttingDown) return;

            try
            {
                ContentFingerprint = Cap(ContentFingerprint, 64);
                Error = Cap(Error, 512);
                if (Revision < 0)
                {
                    Reject("Config sync rejected: server sent an invalid revision.", true);
                    return;
                }
                if (Revision <= Session.AppliedConfigRevision) return;
                if (!string.IsNullOrWhiteSpace(Error))
                {
                    Reject("Config sync rejected by server: " + Error, false);
                    return;
                }

                var localFingerprint = Session.Config?.ContentFingerprint ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ContentFingerprint) ||
                    !string.Equals(ContentFingerprint, localFingerprint, StringComparison.Ordinal))
                {
                    Reject("Ship Core content-pack mismatch. Server fingerprint " +
                           (ContentFingerprint ?? "<missing>") + ", client fingerprint " +
                           (localFingerprint.Length == 0 ? "<missing>" : localFingerprint) +
                           ". Ensure the client and server use identical content-pack versions, then reconnect.",
                        false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ConfigXml))
                {
                    Reject("Config sync rejected: received an empty configuration payload.", true);
                    return;
                }
                if (ConfigXml.Length > MaxConfigCharacters)
                {
                    Reject("Config sync rejected: configuration payload exceeded the size limit.", false);
                    return;
                }

                if (Session.Config == null)
                {
                    var loadedConfig = new ModConfig();
                    loadedConfig.LoadConfig(false);
                    Session.Config = loadedConfig;
                }

                ModConfig import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(ConfigXml);
                if (import == null)
                {
                    Reject("Config sync rejected: configuration payload could not be deserialized.", true);
                    return;
                }
                Session.Config.ApplyWorldSettingsFrom(import);

                Session.Config.EnsurePersistedWorldSettings();
                if (!Session.Config.ResolveSelectedNoCore())
                {
                    Reject(Session.Config.GetNoCoreConfigurationError(), false);
                    return;
                }

                Session.ApplyConfigToDefinitions();
                ModAPI.MarkConfigReady(true);
                var runtimeWasInitialized = Session.RuntimeInitialized;
                if (!Session.TryInitializeRuntime() && runtimeWasInitialized)
                    Session.RefreshGroupsAfterConfigChanged();
                Session.RequestRuntimeState();
                Session.CompleteConfigSync(Revision);
                ModAPI.BroadcastConfigReceived();
            }
            catch (Exception exception)
            {
                Utils.Log($"Config sync failed: {exception}", 2, "Config Sync");
                Reject("Config sync failed while applying the server payload: " + exception.Message, true);
            }
        }

        private void Reject(string error, bool retry)
        {
            Session.RejectConfigSync(Revision, error, retry);
        }
    }
}
