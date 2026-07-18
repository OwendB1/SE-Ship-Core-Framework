using System;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;

namespace ShipCoreFramework
{
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
}

