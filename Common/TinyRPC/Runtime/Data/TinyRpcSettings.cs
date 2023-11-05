using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Settings
{
    //todo：绘制 assembly name 为 Assembly Asset ，方便定位，默认的 Assembly-CSharp 搞成只读
    //todo：收集并展示 RPC 消息对，提供 filter 查询
    //todo：大部分设置是运行时使用，需要提供 SettingProvider 也需要 Merge 到runtime
    // 为规范使用，绝大部分情况下约定网络消息必须是成对出现。
    public class TinyRpcSettings : ScriptableObject
    {
        //specific from which assembly you can collect handlers
        public List<string> assemblyNames = new() { "Assembly-CSharp" };

        //filter log message, default is Ping (must use full type name here)
        public List<string> logFilters = new() { "zFramework.TinyRPC.Ping" };

        [Header("心跳包发送频率 （单位：毫秒）")]
        public int pingInterval = 1000;

        [Header("心跳包超时重试次数")]
        public int pingRetry = 3;

        [Header("RPC 最小超时 （单位：毫秒）"), Tooltip("用户 Response 设定的值过小时，以此设定值为准！ Ping 消息也受此影响~")]
        public int rpcTimeout = 5000;

        [Header("开启log (应用于 PostProcessor)")]
        public bool logEnabled;


        private void Awake() => _instance = this;

        // singleton instance, create and save to "Assets/TinyRPC/Resouces"
        private static TinyRpcSettings _instance;

        public static TinyRpcSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TinyRpcSettings>("TinyRpcSettings");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        _instance = CreateInstance<TinyRpcSettings>();
                        AssetDatabase.CreateFolder("Assets", "TinyRPC");
                        AssetDatabase.CreateFolder("Assets/TinyRPC", "Resources");
                        AssetDatabase.CreateAsset(_instance, "Assets/TinyRPC/Resources/TinyRpcSettings.asset");
                        AssetDatabase.SaveAssets();
                    }
#endif
                }
                return _instance;
            }
        }
#if UNITY_EDITOR
        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OnValidate()
        {
            // validate pingInterval, can not less then 500 ms
            if (pingInterval < 500)
            {
                pingInterval = 1000;
                Debug.LogError($"{nameof(TinyRpcSettings)}: 心跳包发送请不要设置的过于频繁！");
            }
            // validate pingRetry, can not less then 0
            if (pingRetry < 0) 
            {
                pingRetry = 3;
                Debug.LogError($"{nameof(TinyRpcSettings)}: 心跳包重试次数不能取负值！");
            }
            // validate rpcTimeout, can not less then 5000 ms
            // beware: rpcTimeout is the minimum value of response timeout
            // if user set response timeout less then rpcTimeout, use rpcTimeout instead
            // but if user set response timeout greater then rpcTimeout, use user's setting
            if (rpcTimeout < 1000)
            {
                rpcTimeout = 1000;
                Debug.LogError($"{nameof(TinyRpcSettings)}: RPC 超时时间不能小于 1000 ms！");
            }
        }

#endif
    }
}
