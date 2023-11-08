using System.IO;
using System.Text;
using System;
using UnityEngine;
namespace zFramework.TinyRPC.Editor
{
    public static class TinyProtoHandler
    {
        public static void Proto2CS(string protoName,string protoContent, string outputPath)
        {
            protoContent = protoContent.Replace("\r\n", "\n");
            // split into message blocks by }
            var blocks = protoContent.Split('}', StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrEmpty(protoContent) && blocks.Length == 0)
            {
                Debug.LogWarning($"{nameof(TinyProtoHandler)}: proto文件 {protoName} 数据为空 ！\n");
                return;
            }
            var root = Path.Combine(outputPath, protoName);
            var processor = new ProtoContentProcessor();
            foreach (var info in blocks)
            {
                //skip empty block
                if (string.IsNullOrEmpty(info.Trim()))
                {
                    continue;
                }
                var scriptInfo =processor.ResolveBlockInfo(info);
                string csPath = Path.Combine(root, $"{scriptInfo.type}/{scriptInfo.name}.cs");
                var dir = Path.GetDirectoryName(csPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                using FileStream txt = new FileStream(csPath, FileMode.Create, FileAccess.ReadWrite);
                using StreamWriter sw = new StreamWriter(txt, Encoding.UTF8);
                sw.Write(scriptInfo.content);
            }
        }

 
    }
}