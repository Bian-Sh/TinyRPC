using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Settings
{
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
