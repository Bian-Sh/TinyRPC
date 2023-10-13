using UnityEngine;
using zFramework.TinyRPC.DataModel;

namespace zFramework.TinyRPC
{
    public static class SerializeHelper
    {
        internal static byte[] Serialize(IMessage message)
        {
            // 将消息包装
            var wrapper = new MessageWrapper
            {
                Message = message
            };
            // 使用 JsonUtility 序列化
            var json = JsonUtility.ToJson(wrapper);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        internal static IMessage Deserialize(byte[] bytes)
        {
            // 使用 JsonUtility 反序列化
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var wrapper = JsonUtility.FromJson<MessageWrapper>(json);
            return wrapper.Message;
        }
    }
}
