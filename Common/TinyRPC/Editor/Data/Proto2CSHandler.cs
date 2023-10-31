using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace zFramework.TinyRPC.Editor
{
    public class Proto2CSHandler : EditorWindow
    {
        static string MessagePath;
        static string ProtoPath;
        static string ProtoPathKey = $"{nameof(Proto2CSHandler)}-ProtoPath-Key";
        DefaultAsset asset;
        GUIContent initBt_cnt = new GUIContent("请选择 proto 文件", "请选择用于生成 .cs 实体类的 proto 文件");
        GUIContent updateBt_cnt = new GUIContent("更新", "选择新的 proto 文件，如果此文件在工程外，将会复制到工程内，覆盖原有的 proto 文件");
        GUIContent tips = new GUIContent("操作完成，请等待编译...");
        string notice = @"1. 选择的 .proto 文件不在工程中则拷贝至工程中
2. 拷贝的副本只存在一份，永远执行覆盖操作
3. 选择的 .proto 文件位于工程中则不做上述处理
4.  proto 文件中的语法是基于 proto3 语法的变体（精简版）";
        static EditorWindow window;
        [MenuItem("Tools/.proto 转 .cs 实体类")]
        public static void ShowWindow()
        {
            window = GetWindow(typeof(Proto2CSHandler));
        }
        public void OnEnable()
        {
            MessagePath = $"{Application.dataPath}/TinyRPC/Generated";
            if (!Directory.Exists(MessagePath))
            {
                Directory.CreateDirectory(MessagePath);
            }
            ProtoPath = EditorPrefs.GetString(ProtoPathKey);
            if (!string.IsNullOrEmpty(ProtoPath))
            {
                asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ProtoPath);
            }
            minSize = new Vector2(360, 220);
        }

        private void OnGUI()
        {
            if (!asset)
            {
                //获取当前 editorwindow 宽高
                var rect = EditorGUILayout.GetControlRect();
                rect.height = 48;
                rect.width = 200;
                rect.x = (position.width - rect.width) / 2;
                rect.y = (position.height - rect.height) / 2;
                if (GUI.Button(rect, initBt_cnt))
                {
                    SelectAndLoadProtoFile();
                }
                return;
            }
            GUILayout.Space(15);
            using (new GUILayout.HorizontalScope())
            {
                asset = EditorGUILayout.ObjectField("Proto 文件：", asset, typeof(DefaultAsset), false) as DefaultAsset;
                if (GUILayout.Button(updateBt_cnt, GUILayout.Width(60)))
                {
                    SelectAndLoadProtoFile();
                }
            }
            var relativePath = FileUtil.GetProjectRelativePath(MessagePath);
            // Debug.Log($"{nameof(Proto2CSHandler)}: relative path = {relativePath} ");
            //todo: 判断是否为 只读的 package 文件夹，还是 Editor 内的文件夹，还是 Editor 程序集中的文件夹
            //TODO: 和setting整合 ，使用 tab 页签切换

            GUI.enabled = false;
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
            EditorGUILayout.ObjectField("消息存储路径：", folder, typeof(DefaultAsset), false);
            GUI.enabled = true;
            GUILayout.Space(15);
            var style_helpbox = GUI.skin.GetStyle("HelpBox");
            var size_font = style_helpbox.fontSize;
            style_helpbox.fontSize = 12;
            var content = new GUIContent(notice, EditorGUIUtility.IconContent("console.infoicon").image);
            var height = style_helpbox.CalcHeight(content, EditorGUIUtility.currentViewWidth);
            EditorGUILayout.LabelField(content, style_helpbox, GUILayout.Height(height));
            style_helpbox.fontSize = size_font;
            GUILayout.Space(15);

            var rt = GUILayoutUtility.GetLastRect();
            rt.width = 200;
            rt.height = 48;
            rt.x = (position.width - rt.width) / 2;
            rt.y = position.height - rt.height - 10;
            if (GUI.Button(rt, "生成 .cs 实体类"))
            {
                TryCreateAssemblyDefinitionFile();
                InnerProto2CS.Proto2CS("zFramework.TinyRPC.Generated", asset, MessagePath);
                ShowNotification(tips);
                AssetDatabase.Refresh();
            }
            // 检测ObjectField是否有修改
            if (GUI.changed)
            {
                ProtoPath = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;
                EditorPrefs.SetString(ProtoPathKey, ProtoPath);
            }
        }

        private void SelectAndLoadProtoFile()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("请选择 .proto 文件", Application.dataPath, new string[] { "Protobuf file", "proto" });
            if (!string.IsNullOrEmpty(path))
            {
                ProtoPath = FileUtil.GetProjectRelativePath(path);
                if (string.IsNullOrEmpty(ProtoPath)) //.proto 文件不在工程内，则拷贝到工程中,且覆盖原有的 proto 文件
                {
                    var fileName = Path.GetFileName(path);
                    var destPath = $"{MessagePath}/{fileName}";
                    File.Copy(path, destPath, true);
                    ProtoPath = FileUtil.GetProjectRelativePath(destPath);
                    AssetDatabase.Refresh();
                }
                EditorPrefs.SetString(ProtoPathKey, ProtoPath);
                asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ProtoPath);
            }
        }
        /// <summary>
        /// 为降低反射遍历消息的次数、减小编译时长，故使用 AssemblyDefinition 
        /// </summary>
        private static void TryCreateAssemblyDefinitionFile()
        {
            string file = "com.network.generated.asmdef";
            string content = @"{
    ""name"": ""com.network.generated"",
    ""references"": [
        ""GUID:c5a44f231aee9ef4895a10427e883834""
    ],
    ""autoReferenced"": true
}";
            var path = Path.Combine(MessagePath, file);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Encoding.UTF8);
                Debug.Log($"Assembly Definition File 生成 {file} 成功！");
            }
        }
    }

    public static class InnerProto2CS
    {
        private static readonly char[] splitChars = { ' ', '\t' };

        public static void Proto2CS(string ns, DefaultAsset proto, string outputPath)
        {
            string csPath = Path.Combine(outputPath, $"{proto.name}.cs");
            var protoPath = AssetDatabase.GetAssetPath(proto);
            var lines = File.ReadAllLines(protoPath);
            StringBuilder sb = new();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using zFramework.TinyRPC.Messages;");
            sb.Append("\n");
            sb.Append($"namespace {ns}\n");
            sb.Append("{\n");

            bool isMsgStart = false;
            foreach (string line in lines)
            {
                string newline = line.Trim();

                if (newline == "")
                {
                    continue;
                }

                // 写入序列化特性
                if (newline.StartsWith("//ResponseType") || (newline.StartsWith("message") && !newline.EndsWith("Request")))
                {
                    sb.AppendLine("\t[Serializable]");
                }

                // 写入响应类型
                if (newline.StartsWith("//ResponseType"))
                {
                    string responseType = line.Split(' ')[1].TrimEnd('\r', '\n');
                    sb.AppendLine($"\t[ResponseType(typeof({responseType}))]");
                    continue;
                }

                // 写入注释
                if (newline.StartsWith("//"))
                {
                    sb.Append($"\t/// <summary>\n///{newline.TrimStart('/')}\n/// </summary>\n");
                    continue;
                }

                if (newline.StartsWith("message"))
                {
                    string parentClass = "";
                    isMsgStart = true;
                    string msgName = newline.Split(splitChars, StringSplitOptions.RemoveEmptyEntries)[1];
                    string[] ss = newline.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);

                    if (ss.Length == 2)
                    {
                        parentClass = ss[1].Trim();
                    }
                    sb.Append($"\tpublic partial class {msgName}");
                    if (parentClass == "Message" || parentClass == "Request" || parentClass == "Response")
                    {
                        sb.Append($" : {parentClass}");
                    }
                    else if (parentClass != "")
                    {
                        Debug.LogWarning($"{nameof(InnerProto2CS)}: TinyRPC 消息只能继承自 Message、Request、Response ");
                    }
                    sb.Append("\n");
                    continue;
                }

                if (isMsgStart)
                {
                    if (newline == "{")
                    {
                        sb.Append("\t{\n");
                        continue;
                    }

                    if (newline == "}")
                    {
                        isMsgStart = false;
                        sb.Append("\t}\n\n");
                        continue;
                    }

                    if (newline.StartsWith("//"))
                    {
                        sb.AppendLine(newline);
                        continue;
                    }

                    if (newline != "" && newline != "}")
                    {
                        if (newline.StartsWith("repeated"))
                        {
                            Repeated(sb, ns, newline);
                        }
                        else
                        {
                            Members(sb, newline, true);
                        }
                    }
                }
            }

            sb.Append("}\n");
            using FileStream txt = new FileStream(csPath, FileMode.Create, FileAccess.ReadWrite);
            using StreamWriter sw = new StreamWriter(txt);
            var content = sb.ToString();
            content = content.Replace("\r\n", "\n");
            sw.Write(content);
        }
        private static void Repeated(StringBuilder sb, string ns, string newline)
        {
            try
            {
                int index = newline.IndexOf(";");
                newline = newline.Remove(index);
                string[] ss = newline.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
                string type = ss[1];
                type = ConvertType(type);
                string name = ss[2];

                sb.Append($"\t\tpublic List<{type}> {name} = new ();\n");
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(InnerProto2CS)}: Repeated Error {newline}\n {e}");
            }
        }

        private static string ConvertType(string type)
        {
            string typeCs = "";
            switch (type)
            {
                case "int16":
                    typeCs = "short";
                    break;
                case "int32":
                    typeCs = "int";
                    break;
                case "bytes":
                    typeCs = "byte[]";
                    break;
                case "uint32":
                    typeCs = "uint";
                    break;
                case "long":
                    typeCs = "long";
                    break;
                case "int64":
                    typeCs = "long";
                    break;
                case "uint64":
                    typeCs = "ulong";
                    break;
                case "uint16":
                    typeCs = "ushort";
                    break;
                default:
                    typeCs = type;
                    break;
            }

            return typeCs;
        }

        private static void Members(StringBuilder sb, string newline, bool isRequired)
        {
            try
            {
                int index = newline.IndexOf(";");
                newline = newline.Remove(index);
                string[] ss = newline.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
                string type = ss[0];
                string name = ss[1];
                string typeCs = ConvertType(type);

                sb.Append($"\t\tpublic {typeCs} {name} ;\n");
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(InnerProto2CS)}: Create Members error, line = {newline}\n {e}");
            }
        }
    }
}