using System.IO;
using System.Text;
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
namespace zFramework.TinyRPC.Editors
{
    //todo: 代码生成相关
    //1. .proto 文件应该与生成的脚本放一起，方便 git 等源代码管理
    //2. 通过 list 的 “+” 弹窗输入文件名的形式添加 proto 文件而不是选择文件
    //3. 鉴于第三条，支持自动扫描并添加 step1 提到的目录下的所有 .proto 文件
    //4. 生成界面会扫描并显示需要用户指定 assembly definition file 和 namespace 的类型并提示用户补充
    //5. 过程中如果发生异常，会立刻停止并提示错误信息，只有无异常情况下才会生成代码，避免这方面导致的编译错误
    //6. 看有没有可能单独的为 proto 文件生成代码的代码进行编译并添加 dll 到项目中（Library/Assemblies）

    public static class TinyProtoHandler
    {
        public static void Proto2CS(string protoName, string protoContent, string outputPath,TinyRpcEditorSettings settings)
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
            var processor = new ProtoContentProcessor(protoName, settings);
            foreach (var info in blocks)
            {
                //skip empty block
                if (string.IsNullOrEmpty(info.Trim()))
                {
                    continue;
                }
                var scriptInfo = processor.ResolveBlockInfo(info);
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

        //添加 .proto 文件到 IDE 资源管理器，双击可使用 Unity 默认的 IDE 打开
        [InitializeOnLoadMethod]
        static void MarkProtoFileAsAdditionalExtension()
        {
            var extensions = new List<string>(EditorSettings.projectGenerationUserExtensions);
            if (!extensions.Contains("proto"))
            {
                extensions.Add("proto");
                EditorSettings.projectGenerationUserExtensions = extensions.ToArray();
                Debug.Log($"{nameof(TinyProtoHandler)}: 添加 \".proto\" 文件到 ide 编辑器资源管理器，双击 .proto 可以直接使用 IDE 编辑。");
            }
        }
    }
}