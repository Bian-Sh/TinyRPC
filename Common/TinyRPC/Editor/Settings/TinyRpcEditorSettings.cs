using System.Collections.Generic;
using UnityEditor;
namespace zFramework.TinyRPC.Editor
{
    [FilePath("ProjectSettings/TinyRpcEditorSettings.asset")]
    public class TinyRpcEditorSettings : ScriptableSingleton<TinyRpcEditorSettings>
    {
        public List<DefaultAsset> protos;
        public string generatedScriptLocation;
        public bool indentWithTab;
    }
}