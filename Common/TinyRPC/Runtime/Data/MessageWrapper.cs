using System;
using UnityEngine;
using static zFramework.TinyRPC.Manager;
using static zFramework.TinyRPC.ObjectPool;
namespace zFramework.TinyRPC.Messages
{
    // 由于jsonutility无法序列化继承关系，所以需要一个包装类
    [Serializable]
    public class MessageWrapper : ISerializationCallbackReceiver, IReusable
    {
        public string type;
        public string data;
        public IMessage Message { get; set; }
        public bool IsRecycled { get; set; }

        public void OnBeforeSerialize()
        {
            type = Message.GetType().Name;
            data = JsonUtility.ToJson(Message);
            // Request 在这里不能回收，call 还需要使用
            if (Message is not IRequest)
            {
                Recycle(Message);
            }
        }

        public void OnAfterDeserialize()
        {
            Type t = GetMessageType(type);
            var obj = Allocate(t);
            JsonUtility.FromJsonOverwrite(data, obj);
            Message = (IMessage)obj;
        }

        public override string ToString()
        {
            var info = nameof(MessageWrapper);
            var fields = GetType().GetFields();
            foreach (var field in fields)
            {
                info += $"{field.Name} = {field.GetValue(this)}\n";
            }
            return info;
        }

        public void OnRecycle()
        {
            IsRecycled = true;
            Message = null;
            type = default;
            data = default;
        }
    }
}
