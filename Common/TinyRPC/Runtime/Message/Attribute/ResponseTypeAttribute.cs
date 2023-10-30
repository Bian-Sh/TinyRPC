using System;

namespace zFramework.TinyRPC
{
    /// <summary>
    /// 为一个消息类标记一个响应类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ResponseTypeAttribute : Attribute
    {
        public Type Type { get; set; }
        public ResponseTypeAttribute(Type type)
        {
            Type = type;
        }
    }
}
