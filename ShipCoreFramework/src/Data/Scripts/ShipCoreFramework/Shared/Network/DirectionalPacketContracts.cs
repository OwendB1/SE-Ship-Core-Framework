using System;
using ProtoBuf;

namespace ShipCoreFramework
{
    [ProtoContract]
    internal partial class PacketAction : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        [ProtoMember(100)]
        internal ButtonAction ActionData;

        internal PacketAction() { }
        internal PacketAction(ButtonAction actionData)
        {
            ActionData = actionData;
        }

        internal override void Received()
        {
            ReceiveOnServer();
        }

        partial void ReceiveOnServer();
    }

    [ProtoContract]
    internal partial class PacketSetMainCore : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        [ProtoMember(200)]
        internal SetMainCoreAction ActionData;

        internal PacketSetMainCore() { }
        internal PacketSetMainCore(SetMainCoreAction actionData)
        {
            ActionData = actionData;
        }

        internal override void Received()
        {
            ReceiveOnServer();
        }

        partial void ReceiveOnServer();
    }

    [ProtoContract]
    internal partial class PacketSetMainCoreSync : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(300)]
        internal SetMainCoreAction ActionData;

        internal PacketSetMainCoreSync() { }
        internal PacketSetMainCoreSync(SetMainCoreAction actionData)
        {
            ActionData = actionData;
        }

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal partial class PacketNotify : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(400)] internal string Text;
        [ProtoMember(401)] internal int TimeMs;
        [ProtoMember(402)] internal string Font;

        internal PacketNotify() { }
        internal PacketNotify(string text, int timeMs = 2000, string font = "Red")
        {
            Text = text;
            TimeMs = timeMs;
            Font = font;
        }

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal partial class PacketRequestConfig : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        internal PacketRequestConfig() { }

        internal override void Received()
        {
            ReceiveOnServer();
        }

        partial void ReceiveOnServer();
    }

    [ProtoContract]
    internal partial class PacketSendConfig : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)]
        internal string ConfigXml;

        internal PacketSendConfig() { }
        internal PacketSendConfig(string configXml)
        {
            ConfigXml = configXml;
        }

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal partial class PacketCountdown : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(700)] internal string Key;
        [ProtoMember(701)] internal string Text;
        [ProtoMember(702)] internal int RemainingSeconds;
        [ProtoMember(703)] internal string Font;

        internal PacketCountdown() { }
        internal PacketCountdown(string key, string text, int remainingSeconds, string font = "Red")
        {
            Key = key;
            Text = text;
            RemainingSeconds = remainingSeconds;
            Font = font;
        }

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal sealed partial class PacketMissionScreen : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1100)] internal string Title;
        [ProtoMember(1101)] internal string ObjectivePrefix;
        [ProtoMember(1102)] internal string Objective;
        [ProtoMember(1103)] internal string Body;
        [ProtoMember(1104)] internal string ButtonText;

        internal PacketMissionScreen() { }
        internal PacketMissionScreen(string title, string objectivePrefix, string objective, string body,
            string buttonText)
        {
            Title = title;
            ObjectivePrefix = objectivePrefix;
            Objective = objective;
            Body = body;
            ButtonText = buttonText;
        }

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal sealed partial class PacketRequestRuntimeState : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        internal override void Received()
        {
            ReceiveOnServer();
        }

        partial void ReceiveOnServer();
    }

    [ProtoContract]
    internal sealed partial class PacketRuntimeState : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)] internal int Sequence;
        [ProtoMember(3)] internal GroupRuntimeState[] States = Array.Empty<GroupRuntimeState>();
        [ProtoMember(4)] internal int BatchIndex;
        [ProtoMember(5)] internal int BatchCount;
        [ProtoMember(6)] internal int SnapshotRevision;

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }

    [ProtoContract]
    internal sealed partial class PacketRuntimeStateDelta : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)] internal GroupRuntimeState[] States = Array.Empty<GroupRuntimeState>();

        internal override void Received()
        {
            ReceiveOnClient();
        }

        partial void ReceiveOnClient();
    }
}
