using UnityEngine;

namespace zFramework.TinyRPC
{
    public static class MonoBehaviourEx
    {
        public static void AddMessageHandler(this MonoBehaviour target) => MessageManager.RegisterHandler(target);
        public static void RemoveMessageHandler(this MonoBehaviour target) => MessageManager.UnRegisterHandler(target);
    }
}