using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace zFramework.TinyRPC.Editor
{
    [FilePath("ProjectSettings/TinyRpcEditorSettings.asset")]
    public class TinyRpcEditorSettings : ScriptableSingleton<TinyRpcEditorSettings>
    {
        public List<DefaultAsset> protos;
        public string generatedScriptLocation= "Packages/TinyRPC Generated";
        public bool indentWithTab;
        //消息存 Project 同级目录时允许新增父节点
        //比如：Project/../Common/TinyRPC Generated 
        // Project/../  在这里代表与 Project 同级目录的意思
        // 其中，Common 为新增的父节点
        [Tooltip("为生成的脚本添加自定义引用")]
        public List<AssemblyDefinitionAsset> assemblies;
    }
}

public class ProtoAsset : TextAsset 
{

}