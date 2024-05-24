using System;
using UnityEngine;
using zFramework.TinyRPC.Messages;
using static zFramework.TinyRPC.ObjectPool;

namespace zFramework.TinyRPC
{
    public static class SerializeHelper
    {
        public static Func<byte[], byte[]> Encrypt;
        public static Func<byte[], byte[]> Decrypt;
        internal static byte[] Serialize(IMessage message)
        {
            // MessageWrapper 池化
            var wrapper = Allocate<MessageWrapper>();
            // 将消息包装
            wrapper.Message = message;
            // 使用 JsonUtility 序列化
            var json = JsonUtility.ToJson(wrapper);
            Recycle(wrapper);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            // 加密
            if (null != Encrypt)
            {
                bytes = Encrypt(bytes);
            }
            return bytes;
        }
        internal static IMessage Deserialize(byte[] bytes)
        {
            // 解密
            if (null != Decrypt)
            {
                bytes = Decrypt(bytes);
            }
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            // MessageWrapper 池化
            var wrapper = Allocate<MessageWrapper>();
            //具体的网络消息 池化，框架内的 Ping 由框架回收，框架外的消息需要用户手动回收
            wrapper.Message = Allocate<IMessage>();
            // 使用 JsonUtility 反序列化
            JsonUtility.FromJsonOverwrite(json, wrapper);
            var message = wrapper.Message;
            Recycle(wrapper);
            return message;
        }
    }
}
