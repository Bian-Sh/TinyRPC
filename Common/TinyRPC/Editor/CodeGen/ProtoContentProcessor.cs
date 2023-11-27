using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;
namespace zFramework.TinyRPC.Editor
{
    /*
     该脚本实现了对传入 proto line 进行处理
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
    public class ProtoContentProcessor
    {
        bool isInsideMessage;
        bool hasRepeatedField;
        bool hasSerializableMarked;
        bool hasResponseTypeMarked;
        bool isTinyRpcMessage;
        bool hasBuildInTypeUsed;
        string parentClass;
        string msgName;

        readonly StringBuilder sb;
        readonly List<string> summarys = new();
        readonly List<string> members = new();
        readonly char[] splitChars = { ' ', '\t' };
        const string NAMESPACE = "zFramework.TinyRPC.Generated";
        static List<string> BuildInType => new()
        {
            nameof(Vector2),
            nameof(Vector3),
            nameof(Vector4),
            nameof(Quaternion),
            nameof(Color) ,
            nameof(Color32),
            nameof(Rect),
            nameof(Bounds),
            nameof(Matrix4x4),
            nameof(LayerMask),
        };

        public ProtoContentProcessor() => sb = new StringBuilder();

        //1. handle SerializableAttribute, call inside procerssor 
        private void AddSerializable()
        {
            if (!hasSerializableMarked)
            {
                sb.AppendLine("\t[Serializable]");
                hasSerializableMarked = true;
            }
        }

        //2. handle custom summary 
        public void MarkSummary(string line)
        {
            if (!isInsideMessage && hasResponseTypeMarked)
            {
                throw new InvalidOperationException($"生成代码失败: 请将注释写在 ResponseType 之前！");
            }
            summarys.Add(line.TrimStart('/'));
        }

        //insert summaries , call inside procerssor 
        private void InsertSummary()
        {
            if (summarys.Count > 0)
            {
                var indent = isInsideMessage ? "\t\t" : "\t";
                sb.AppendLine($"{indent}/// <summary>");
                foreach (var item in summarys)
                {
                    sb.AppendLine($"{indent}/// {item}");
                }
                sb.AppendLine($"{indent}/// </summary>");

                //clean and reset status for next member
                summarys.Clear();
            }
        }

        // 3. handle ResponseTypeAttribute
        void AddResponseType(string line)
        {
            if (!hasResponseTypeMarked)
            {
                try
                {
                    // if has summary , append it !
                    InsertSummary();
                    // if has not marked serializable , append it !
                    AddSerializable();

                    string responseType = line.Split(new string[] { " ", "//" }, StringSplitOptions.RemoveEmptyEntries)[1];
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
            if (line.StartsWith("message"))
            {
                // if has no responsetype attribute attached ,try add summary here
                if (!hasResponseTypeMarked)
                {
                    InsertSummary();
                }
                // if is not struct and has not marked serializable , append it !
                //Please ensure that the struct is marked with the[Serializable] attribute.
                //I have discovered that it will not be serialized without it when used as an item in a list or as a member of another message.
                AddSerializable();
                msgName = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] ss = line.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
                parentClass = ss.Length == 2 ? ss[1].Trim() : "";

                if (parentClass == "Message" || parentClass == "Request" || parentClass == "Response")
                {
                    sb.Append($"\tpublic partial class {msgName}");
                    sb.Append($" : {parentClass}");
                    isTinyRpcMessage = true;
                    // validate request 
                    if (parentClass == "Request" && !hasResponseTypeMarked)
                    {
                        throw new InvalidOperationException($"生成代码失败: Request 类型的消息 {msgName} 必须 ResponseType 特性指定响应类型！");
                    }
                }
                else if (string.IsNullOrEmpty(parentClass))
                {
                    sb.Append($"\tpublic struct {msgName}");
                }
                else
                {
                    Debug.LogWarning($"{nameof(TinyProtoHandler)}: 消息 {msgName} 只能 0 继承或继承自 Message、Request、Response ");
                }
                sb.AppendLine("\n\t{");
                isInsideMessage = true;
            }
        }


        public void AddMember(string line)
        {
            if (line.StartsWith("//"))
            {
                MarkSummary(line);
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

        private void MarkRecycleField(string line)
        {
            members.Add(line);
        }

        private void AddOnRecycleMethod(StringBuilder sb)
        {
            // only tinyrpc message need recycle 
            if (isTinyRpcMessage)
            {
                var indent = new string('\t', 3);
                sb.AppendLine("\t\tpublic override void OnRecycle()");
                sb.AppendLine("\t\t{");
                sb.AppendLine($"{indent}base.OnRecycle();");
                foreach (var item in members)
                {
                    sb.AppendLine($"{indent}{item}");
                }
                sb.AppendLine("\t\t}");
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
                MarkRecycleField($"{name} = null;");
                sb.Append($"\t\tpublic List<{type}> {name}{(isTinyRpcMessage ? " = new()" : "")};\n");
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TinyProtoHandler)}: Repeated Error {line}\n {e}");
            }
        }

        private static string GetFieldDefaultValue(string type)
        {
            string value = type switch
            {
                "int16" => "0",
                "int32" => "0",
                "bytes" => "null",
                "uint32" => "0",
                "long" => "0",
                "int64" => "0",
                "uint64" => "0",
                "uint16" => "0",
                "string" => "\"\"",
                "float" => "0f",
                "bool" => "false",
                _ => "default",
            };
            return value;
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
                var value = GetFieldDefaultValue(type);
                // only check once , in case of being overwrited later
                // will only be reset by reset function
                if (!hasBuildInTypeUsed)
                {
                    hasBuildInTypeUsed = BuildInType.Contains(typeCs);
                }
                MarkRecycleField($"{name} = {value};");
                sb.Append($"\t\tpublic {typeCs} {name};\n");
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TinyProtoHandler)}: Create Members error, line = {line}\n {e}");
            }
        }

        private void Header(StringBuilder sb)
        {
            var inner = new StringBuilder();
            inner.AppendLine("/*");
            inner.AppendLine("*代码由 TinyRPC 自动生成，请勿修改");
            inner.AppendLine("*don't modify manually as it generated by TinyRPC");
            inner.AppendLine("*/");
            inner.AppendLine("using System;");
            if (hasBuildInTypeUsed)
            {
                inner.AppendLine("using UnityEngine;");
            }
            if (hasRepeatedField)
            {
                inner.AppendLine("using System.Collections.Generic;");
            }
            if (isTinyRpcMessage)
            {
                inner.AppendLine("using zFramework.TinyRPC.Messages;");
            }
            inner.AppendLine($"namespace {NAMESPACE}");
            inner.AppendLine("{");
            sb.Insert(0, inner.ToString());
        }


        public ScriptInfo ToScriptInfo()
        {
            Header(sb);
            AddOnRecycleMethod(sb);
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            var content = sb.ToString();
            content = content.Replace("\r\n", "\n");
            if (!TinyRpcEditorSettings.Instance.indentWithTab)
            {
                var indentSymbol = new string(' ', 4); // visual studio use 4 space as indent usually
                content = content.Replace("\t", indentSymbol);
            }
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
            Reset();
            return scriptInfo;
        }

        private void Reset()
        {
            sb.Clear();
            isInsideMessage = false;
            isTinyRpcMessage = false;
            hasRepeatedField = false;
            hasSerializableMarked = false;
            hasResponseTypeMarked = false;
            hasBuildInTypeUsed = false;
            parentClass = "";
            msgName = "";
            summarys.Clear();
            members.Clear();
        }

        /// <summary>
        ///  根据消息块信息，生成消息代码
        /// </summary>
        /// <param name="block">分割的消息数据</param>
        /// <returns>消息代码内容</returns>
        public ScriptInfo ResolveBlockInfo(string block)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                //use keyword "message" instead of "{" for checking whether inside message or not
                if (string.IsNullOrEmpty(line) || line == "{")
                {
                    continue;
                }
                if (line.StartsWith("//"))
                {
                    if (line.StartsWith("//ResponseType") || line.StartsWith("// ResponseType")) // response type
                    {
                        AddResponseType(line);
                    }
                    else // summary 
                    {
                        MarkSummary(line);
                    }
                    continue;
                }
                // message
                if (line.StartsWith("message"))
                {
                    AddMessageType(line);
                    continue;
                }
                AddMember(line);
            }
            return ToScriptInfo();
        }
    }
}