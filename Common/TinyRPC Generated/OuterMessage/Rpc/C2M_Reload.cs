﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    [ResponseType(typeof(M2C_Reload))]
    public partial class C2M_Reload : Request
    {
        public string Account;
        public string Password;
    }
}
