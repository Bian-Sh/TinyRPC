﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    public partial class TestMessage : Message
    {
        /// <summary>
        ///  消息内容
        /// </summary>
        public string message;
        /// <summary>
        ///  年龄（fake info）
        /// </summary>
        public int age;
        public override void OnRecycle()
        {
            base.OnRecycle();
            message = "";
            age = 0;
        }
    }
}
