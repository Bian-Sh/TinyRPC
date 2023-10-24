using UnityEngine;
namespace zFramework.TinyRPC.Generated
{
    // 在功能完善后，此脚本自动根据 .proto 文件生成，现在是我手写的
    //  After the function is improved, this script is automatically generated according to the .proto file. Currently I wrote it all by myself
    public class MessageHandlerRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void AutoRegistMessageHandler()
        {
            new NormalMessageHandler<TestMessage>();
        }
    }
}