using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal class Comms
    {
        private readonly ushort _commsId;
        public Comms(ushort id)
        {
            _commsId = id;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_commsId, MessageHandler);
        }

        public void Discard()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, MessageHandler);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, MessageHandler);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, MessageHandler);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, MessageHandler);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, MessageHandler);
        }

        public void ChangeGridClass(long entityId, long gridClassId)
        {
            if (!Constants.IsClient) return;
            try
            {
                var messageData = MyAPIGateway.Utilities.SerializeToBinary(new GridMessage
                    { EntityId = entityId, GridClassId = gridClassId });
                var message = MyAPIGateway.Utilities.SerializeToBinary(new Message
                    { Type = MessageType.ChangeGridClass, Data = messageData });
                MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, message);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::SendChangeGridClassMessage error", 3);
                Utils.LogException(e);
            }
        }

        public void RequestConfig()
        {
            if (!Constants.IsClient) return;
            try
            {
                var messageData = MyAPIGateway.Utilities.SerializeToBinary(MyAPIGateway.Multiplayer.MyId);
                var message = MyAPIGateway.Utilities.SerializeToBinary(new Message
                    { Type = MessageType.RequestConfig, Data = messageData });
                MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, message);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::SendChangeGridClassMessage error", 3);
                Utils.LogException(e);
            }
        }

        private void MessageHandler(ushort handlerId, byte[] data, ulong playerId, bool unknown)
        {
            Utils.Log($"Comms::MessageHandler received message length = {data.Length}", 1);

            Message message;
            try
            {
                message = MyAPIGateway.Utilities.SerializeFromBinary<Message>(data);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::MessageHandler: deserialise message error", 3);
                Utils.LogException(e);
                return;
            }

            Utils.Log("Comms::MessageHandler: deserialised message", 1);

            switch (message.Type)
            {
                case MessageType.ChangeGridClass:
                    HandleChangeGridClass(message.Data);
                    break;
                case MessageType.RequestConfig:
                    HandleRequestConfig(message.Data);
                    break;
                case MessageType.Config:
                    HandleConfig(message.Data);
                    break;
                default:
                    Utils.Log("Comms::MessageHandler: Unknown message type", 2);
                    break;
            }
        }

        private void HandleRequestConfig(byte[] data)
        {
            ulong recipient;
            try
            {
                recipient = MyAPIGateway.Utilities.SerializeFromBinary<ulong>(data);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::HandleRequestConfig: deserialize message error", 3);
                Utils.LogException(e);
                return;
            }
            var configData = MyAPIGateway.Utilities.SerializeToBinary(ModSessionManager.Config);
            var message = MyAPIGateway.Utilities.SerializeToBinary(new Message
                { Type = MessageType.Config, Data = configData });
            MyAPIGateway.Multiplayer.SendMessageTo(_commsId, message, recipient);
        }

        private void HandleConfig(byte[] data)
        {
            Utils.Log($"Comms::HandleConfig: Received config in bytearray with length of {data.Length}");
            try
            {
                var config = MyAPIGateway.Utilities.SerializeFromBinary<ModConfig>(data);
                if (config.GridClasses.Length < 1) return;
                ModSessionManager.LoadConfig(config);
                ModConfig.SaveConfig(config, "REMOTE" + Constants.ConfigFilename);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::HandleConfig: deserialize message error", 3);
                Utils.LogException(e);
            }
        }

        private void HandleChangeGridClass(byte[] data)
        {
            GridMessage message;
            try
            {
                message = MyAPIGateway.Utilities.SerializeFromBinary<GridMessage>(data);
            }
            catch (Exception e)
            {
                Utils.Log("Comms::HandleChangeGridClassMessage: deserialize message error", 3);
                Utils.LogException(e);
                return;
            }

            try
            {
                var entity = MyAPIGateway.Entities.GetEntityById(message.EntityId);
                if (entity == null) return;
                var cubeGrid = entity as IMyCubeGrid;
                var gridLogic = cubeGrid?.GetMainGridLogic();
                if (gridLogic == null) return;
                if (ModSessionManager.Config.IsValidGridClassId(message.GridClassId))
                {
                    Utils.Log($"Comms::HandleChangeGridClassMessage: Setting grid class id for {message.EntityId} to {message.GridClassId}", 1);
                    gridLogic.GridClassId = message.GridClassId;
                    if (!Constants.IsServer || gridLogic.GridClassId != message.GridClassId) return;
                    Utils.Log("Comms::HandleChangeGridClassMessage: Sending change to others");
                    var msg = MyAPIGateway.Utilities.SerializeToBinary(new Message
                        { Type = MessageType.ChangeGridClass, Data = data });
                    MyAPIGateway.Multiplayer.SendMessageToOthers(_commsId, msg);
                }
                else Utils.Log($"Comms::HandleChangeGridClassMessage: Unknown grid class ID {message.GridClassId}", 3);
            }
            catch (Exception e)
            {
                Utils.LogException(e);
            }
        }
    }

    internal enum MessageType
    {
        ChangeGridClass,
        RequestConfig,
        Config
    }

    [ProtoContract]
    internal struct Message
    {
        [ProtoMember(1)] public MessageType Type;
        [ProtoMember(2)] public byte[] Data;
    }

    [ProtoContract]
    internal struct GridMessage
    {
        [ProtoMember(1)] public long EntityId;
        [ProtoMember(2)] public long GridClassId;
    }
}