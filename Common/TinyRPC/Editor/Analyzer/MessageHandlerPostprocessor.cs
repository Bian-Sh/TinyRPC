using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using zFramework.TinyRPC.Settings;

/*
 * todo:代码分析器
 * 1. 实现对 IMessage 部分字段的意外使用的判断，例如不允许用户修改 Rid 、error
 * 2. 实现对 MessageHandlerAttribute 标注的函数/类型的检测，需要使用静态类型
 * 3. RPC 消息处理器不允许存在多个，重复则提示错误
 * 4. 如果类型中存在 MessageHandlerAttribute,则该类型必须被冠以 MessageHandlerProviderAttribute
 */

namespace zFramework.TinyRPC.Editors
{
    /// <summary>
    /// 消息处理器后处理器,用于自动检测程序集是否包含 MessageHandler
    /// 如果存在，则将其程序集名称添加到 TinyRpcSettings 中
    /// 以便在运行时自动从指定的程序集加载，而不需要遍历所有程序集
    /// </summary>
    public class MessageHandlerPostprocessor : AssetPostprocessor
    {
        static bool cachedState;
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            cachedState = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = TinyRpcSettings.Instance.logEnabled;
            var scripts = importedAssets.Where(asset => asset.EndsWith(".cs"))
               .Where(path => !Path.GetFullPath(path).Contains("PackageCache")) // can not be internal solid(readonly) package
               .Where(path => !path.Replace("\\", "/").Contains("/Editor/"));  // can not be editor script
            if (scripts.Count() == 0)
            {
                Debug.unityLogger.logEnabled = cachedState;
                return;
            }
            foreach (var asset in scripts)
            {
                // 获取 MonoScript 对象
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(asset);
                // 获取脚本类型
                if (monoScript == null)
                {
                    Debug.Log($"{nameof(MessageHandlerPostprocessor)}: {asset} 不是脚本，不处理！");
                    Debug.unityLogger.logEnabled = cachedState;
                    continue;
                }
                var type = monoScript.GetClass();
                // 获取脚本所属程序集
                var assembly = type.Assembly;
                if (assembly == null)
                {
                    Debug.Log($"{nameof(MessageHandlerPostprocessor)}: {type.Name} 所属程序集为空，不处理！");
                    Debug.unityLogger.logEnabled = cachedState;
                    continue;
                }
                var assemblyName = assembly.GetName().Name;
                var filter = $"{assemblyName} t:asmdef ";
                var assemblyDefinition = AssetDatabase.FindAssets(filter);
                var isEditorAssembly = false;
                if (assemblyDefinition.Length > 0)
                {
                    var assemblyDefinitionPath = AssetDatabase.GUIDToAssetPath(assemblyDefinition[0]);
                    var assemblyDefinitionObj = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assemblyDefinitionPath);
                    if (assemblyDefinitionObj != null)
                    {
                        var converted = JsonUtility.FromJson<AssemblyInfo>(assemblyDefinitionObj.text);
                        isEditorAssembly = converted.includePlatforms.Length == 1 &&
                                                               converted.includePlatforms.Contains("Editor") &&
                                                               converted.excludePlatforms.Length == 0;
                    }
                }
                if (isEditorAssembly)
                {
                    Debug.Log($"{nameof(MessageHandlerPostprocessor)}: {assemblyName} 是编辑器程序集，不处理！");
                    Debug.unityLogger.logEnabled = cachedState;
                    continue;
                }

                var list = TinyRpcSettings.Instance.assemblyNames;
                Debug.Log($"{nameof(MessageHandlerPostprocessor)}: assemblyName {assemblyName} - {type.Name}");
                // 如果有存储过，或者用户声明不扫描，则不处理
                // 约定：如果以 ! 开头，则表示不扫描
                var exist = list.Any(item => item == assemblyName || item == $"!{assemblyName}");
                if (exist)
                {
                    continue;
                }

                // 如果此程序集名称不存在于 TinyRpcSettings 中，则分析此程序集是否包含 MessageHandler
                Debug.Log($"{nameof(MessageHandlerPostprocessor)}: 正在分析{assemblyName}.{type.Name}是否包含 MessageHandler");
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var hasMessageHandler = methods.Any(method => method.GetCustomAttribute<MessageHandlerAttribute>() != null);
                // 如果包含 MessageHandler，则添加到 TinyRpcSettings 中
                if (hasMessageHandler)
                {
                    Debug.Log($"{nameof(MessageHandlerPostprocessor)}: 检测到 {assemblyName}.{type.Name} 包含 MessageHandler，已添加到 TinyRpcSettings 中");
                    TinyRpcSettings.Instance.assemblyNames.Add(assemblyName);
                    TinyRpcSettings.Instance.Save();
                }
            }
            Debug.unityLogger.logEnabled = cachedState;
        }

        [Serializable]
        class AssemblyInfo
        {
            public string name;
            public string[] includePlatforms;
            public string[] excludePlatforms;
        }
    }

}