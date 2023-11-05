namespace zFramework.TinyRPC.Editor
{
    public class ScriptInfo
    {
        public string name;
        public string content;
        // message save in {proto name}/Message Folder
        // request + response save in {proto name}/Rpc Folder
        //  parent is empty, save in {proto name}/Common Folder
        public ScriptType type;
    }
}