using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Settings
{
    //todo：过滤 log 的 message
    //todo：选择 message 自动生成 messagehandler 方便用户使用
    //todo：绘制 assembly name 为 Assembly Asset ，方便定位，默认的 Assembly-CSharp 搞成只读
    //todo：收集并展示 RPC 消息对，提供 filter 查询
    //todo：大部分设置是运行时使用，需要提供 SettingProvider 也需要 Merge 到runtime
    //todo：为规范使用，绝大部分情况下约定网络消息必须是成对出现。

    public class TinyRpcSettings : ScriptableObject
    {
        //specific from which assembly you can collect handlers
        public string[] AssemblyNames = new string[] {"Assembly-CSharp" };

        private void Awake()
        {
            _instance = this;
        }

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

    }
}
