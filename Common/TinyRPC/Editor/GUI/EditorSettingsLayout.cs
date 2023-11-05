using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Editor
{
    public class EditorSettingsLayout
    {
        TinyRpcEditorSettings settings;
        EditorWindow window;
        SerializedObject serializedObject;
        SerializedProperty protoProperty;
        SerializedProperty indentWithTabProperty;
        const string ProtoLocation = "Assets/TinyRPC/Proto";
        public EditorSettingsLayout(EditorWindow window) => this.window = window;

        internal void OnEnable() => InitLayout();

        private void InitLayout()
        {
            settings = TinyRpcEditorSettings.Instance;
            serializedObject = new SerializedObject(settings);
            protoProperty = serializedObject.FindProperty("protos");
            indentWithTabProperty = serializedObject.FindProperty("indentWithTab");
        }

        public void Draw()
        {
            //编辑器下切换场景时，settings 会被置空，故重新获取
            if (null == serializedObject || !serializedObject.targetObject)
            {
                InitLayout();
            }
            serializedObject.Update();
            using var changescope = new EditorGUI.ChangeCheckScope();
            DrawArrayEmptyInterface();
            DrawMainContent();
            DrawEditorHelpbox();
            DrawIndentToggle();
            DrawCodeGenerateButton();
            if (changescope.changed)
            {
                serializedObject.ApplyModifiedProperties();
                Save();
            }
        }

        private void Save() => TinyRpcEditorSettings.Save();


        private void DrawMainContent()
        {
            EditorGUILayout.PropertyField(protoProperty, true);
            //todo:  不能是只读的 package 文件夹、Editor 文件夹以及标记为纯粹的 Editor 程序集文件夹
            // todo: 用户可选存储的文件夹： Assets 内、Project 同级目录，Packages 文件夹 
            // todo: 支持在上述三个文件夹的情况下新增任意文件夹深度，比如：Assets/TinyRPC/xxx/xxx/xxx/Generated/

            GUI.enabled = false;
            var path = settings.generatedScriptLocation;
            if (string.IsNullOrEmpty(path))
            {
                path = settings.generatedScriptLocation = "Assets/TinyRPC/Generated";
                Save();
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUILayout.ObjectField("消息存储路径：", folder, typeof(DefaultAsset), false);
            GUI.enabled = true;
        }

        private void DrawArrayEmptyInterface()
        {
            if (protoProperty.arraySize == 0)
            {
                //获取当前 editorwindow 宽高
                var rect = EditorGUILayout.GetControlRect();
                rect.height = 48;
                rect.width = 200;
                rect.x = (window.position.width - rect.width) / 2;
                rect.y = (window.position.height - rect.height) / 2;
                if (GUI.Button(rect, initBt_cnt))
                {
                    SelectAndLoadProtoFile();
                }
                EditorGUIUtility.ExitGUI();
            }
        }

        private void DrawEditorHelpbox()
        {
            GUILayout.Space(15);
            var style_helpbox = GUI.skin.GetStyle("HelpBox");
            var size_font = style_helpbox.fontSize;
            style_helpbox.fontSize = 12;
            var content = new GUIContent(notice, EditorGUIUtility.IconContent("console.infoicon").image);
            var height = style_helpbox.CalcHeight(content, EditorGUIUtility.currentViewWidth);
            EditorGUILayout.LabelField(content, style_helpbox, GUILayout.Height(height));
            style_helpbox.fontSize = size_font;
            GUILayout.Space(15);
        }
        private void DrawIndentToggle()
        {
            var rt = GUILayoutUtility.GetLastRect();
            rt.width = 100;
            rt.height = 24;
            rt.x = window.position.width - rt.width - 10;
            rt.y = window.position.height - rt.height - 10;
            indentWithTabProperty.boolValue = EditorGUI.ToggleLeft(rt, "使用 Tab 缩进", indentWithTabProperty.boolValue);
        }

        private void DrawCodeGenerateButton()
        {
            var rt = GUILayoutUtility.GetLastRect();
            rt.width = 200;
            rt.height = 48;
            rt.x = (window.position.width - rt.width) / 2;
            rt.y = window.position.height - rt.height - 10;
            if (GUI.Button(rt, "生成 .cs 实体类"))
            {
                TryCreatePackageJsonFile();
                TryCreateAssemblyDefinitionFile();
                var protos = settings.protos;
                foreach (var proto in protos)
                {
                    if (proto)
                    {
                        var protoPath = AssetDatabase.GetAssetPath(proto);
                        var protoContent = File.ReadAllText(protoPath);
                        TinyProtoHandler.Proto2CS(proto.name, protoContent, settings.generatedScriptLocation);
                    }
                }
                window.ShowNotification(tips);
                AssetDatabase.Refresh();
            }
        }

        private void SelectAndLoadProtoFile()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("请选择 .proto 文件", Application.dataPath, new string[] { "Protobuf file", "proto" });
            if (!string.IsNullOrEmpty(path))
            {
                var relatedPath = FileUtil.GetProjectRelativePath(path);
                //.proto 文件不在工程内，则拷贝到工程中,且覆盖原有的 proto 文件
                // 如果在工程内，则不做处理，尊重用户随意存放的权力
                if (string.IsNullOrEmpty(relatedPath))
                {
                    var fileName = Path.GetFileName(path);
                    var destPath = $"{ProtoLocation}/{fileName}";
                    File.Copy(path, destPath, true);
                    relatedPath = FileUtil.GetProjectRelativePath(destPath);
                    AssetDatabase.Refresh();
                }
                var proto = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relatedPath);
                TinyRpcEditorSettings.Instance.protos.Add(proto);
                TinyRpcEditorSettings.Save();
            }
        }

        /// <summary>
        /// 为降低反射遍历消息的次数、减小编译时长，故使用 AssemblyDefinition 
        /// </summary>
        /// <param name="root">生成脚本的根节点路径</param>
        private void TryCreateAssemblyDefinitionFile()
        {
            string file = "com.zframework.tinyrpc.generated.asmdef";
            string content = @"{
    ""name"": ""com.zframework.tinyrpc.generated"",
    ""references"": [
        ""GUID:c5a44f231aee9ef4895a10427e883834""
    ],
    ""autoReferenced"": true
}";
            var path = Path.Combine(settings.generatedScriptLocation, file);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Encoding.UTF8);
                Debug.Log($"Assembly Definition File 生成 {file} 成功！");
            }
        }

        private void TryCreatePackageJsonFile()
        {
            string file = "package.json";
            string content = @"{
  ""name"": ""com.zframework.tinyrpc.generated"",
  ""displayName"": ""TinyRPC Generated"",
  ""version"": ""1.0.0"",
  ""description"": ""TinyRpc \u751f\u6210\u7684\u6d88\u606f\u6570\u636e\u6a21\u7ec4"",
  ""keywords"": [
    ""message"",
    ""tiny rpc"",
    ""code generation""
  ],
  ""author"": {
    ""name"": ""bian shanghai"",
    ""url"": ""https://www.jianshu.com/u/275cca6e5f17""
  },
  ""type"": ""library""
}";
            var path = Path.Combine(settings.generatedScriptLocation, file);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Encoding.UTF8);
                Debug.Log($"Package.json 生成 {file} 成功！");
            }
        }





        /// <summary>
        ///  根据给定的路径，判断 proto 文件的存储位置类型
        /// </summary>
        public LocationType ResolveLocationType(string path)
        {
            // assets 内: Assets/TinyRPC/Generated
            // project 同级：Application.DataPath/../{subPath}/TinyRPC Generated
            // packages: Packages/TinyRPC Generated

            Debug.Log($"path: {path}");
            var type = path switch
            {
                string p when p.StartsWith("Assets") => LocationType.Assets,
                string p when p.StartsWith("Packages") => LocationType.Packages,
                _ => LocationType.Project,
            };
            return type;
        }


        public enum LocationType
        {
            Assets,
            Project, //Project 同级目录,但是依旧出现在 Packages 模块下
            Packages // 文件夹直接出现在 Packages 文件夹下
        }

        #region GUIContents and message
        GUIContent initBt_cnt = new GUIContent("请选择 proto 文件", "请选择用于生成 .cs 实体类的 proto 文件");
        GUIContent updateBt_cnt = new GUIContent("更新", "选择新的 proto 文件，如果此文件在工程外，将会复制到工程内，覆盖原有的 proto 文件");
        GUIContent tips = new GUIContent("操作完成，请等待编译...");
        string notice = @"1. 选择的 .proto 文件不在工程中则拷贝至工程中
2. 拷贝的副本只存在一份，永远执行覆盖操作
3. 选择的 .proto 文件位于工程中则不做上述处理
4.  proto 文件中的语法是基于 proto3 语法的变体（精简版）";
        #endregion
    }
}