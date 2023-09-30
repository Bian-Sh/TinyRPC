using System;

namespace zFramework.TinyRPC.DataModel
{
    [Serializable]
    [ResponseType(typeof(Response))]
    public class Request : Message
    {
        /// <summary>
        ///  用户有能力设置超时时间，以根据不同的事务强度设置等待时间，单位毫秒
        /// </summary>
        public int timeout = 5000;
    }
}
