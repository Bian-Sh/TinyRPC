using System;
using UnityEngine;
using static zFramework.TinyRPC.Manager;
using static zFramework.TinyRPC.ObjectPool;
namespace zFramework.TinyRPC.Messages
{
    // JsonUtility 可以序列化/反序列化继承关系的类，但由于其序列化/反序列化必须要提供具体的类型
    // 因此需要此一个包装类，记录消息类型和消息内容
    [Serializable]
    public class MessageWrapper : ISerializationCallbackReceiver, IReusable
    {
        public string type;
        public string data;
        public IMessage Message { get; set; }
        bool IReusable.RequireRecycle { get; set; }
        void IDisposable.Dispose() => Recycle(this);
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
            Message = null;
            type = default;
            data = default;
        }
    }
}
