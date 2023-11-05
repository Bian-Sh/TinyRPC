using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Editor
{
    [FilePath("ProjectSettings/TinyRpcEditorSettings.asset")]
    public class TinyRpcEditorSettings : ScriptableSingleton<TinyRpcEditorSettings>
    {
        public List<DefaultAsset> protos;
        public string generatedScriptLocation;
    }
}