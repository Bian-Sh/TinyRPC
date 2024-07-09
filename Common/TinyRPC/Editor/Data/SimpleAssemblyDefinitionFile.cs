using System.Collections.Generic;
using System;
namespace zFramework.TinyRPC.Editors
{
    [Serializable]
    public class SimpleAssemblyDefinitionFile
    {
        public string name;
        public List<string> references;
        public bool autoReferenced;
    }
}