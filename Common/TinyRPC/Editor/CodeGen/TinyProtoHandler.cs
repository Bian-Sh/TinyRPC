using System.IO;
using System.Text;
using System;
using UnityEditor;
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
                var scriptInfo = ResolveBlockInfo(info, processor);
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

        /// <summary>
        ///  根据消息块信息，生成消息代码
        /// </summary>
        /// <param name="block">分割的消息数据</param>
        /// <returns>消息代码内容</returns>
        public static ScriptInfo ResolveBlockInfo(string block, ProtoContentProcessor processor)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                //use keyword "message" instead of "{" for checking whether inside message
                if (string.IsNullOrEmpty(line) || line == "{")
                {
                    continue;
                }
                if (line.StartsWith("//"))
                {
                    if (line.StartsWith("//ResponseType")) // response type
                    {
                        processor.AddResponseType(line);
                    }
                    else // summary 
                    {
                        processor.MarkSummary(line);
                    }
                    continue;
                }
                // message
                if (line.StartsWith("message"))
                {
                    processor.AddMessageType(line);
                    continue;
                }
                processor.AddMember(line);
            }
            return processor.ToScriptInfo();
        }
    }
}