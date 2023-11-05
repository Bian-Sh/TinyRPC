using System.IO;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class TinyProtoHandler
{
    private static readonly char[] splitChars = { ' ', '\t' };
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

    public static void Proto2CS(string ns, DefaultAsset proto, string outputPath)
    {
        var protoPath = AssetDatabase.GetAssetPath(proto);
        var protoContent = File.ReadAllText(protoPath);
        protoContent = protoContent.Replace("\r\n", "\n");

        // split into message blocks by }
        var blocks = protoContent.Split('}', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrEmpty(protoContent) && blocks.Length == 0)
        {
            Debug.LogWarning($"{nameof(TinyProtoHandler)}: proto文件数据为空 ！\n");
            return;
        }
        var root = Path.Combine(outputPath, proto.name);
        foreach (var info in blocks)
        {
            //log info
            Debug.Log($"{nameof(TinyProtoHandler)}: block = {info}");
            //skip empty block
            if (string.IsNullOrEmpty(info.Trim()))
            {
                Debug.Log($"{nameof(TinyProtoHandler)}: skipd {info} ！");
                continue;
            }
            var scriptInfo = ResolveBlockInfo(info, ns);
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
    public static ScriptInfo ResolveBlockInfo(string block, string ns)
    {
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var script = new StringBuilder();
        /*
         对 Lines 进行处理，
         从上而下，依次是：注释（按需）、Attribute，消息名、继承、成员
         Attribute ：Serializable（必选）+ ResponseType(按需)
         消息内容：
         // 注释aa
         //ResponseType TestRPCResponse
         message TestRPCRequest // Request
         {
         	string name ;
             repeated int32 age ;
             CustomType customType ;
         }
         */
        //1. using 
        script.AppendLine("using System;");
        script.AppendLine("using zFramework.TinyRPC.Messages;");
        script.Append("\n");
        // 2. namespace
        script.Append($"namespace {ns}\n");
        script.Append("{\n");

        // 4. message
        bool isInsideMessage = false;
        bool hasRepeatedField = false;
        string parentClass = "";
        string msgName = "";
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "{")
            {
                isInsideMessage = true;
                script.AppendLine("\t{");
                continue;
            }
            // 写入序列化特性
            if (line.StartsWith("//ResponseType") || (line.StartsWith("message") && !line.EndsWith("Request")))
            {
                script.AppendLine("\t[Serializable]");
            }
            if (line.StartsWith("//"))
            {
                if (line.StartsWith("//ResponseType")) // response type
                {
                    string responseType = line.Split(' ')[1].TrimEnd('\r', '\n');
                    script.AppendLine($"\t[ResponseType(typeof({responseType}))]");
                }
                else // summary ,do not surport multi line and summary mix responsetype
                {
                    var indent = isInsideMessage ? "\t\t" : "\t";
                    script.Append($"{indent}/// <summary>\n{indent}///{line.TrimStart('/')}\n{indent}/// </summary>\n");
                }
                continue;
            }


            if (line.StartsWith("message"))
            {
                msgName = line.Split(splitChars, StringSplitOptions.RemoveEmptyEntries)[1];
                script.Append($"\tpublic partial class {msgName}");
                string[] ss = line.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
                parentClass = ss.Length == 2 ? ss[1].Trim() : "";
                if (parentClass == "Message" || parentClass == "Request" || parentClass == "Response")
                {
                    script.Append($" : {parentClass}");
                }
                else if (parentClass != "")
                {
                    Debug.LogWarning($"{nameof(TinyProtoHandler)}: TinyRPC 消息只能继承自 Message、Request、Response ");
                }
                script.AppendLine();
                continue;
            }


            if (isInsideMessage)
            {
                if (line.StartsWith("repeated"))
                {
                    hasRepeatedField = true;
                    Repeated(script, ns, line);
                }
                else
                {
                    Members(script, line, true);
                }
            }
        }
        // insert using Collections for repeated field with hard code
        if (hasRepeatedField)
        {
            script.Insert(13, "\nusing System.Collections.Generic;");
        }
        script.AppendLine("\t}");
        script.AppendLine("}");
        var content = script.ToString();
        content = content.Replace("\r\n", "\n");
        var scriptInfo = new ScriptInfo
        {
            name = msgName,
            content = content,
            type = parentClass switch
            {
                "Message" => ScriptType.Message,
                "Request" => ScriptType.Rpc,
                "Response" => ScriptType.Rpc,
                _ => ScriptType.Common,
            }
        };
        return scriptInfo;
    }
}

public class ScriptInfo
{
    public string name;
    public string content;
    // message save in {proto name}/Message Folder
    // request + response save in {proto name}/Rpc Folder
    //  parent is empty, save in {proto name}/Common Folder
    public ScriptType type;
}
public enum ScriptType
{
    Message,
    Rpc,
    Common,
}
public class ProtoContentProcessor
{
    StringBuilder sb;
    bool isInsideMessage = false; // start with "{"
    bool hasRepeatedField = false; //any repeated keywords appear.
    bool hasSerializableMarked = false; // only one
    bool hasResponseTypeMarked = false; // only onece
    bool hasSummary = false; // only onece needed

    public string parentClass;
    public string msgName;
    readonly Dictionary<int, string> summarys = new();
    readonly char[] splitChars = { ' ', '\t' };
    const string NAMESPACE = "zFramework.TinyRPC.Generated";
    public ProtoContentProcessor()
    {
        this.sb = new StringBuilder();
        AddUsingAndNamespace();
    }

    // 0. using
    public void AddUsingAndNamespace()
    {
        sb.AppendLine("using System;");
        sb.AppendLine("using zFramework.TinyRPC.Messages;");
        sb.AppendLine($"namespace {NAMESPACE}");
        sb.AppendLine("{");
    }

    //1. handle SerializableAttribute, call inside procerssor 
    public void AddSerializable()
    {
        if (!hasSerializableMarked)
        {
            sb.AppendLine("\t[Serializable]");
            hasSerializableMarked = true;
        }
    }

    //2. handle custom summary 
    // try record summary line and it's index
    // so that we can determine how many lines to insert
    // and wether has mix with ResponseType attribute
    public void MarkSummary(int index, string line)
    {
        if (hasSummary)
        {
            return;
        }
        if (!isInsideMessage && hasResponseTypeMarked)
        {
            throw new InvalidOperationException($"生成代码失败: 请将注释写在 ResponseType 之前！");
        }

        summarys[index] = line;
        var indent = isInsideMessage ? "\t\t" : "\t";
        summarys[index] = $"{indent}///{line.TrimStart('/')}";
        hasSummary = true;
    }

    // call inside procerssor 
    private void InsertSummary()
    {
        if (hasSummary && summarys.Count > 0)
        {
            var indent = isInsideMessage ? "\t\t" : "\t";
            sb.AppendLine($"{indent}/// <summary>");
            foreach (var item in summarys)
            {
                sb.AppendLine(item.Value);
            }
            sb.AppendLine($"{indent}/// </summary>");

            //clean and reset status for next member
            summarys.Clear(); 
            hasSummary = false;
        }
    }

    // 3. handle ResponseTypeAttribute
    public void AddResponseType(string line)
    {
        if (!hasResponseTypeMarked)
        {
            try
            {
                // if has summary , append it !
                InsertSummary();
                // if has not marked serializable , append it !
                AddSerializable();

                line = line.Trim();
                string responseType = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
                sb.AppendLine($"\t[ResponseType(typeof({responseType}))]");
                hasResponseTypeMarked = true;
            }
            catch (Exception e) when (e is IndexOutOfRangeException)
            {
                throw new NotSupportedException($"代码生成错误: ResponseType 格式错误！\n{line}");
            }
        }
    }

    // 4. handle message
    public void AddMessageType(string line)
    {
        line = line.Trim();
        if (line.StartsWith("message"))
        {
            // if has not marked serializable , append it !
            AddSerializable();

            msgName = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1];
            sb.Append($"\tpublic partial class {msgName}");
            string[] ss = line.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
            parentClass = ss.Length == 2 ? ss[1].Trim() : "";
            if (parentClass == "Message" || parentClass == "Request" || parentClass == "Response")
            {
                sb.Append($" : {parentClass}");
            }
            else if (parentClass != "")
            {
                Debug.LogWarning($"{nameof(TinyProtoHandler)}: TinyRPC 消息只能继承自 Message、Request、Response ");
            }
            sb.AppendLine("\n\t{");
            isInsideMessage = true;
        }
    }

    public void AddMember(string line)
    {
        line = line.Trim();
        if (line.StartsWith("//"))
        {
            MarkSummary(sb.Length, line);
            return;
        }
        // if has summary , append it !
        InsertSummary();

        if (line.StartsWith("repeated"))
        {
            hasRepeatedField = true;
            Repeated(line);
        }
        else
        {
            Members(line);
        }
    }

    private void Repeated(string line)
    {
        try
        {
            int index = line.IndexOf(";");
            line = line.Remove(index);
            string[] ss = line.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
            string type = ss[1];
            type = ConvertType(type);
            string name = ss[2];

            sb.Append($"\t\tpublic List<{type}> {name} = new ();\n");
        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(TinyProtoHandler)}: Repeated Error {line}\n {e}");
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

    private void Members(string line)
    {
        try
        {
            int index = line.IndexOf(";");
            line = line.Remove(index);
            string[] ss = line.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
            string type = ss[0];
            string name = ss[1];
            string typeCs = ConvertType(type);

            sb.Append($"\t\tpublic {typeCs} {name} ;\n");
        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(TinyProtoHandler)}: Create Members error, line = {line}\n {e}");
        }
    }


    public string ToScript()
    {
        // insert "using Collections" for repeated field with hard code
        //  that is skip the very first line "using System;" and insert at 13
        if (hasRepeatedField)
        {
            sb.Insert(13, "\nusing System.Collections.Generic;");
        }
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        var content = sb.ToString();
        content = content.Replace("\r\n", "\n");
        return content;
    }
}
