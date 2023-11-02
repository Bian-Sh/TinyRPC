using System.IO;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;
public static class TinyProtoHandler
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
                sb.Append($"\t/// <summary>\n\t///{newline.TrimStart('/')}\n\t/// </summary>\n");
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
                    Debug.LogWarning($"{nameof(TinyProtoHandler)}: TinyRPC 消息只能继承自 Message、Request、Response ");
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
            Debug.LogError($"{nameof(TinyProtoHandler)}: Repeated Error {newline}\n {e}");
        }
    }

    private static string ConvertType(string type)
    {
        string typeCs = type switch
        {
            "int16" => "short",
            "int32" => "int",
            "bytes" => "byte[]",
            "uint32" => "uint",
            "long" => "long",
            "int64" => "long",
            "uint64" => "ulong",
            "uint16" => "ushort",
            _ => type,
        };
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
            Debug.LogError($"{nameof(TinyProtoHandler)}: Create Members error, line = {newline}\n {e}");
        }
    }
}
