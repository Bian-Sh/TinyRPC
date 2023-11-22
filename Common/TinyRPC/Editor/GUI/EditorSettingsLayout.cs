using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditorInternal;
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
        SerializedProperty generatedScriptLocationProperty;
        const string ProtoLocation = "Assets/TinyRPC/Proto";
        LocationType selectedLocationType, currentLocationType;
        string newLocation;
        string subLocation = "Common";
        public EditorSettingsLayout(EditorWindow window) => this.window = window;

        internal void OnEnable() => InitLayout();

        private void InitLayout()
        {
            settings = TinyRpcEditorSettings.Instance;
            serializedObject = new SerializedObject(settings);
            currentLocationType = selectedLocationType = ResolveLocationType(settings.generatedScriptLocation);
            if (currentLocationType == LocationType.Project)
            {
                subLocation = ExtractSubLocation(settings.generatedScriptLocation);
            }
            protoProperty = serializedObject.FindProperty(nameof(settings.protos));
            indentWithTabProperty = serializedObject.FindProperty(nameof(settings.indentWithTab));
            generatedScriptLocationProperty = serializedObject.FindProperty(nameof(settings.generatedScriptLocation));
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
            if (protoProperty.arraySize == 0)
            {
                DrawArrayEmptyInterface();
            }
            else
            {
                DrawMainContent();
                DrawIndentToggle();
                DrawCodeGenerateButton();
            }
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
            DrawEditorHelpbox();
            // 用户可选存储的文件夹： Assets 内、Project 同级目录，Packages 文件夹 
            // 支持在Project 同级目录下新增任意文件夹深度的父节点，比如：[Project 同级]/xxx/xxx/xxx/TinyRPC Generated/

            var path = generatedScriptLocationProperty.stringValue;
            if (string.IsNullOrEmpty(path))
            {
                path = generatedScriptLocationProperty.stringValue = "Assets/TinyRPC/Generated";
                currentLocationType = selectedLocationType = LocationType.Assets;
            }

            var packagePath = currentLocationType != LocationType.Assets ? "Packages/com.zframework.tinyrpc.generated" : path;
            DefaultAsset packageFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(packagePath);

            // 获取 Project Packages 节点下的 TinyRPC Generated 文件夹
            if (packageFolder)
            {
                if (currentLocationType != LocationType.Assets)
                {
                    packageFolder.name = "TinyRPC Generated";
                }
                using (var horizontal = new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("消息存储路径：", packageFolder, typeof(DefaultAsset), false);
                    GUI.enabled = true;
                    if (GUILayout.Button(showFolderBt_cnt, GUILayout.Width(60)))
                    {
                        EditorUtility.RevealInFinder(packagePath);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("未找到消息存储文件夹，请生成！", UnityEditor.MessageType.Error);
            }

            // draw loaction type as enum popup
            selectedLocationType = (LocationType)EditorGUILayout.EnumPopup("消息存储位置：", selectedLocationType);

            // 如果 newlocationtype 是 Project, 需要绘制 Delay Input field 提供用户指定子路径
            // 用户输入的子路径需要校验，剔除 ../ 和 ./ 以及空格，不允许通过组合 ../ 和 ./ 来跳出工程目录

            if (selectedLocationType == LocationType.Project)
            {
                GUIContent subPathContent = new("新增父节点", "选择 Project 目录时，你可以为：TinyRPC Generated 文件夹提供父节点！");
                subLocation = EditorGUILayout.TextField(subPathContent, subLocation);
                subLocation = subLocation.Replace("\\", "/").Trim();
                var error = ValidateSubLocation(subLocation);
                if (!string.IsNullOrEmpty(error))
                {
                    EditorGUILayout.HelpBox(error, UnityEditor.MessageType.Error);
                }
            }
            if (selectedLocationType != currentLocationType)
            {
                // draw waring helpbox : 选择了新的消息存储位置，在下次生成代码时生效 
                EditorGUILayout.HelpBox("选择了新的消息存储位置，在下次生成代码时生效", UnityEditor.MessageType.Warning);
            }
        }

        private void DrawArrayEmptyInterface()
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
            indentWithTabProperty.boolValue = EditorGUI.ToggleLeft(rt, indentwithtab_content, indentWithTabProperty.boolValue);
        }

        private async void DrawCodeGenerateButton()
        {
            var rt = GUILayoutUtility.GetLastRect();
            rt.width = 200;
            rt.height = 48;
            rt.x = (window.position.width - rt.width) / 2;
            rt.y = window.position.height - rt.height - 10;

            if (GUI.Button(rt, generateBt_cnt))
            {
                await HandlerMessageGenerateAsync();
                PostProcess();
            }
        }

        private async Task HandlerMessageGenerateAsync()
        {
            try
            {
                CreateNewPackage();
                //its new one , we should del the previours package
                // and save new location as well
                if (selectedLocationType != currentLocationType)
                {
                    await RemovePrevioursPackageEntityAsync();
                }
                // add upm package identifier
                if (selectedLocationType == LocationType.Project || selectedLocationType == LocationType.Packages)
                {
                    var identifier = selectedLocationType == LocationType.Project ?
                        $"file:../../{subLocation}/TinyRPC Generated" : $"file:Packages/TinyRPC Generated";
                    var error = await Client.Add(identifier);
                    if (string.IsNullOrEmpty(error))
                    {
                        Debug.Log($"{nameof(EditorSettingsLayout)}: add upm package success!");
                    }
                    else
                    {
                        Debug.Log($"{nameof(EditorSettingsLayout)}: add upm package failed! {error}");
                    }
                }
                //location type changed  or  type is not  "assets"  is need to resolve packages every time
                if (currentLocationType != selectedLocationType || currentLocationType != LocationType.Assets)
                {
                    Debug.Log($"{nameof(EditorSettingsLayout)}:  needResolve upm!");
                    Client.Resolve();
                }
                window.ShowNotification(tips);
                Debug.Log($"TinyRPC 消息生成完成！ ");
            }
            catch (Exception e)
            {
                Debug.LogError($"TinyRPC CodeGen Error :{e} ");
                //if user move to new place and cause exception try to rollback 
                // but if user is only update messages, do not rollback
                if (Directory.Exists(newLocation) && selectedLocationType != currentLocationType)
                {
                    FileUtil.DeleteFileOrDirectory(newLocation);
                }
            }
            finally
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            }
        }

        private void PostProcess()
        {
            // 保存新的文件夹
            currentLocationType = selectedLocationType;
            settings.generatedScriptLocation = generatedScriptLocationProperty.stringValue = newLocation;
            serializedObject.ApplyModifiedProperties();
            Save();
        }
        private void CreateNewPackage()
        {
            //根据 type 获取新的文件夹
            newLocation = selectedLocationType switch
            {
                LocationType.Project => $"{Application.dataPath}/../../{subLocation}/TinyRPC Generated".Replace("//", "/"),
                LocationType.Assets => "Assets/TinyRPC/Generated",
                LocationType.Packages => "Packages/TinyRPC Generated",
                _ => "Assets/TinyRPC/Generated",
            };

            if (settings.protos.Count(p => p != null) == 0)
            {
                throw new Exception("请先选择 .proto 文件！");
            }

            //Create brand new  
            if (Directory.Exists(newLocation))
            {
                Directory.Delete(newLocation, true);
            }

            if (!Directory.Exists(newLocation))
            {
                Directory.CreateDirectory(newLocation);
            }
            var protos = settings.protos;
            foreach (var proto in protos)
            {
                if (proto)
                {
                    var protoPath = AssetDatabase.GetAssetPath(proto);
                    var protoContent = File.ReadAllText(protoPath);
                    TinyProtoHandler.Proto2CS(proto.name, protoContent, newLocation);
                }
            }
            TryCreatePackageJsonFile(newLocation);
            TryCreateAssemblyDefinitionFile(newLocation);
        }
        private async Task RemovePrevioursPackageEntityAsync()
        {
            var current = settings.generatedScriptLocation;
            current = currentLocationType == LocationType.Project ? Path.Combine(Application.dataPath, current) : FileUtil.GetPhysicalPath(current);

            //删除现有的文件夹
            if (Directory.Exists(current))
            {
                FileUtil.DeleteFileOrDirectory(current);
                var meta = $"{current}.meta";
                if (File.Exists(meta))
                {
                    // 删除 meta 文件
                    FileUtil.DeleteFileOrDirectory(meta);
                }
            }
            else
            {
                Debug.Log($"{nameof(EditorSettingsLayout)}: can not find dir named {current}");
            }

            if (currentLocationType != LocationType.Assets)
            {
                var error = await Client.Remove("com.zframework.tinyrpc.generated");
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"{nameof(EditorSettingsLayout)}: remove upm package success!");
                }
                else
                {
                    Debug.Log($"{nameof(EditorSettingsLayout)}: remove upm package failed! {error}");
                }
            }
        }
        private string ValidateSubLocation(string subLocation)
        {
            // 检查是否包含 ../, ./, 或 //
            if (subLocation.Contains("../") || subLocation.Contains("./") || subLocation.Contains("//"))
            {
                return "不允许使用 ../ 或 ./ 或 // 跳出此工程所在目录！";
            }

            // 检查是否包含非法字符
            var invalidChars = new char[] { '<', '>', '*', ':', '?', '|' };
            foreach (var c in invalidChars)
            {
                if (subLocation.Contains(c))
                {
                    return $"路径中包含非法字符：{c}";
                }
            }
            // 如果没有问题，返回 null
            return null;
        }
        public string ExtractSubLocation(string path)
        {
            // 确保路径以 TinyRPC Generated 结尾
            if (!path.EndsWith("TinyRPC Generated"))
            {
                return "路径必须以 'TinyRPC Generated' 结尾";
            }

            // 移除路径的 TinyRPC Generated 部分
            var trimmedPath = path.Substring(0, path.Length - "TinyRPC Generated".Length);

            // 使用 Split 方法分割路径
            var pathParts = trimmedPath.Split(new[] { "../../" }, StringSplitOptions.None);

            // 如果路径部分少于2，返回错误
            if (pathParts.Length < 2)
            {
                return "路径中必须包含 '../../'";
            }

            // 提取最后一个 ../../ 之后的子位置
            var subLocation = pathParts[pathParts.Length - 1];

            // 移除子位置前后的所有斜杠
            subLocation = subLocation.Trim('/');

            return subLocation;
        }
        private void SelectAndLoadProtoFile()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("请选择 .proto 文件", Application.dataPath, new string[] { "Protobuf file", "proto" });
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var relatedPath = FileUtil.GetProjectRelativePath(path);
                    //.proto 文件不在工程内，则拷贝到工程中,且覆盖原有的 proto 文件
                    // 如果在工程内，则不做处理，尊重用户随意存放的权力
                    if (string.IsNullOrEmpty(relatedPath))
                    {
                        var fileName = Path.GetFileName(path);
                        relatedPath = $"{ProtoLocation}/{fileName}";
                        if (!Directory.Exists(ProtoLocation))
                        {
                            Directory.CreateDirectory(ProtoLocation);
                        }
                        File.Copy(path, relatedPath, true);
                        AssetDatabase.Refresh();
                    }
                    var proto = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relatedPath);
                    protoProperty.arraySize = 1;
                    protoProperty.GetArrayElementAtIndex(0).objectReferenceValue = proto;
                    serializedObject.ApplyModifiedProperties();
                    TinyRpcEditorSettings.Save();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// 为降低反射遍历消息的次数、减小编译时长，故使用 AssemblyDefinition 
        /// </summary>
        /// <param name="root">生成脚本的根节点路径</param>
        private void TryCreateAssemblyDefinitionFile(string path)
        {
            string name = "com.zframework.tinyrpc.generated.asmdef";
            string content = @"{
    ""name"": ""com.zframework.tinyrpc.generated"",
    ""references"": [
        ""GUID:c5a44f231aee9ef4895a10427e883834""
    ],
    ""autoReferenced"": true
}";
            var file = Path.Combine(path, name);
            if (!File.Exists(file))
            {
                File.WriteAllText(file, content, Encoding.UTF8);
            }
        }

        private void TryCreatePackageJsonFile(string path)
        {
            string name = "package.json";
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
  ""type"": ""library""
}";
            var file = Path.Combine(path, name);
            if (!File.Exists(file))
            {
                File.WriteAllText(file, content, Encoding.UTF8);
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
            var type = path switch
            {
                string p when p.EndsWith("Assets/TinyRPC/Generated") => LocationType.Assets,
                string p when p.EndsWith("Packages/TinyRPC Generated") => LocationType.Packages,
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
        GUIContent indentwithtab_content = new("使用 Tab 缩进", "取消勾选使用 4 个空格代表一个 Tab (visual studio)");
        GUIContent initBt_cnt = new GUIContent("请选择 proto 文件", "请选择用于生成 .cs 实体类的 proto 文件");
        GUIContent updateBt_cnt = new GUIContent("更新", "选择新的 proto 文件，如果此文件在工程外，将会复制到工程内，覆盖原有的 proto 文件");
        GUIContent generateBt_cnt = new GUIContent("生成消息实体类", "点击将在你选择的存储文件夹根据 proto 生成消息实体类");
        GUIContent showFolderBt_cnt = new GUIContent("Show", "Show in Explorer");
        GUIContent tips = new GUIContent("操作完成，请等待编译...");
        string notice = @"1. 选择的 .proto 文件不在工程中则拷贝至工程中
2. 拷贝的副本只存在一份，永远执行覆盖操作
3. 选择的 .proto 文件位于工程中则不做上述处理
4.  proto 文件中的语法是基于 proto3 语法的变体（精简版）";
        #endregion
    }
}