using System;
using UnityEngine;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    public class Message:IMessage
    {
        /// <summary>
        /// 消息 id , 系统中自增，用于消息身份识别
        /// </summary>
        public int id;

        /// <inheritdoc/>
        public int Id { get => id; set => id = value; }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
