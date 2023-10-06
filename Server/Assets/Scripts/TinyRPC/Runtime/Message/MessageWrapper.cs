using System;
using UnityEngine;
namespace zFramework.TinyRPC.DataModel
{
    // 由于jsonutility无法序列化继承关系，所以需要一个包装类
    [Serializable]
    public class MessageWrapper : ISerializationCallbackReceiver
    {
        public string type;
        public string data;
        public Message Message { get; set; }
        public void OnBeforeSerialize()
        {
            type = Message.GetType().FullName;
            data = JsonUtility.ToJson(Message);
        }

        public void OnAfterDeserialize()
        {
            Type t = Type.GetType(type);
            Message = (Message)JsonUtility.FromJson(data, t);
        }
    }
}
