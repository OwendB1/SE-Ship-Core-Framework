using System;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage;

namespace NexusModAPI
{
    internal class NexusAPI
    {
        private const long MessageId = 20240902;

        public byte CurrentServerID { get; private set; }
        public bool Enabled { get; private set; }

        private Action _onEnabled;
        private Func<object, object> _sendModMsgToServer;
        private Func<object, object> _sendModMsgToAllServers;

        public NexusAPI(Action onEnabled = null)
        {
            _onEnabled = onEnabled;
            MyAPIGateway.Utilities.RegisterMessageHandler(MessageId, ReceiveData);
        }

        public void Unload()
        {
            Enabled = false;
            _sendModMsgToServer = null;
            _sendModMsgToAllServers = null;
            _onEnabled = null;
            MyAPIGateway.Utilities.UnregisterMessageHandler(MessageId, ReceiveData);
        }

        public bool SendModMsgToServer(byte[] data, long modChannelId, byte targetServer)
        {
            return Enabled && _sendModMsgToServer != null &&
                   (bool)_sendModMsgToServer(MyTuple.Create(data, modChannelId, targetServer));
        }

        public bool SendModMsgToAllServers(byte[] data, long modChannelId)
        {
            return Enabled && _sendModMsgToAllServers != null &&
                   (bool)_sendModMsgToAllServers(MyTuple.Create(data, modChannelId));
        }

        private void ReceiveData(object obj)
        {
            try
            {
                var data = (MyTuple<byte[], Func<int, Func<object, object>>>)obj;
                var getMethod = data.Item2;

                _sendModMsgToServer = getMethod((int)Methods.SendModMsgToServer);
                _sendModMsgToAllServers = getMethod((int)Methods.SendModMsgToAllServers);

                var serverData = MyAPIGateway.Utilities.SerializeFromBinary<ServerDataMsgAPI>(data.Item1);
                CurrentServerID = serverData.ThisServerID;
                Enabled = true;

                if (_onEnabled != null) _onEnabled();
            }
            catch
            {
            }
        }

        private enum Methods
        {
            SendModMsgToServer = 5,
            SendModMsgToAllServers = 6
        }
        
        [ProtoContract]
        private class ServerDataMsgAPI
        {
            [ProtoMember(40)]
            internal byte ThisServerID;
        }

        [ProtoContract]
        internal class ModAPIMsg
        {
            [ProtoMember(10)]
            internal byte fromServerID;

            [ProtoMember(20)]
            internal byte toServerID;

            [ProtoMember(25)]
            internal long targetModMessageID;

            [ProtoMember(30)]
            internal byte[] msgData;
        }
    }
}
