using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using zFramework.TinyRPC.Settings;

namespace zFramework.TinyRPC.Editors
{
    [FilePath("ProjectSettings/TinyRpcEditorSettings.asset")]
    public class TinyRpcEditorSettings : ScriptableSingleton<TinyRpcEditorSettings>
    {
        public const string AsmdefName = "com.zframework.tinyrpc.generated.asmdef";
        /// <summary>
        ///  .proto 文件存放目录
        /// </summary>
        public const string ProtoFileContainer = "Proto Files";
        /// <summary>
        ///  .proto 文件列表
        /// </summary>
        public List<ProtoAsset> protos;
        public LocationType currentLocationType = LocationType.Packages;
        public string generatedScriptLocation = "Packages/TinyRPC Generated";
        public bool indentWithTab;
        //消息存 Project 同级目录时允许新增父节点
        //比如：Project/../Common/TinyRPC Generated 
        // Project/../  在这里代表与 Project 同级目录的意思
        // 其中，Common 为新增的父节点
        [Tooltip("为生成的脚本添加自定义引用")]
        public List<AssemblyDefinitionAsset> assemblies;
        [Tooltip("将该列表中出现的 Common 消息生成 partial class 类型而不是默认的 struct 类型, 如果出现 \"*\" 则全部生成 partial class 类型...")]
        public List<string> generateAsPartialClass;

        public string GetProtoFileContianerPath()
        {
            string path;
            if (currentLocationType == LocationType.Assets)
            {
                path = FileUtil.GetProjectRelativePath(generatedScriptLocation);
                path =  path + "/" + ProtoFileContainer;
            }
            else
            {
                path = $"Packages/{AsmdefName[..^7]}/{ProtoFileContainer}";
            }
            return path;
        }
    }
}

