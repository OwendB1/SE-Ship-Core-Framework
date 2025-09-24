using System.Linq;
using ProtoBuf;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    [ProtoInclude(1000, typeof(PacketAction))]
    //[ProtoInclude(2000, typeof(PacketActionResponse))]
    // [ProtoInclude(3000, typeof(PacketStatsRequest))]
    // [ProtoInclude(4000, typeof(PacketStatsSend))]
    // [ProtoInclude(5000, typeof(PacketRequestSettings))]
    // [ProtoInclude(6000, typeof(PacketSendSettings))]
    [ProtoContract]
    internal abstract class PacketBase
    {
        internal PacketBase() { } // Empty constructor required for deserialization
        internal abstract void Received();
    }
    
    [ProtoContract]
    internal class PacketAction : PacketBase
    {
        [ProtoMember(100)]
        internal ButtonAction actionData;
        internal PacketAction() { } // Empty constructor required for deserialization
        internal PacketAction(ButtonAction actionData)
        {
            this.actionData = actionData;
        }
        
        internal override void Received()
        {
            var group = Session.GroupDict[actionData.Group];
            if (actionData.IsBoost)
            {
                group.ActivateBoost();
            }
            else
            {
                group.ActivateDefense();
            }
        }
    }
    
    [ProtoContract]
    internal class PacketMainCore : PacketBase
    {
        [ProtoMember(200)]
        internal CheckboxMainCore data;
        internal PacketMainCore() { } // Empty constructor required for deserialization
        internal PacketMainCore(CheckboxMainCore data)
        {
            this.data = data;
        }
        
        internal override void Received()
        {
            var group = Session.GroupDict[data.Group];
            var core= group.CoreDictionary.FirstOrDefault(kvp => kvp.Key.EntityId == data.BlockId);
        }
    }


    [ProtoContract]
    internal class ButtonAction
    {
        [ProtoMember(1)]
        internal IMyGridGroupData Group;
        [ProtoMember(2)]
        internal bool IsBoost;
    }
    
    [ProtoContract]
    internal class CheckboxMainCore
    {
        [ProtoMember(1)]
        internal IMyGridGroupData Group;
        [ProtoMember(2)]
        internal long BlockId;
    }
}