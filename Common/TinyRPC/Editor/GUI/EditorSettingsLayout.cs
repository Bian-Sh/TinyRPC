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
using Object = UnityEngine.Object;
namespace zFramework.TinyRPC.Editors
{
    // 一个操作一个动作，当切换消息存储位置时，立马 Move Dir
    // 生成代码时，先删除之前的代码，再生成新的代码
    // 移动文件夹时，需要一并移动 Proto 文件夹
    public class EditorSettingsLayout
    {
        TinyRpcEditorSettings settings;
        EditorWindow window;
        SerializedObject serializedObject;
        SerializedProperty protoProperty;
        SerializedProperty asmdefsProperty;
        SerializedProperty indentWithTabProperty;
        SerializedProperty generatedScriptLocationProperty;
        SerializedProperty generateAsPartialClassProperty;
        // 重绘 ReorderableList Add 功能，实现点击“+”出现弹窗要求用户输入 proto 文件名
        // 重绘 ReorderableList Remove 功能，实现点击“-”出现确认弹窗：是否删除该 proto 文件
        // Hook Del 按键删除 item 功能，确保跟点击 “-” 是一样的效果
        // 其他行为保持不变
        ReorderableList m_list;

        private const string TinyRPCRuntimeAssembly = "GUID:c5a44f231aee9ef4895a10427e883834";
        LocationType selectedLocationType;
        string newLocation;
        string subLocation = "Common";
        public EditorSettingsLayout(EditorWindow window) => this.window = window;

        internal void OnEnable() => InitLayout();

        private void InitLayout()
        {
            settings = TinyRpcEditorSettings.Instance;
            serializedObject = new SerializedObject(settings);
            settings.currentLocationType = selectedLocationType = ResolveLocationType(settings.generatedScriptLocation);
            if (settings.currentLocationType == LocationType.Project)
            {
                subLocation = ExtractSubLocation(settings.generatedScriptLocation);
            }
            protoProperty = serializedObject.FindProperty(nameof(settings.protos));
            asmdefsProperty = serializedObject.FindProperty(nameof(settings.assemblies));
            indentWithTabProperty = serializedObject.FindProperty(nameof(settings.indentWithTab));
            generatedScriptLocationProperty = serializedObject.FindProperty(nameof(settings.generatedScriptLocation));
            generateAsPartialClassProperty = serializedObject.FindProperty(nameof(settings.generateAsPartialClass));
            m_list ??= new ReorderableList(serializedObject, protoProperty, true, true, true, true);
            m_list.drawHeaderCallback = OnHeaderDrawing;
            m_list.drawElementCallback = OnElementCallbackDrawing;
            m_list.onRemoveCallback = OnRemoveCallback;
            m_list.onAddDropdownCallback = OnAddDropdownCallback;
            m_list.elementHeightCallback = OnCalcElementHeight;

            ResolveLocation();
        }

        ~EditorSettingsLayout()
        {
            m_list.drawHeaderCallback -= OnHeaderDrawing;
        }

        private void ResolveLocation()
        {
            //根据 type 获取新的文件夹
            newLocation = selectedLocationType switch
            {
                LocationType.Project => $"{Application.dataPath}/../../{subLocation}/TinyRPC Generated".Replace("//", "/"),
                LocationType.Assets => "Assets/TinyRPC/Generated",
                LocationType.Packages => "Packages/TinyRPC Generated",
                _ => "Packages/TinyRPC Generated",
            };
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
            DrawMainContent();
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
            // 用户可选存储的文件夹： Assets 内、Project 同级目录，Packages 文件夹 
            // 支持在Project 同级目录下新增任意文件夹深度的父节点，比如：[Project 同级]/xxx/xxx/xxx/TinyRPC Generated/

            var path = generatedScriptLocationProperty.stringValue;
            if (string.IsNullOrEmpty(path))
            {
                path = generatedScriptLocationProperty.stringValue = "Assets/TinyRPC/Generated";
                settings.currentLocationType = selectedLocationType = LocationType.Assets;
            }

            var packagePath = settings.currentLocationType != LocationType.Assets ? "Packages/com.zframework.tinyrpc.generated" : path;
            DefaultAsset packageFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(packagePath);

            // 获取 Project Packages 节点下的 TinyRPC Generated 文件夹
            if (packageFolder)
            {
                if (settings.currentLocationType != LocationType.Assets)
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

            if (selectedLocationType != settings.currentLocationType)
            {
                // todo : 选择了直接提醒是否转移，是，转移，否，不转移 ，不会是下次生成代码时生效
                EditorGUILayout.HelpBox("选择了新的消息存储位置，在下次生成代码时生效", UnityEditor.MessageType.Warning);
                ResolveLocation();
            }
            m_list.DoLayoutList();
            // 如果用户插入或者删除了 .asmdef 文件，需要重新生成 .asmdef 文件
            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(asmdefsProperty, true);
                if (changeScope.changed)
                {
                    //从 asmdefsProperty 中提取所有的引用
                    var arrsize = asmdefsProperty.arraySize;
                    { // 没有值的状态也要更新
                        var references = new List<string>(arrsize);
                        for (int i = 0; i < arrsize; i++)
                        {
                            var asmdef = asmdefsProperty.GetArrayElementAtIndex(i).objectReferenceValue;
                            if (asmdef)
                            {
                                references.Add($"GUID:{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asmdef))}");
                            }
                        }
                        AddAssemblyReference(newLocation, references);
                    }
                }
            }
            // 如果用户指定了需要生成 partial class 的消息
            EditorGUILayout.PropertyField(generateAsPartialClassProperty, true);
            DrawEditorHelpbox();
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

            var isCompiling = EditorApplication.isCompiling;
            generateBt_cnt.text = isCompiling ? "编译中，请稍后..." : "生成消息实体类";
            var isCompilefailed = EditorUtility.scriptCompilationFailed;
            if (isCompilefailed)
            {
                generateBt_cnt.text = "请先解决工程编译错误...";
            }
            using (var disablescop = new EditorGUI.DisabledScope(isCompiling || isCompilefailed))
            {
                if (GUI.Button(rt, generateBt_cnt))
                {
                    //生成代码前要求工程不得有任何编译异常
                    if (EditorUtility.scriptCompilationFailed)
                    {
                        Debug.LogError($"代码生成失败，请先解决工程编译错误！");
                        return;
                    }
                    await HandlerMessageGenerateAsync();
                    PostProcess();
                }
            }
        }

        private async Task HandlerMessageGenerateAsync()
        {
            try
            {
                // 不允许刷新资产
                AssetDatabase.DisallowAutoRefresh();
                CreateNewPackage();
                //its new one , we should del the previours package
                // and save new location as well
                if (selectedLocationType != settings.currentLocationType)
                {
                    await RemovePrevioursPackageEntityAsync();
                }
                // add upm package identifier
                if (selectedLocationType == LocationType.Project || selectedLocationType == LocationType.Packages)
                {
                    var identifier = selectedLocationType == LocationType.Project ?
                        $"file:../../{subLocation}/TinyRPC Generated" : $"file:Packages/TinyRPC Generated";
                    // TODO:直接 Reimport ? 
                    //AssetDatabase.ImportAsset("Packages/floder", ImportAssetOptions.ImportRecursive);
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
                // 如果存储类型发生变化，且转移到了 Assets 下，需要重新解析 upm
                if (settings.currentLocationType != selectedLocationType && settings.currentLocationType == LocationType.Assets)
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
                if (Directory.Exists(newLocation) && selectedLocationType != settings.currentLocationType)
                {
                    FileUtil.DeleteFileOrDirectory(newLocation);
                }
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
            }
        }

        private void PostProcess()
        {
            // 保存新的文件夹
            settings.currentLocationType = selectedLocationType;
            var trimpath = newLocation.Replace(Application.dataPath, "");
            settings.generatedScriptLocation = generatedScriptLocationProperty.stringValue = trimpath;
            serializedObject.ApplyModifiedProperties();
            Save();
        }
        private void CreateNewPackage()
        {
            if (settings.protos.Count(p => p != null) == 0)
            {
                throw new Exception("请先选择 .proto 文件！");
            }

            //Create brand new  
            // Proto 文件夹不允许删除
            var dirs = Directory.GetDirectories(newLocation);
            foreach (var dir in dirs)
            {
                if (dir.EndsWith("Proto"))
                {
                    continue;
                }
                Directory.Delete(dir, true);
            }

            if (!Directory.Exists(newLocation))
            {
                Directory.CreateDirectory(newLocation);
            }
            var protos = settings.protos;
            foreach (var proto in protos)
            {
                if (proto.file && proto.enable)
                {
                    var protoPath = AssetDatabase.GetAssetPath(proto.file);
                    // validate proto file
                    if (protoPath.EndsWith(".proto"))
                    {
                        var protoContent = File.ReadAllText(protoPath);
                        TinyProtoHandler.Proto2CS(proto.file.name, protoContent, newLocation, settings);
                    }
                }
            }
            TryCreatePackageJsonFile(newLocation);
            TryCreateAssemblyDefinitionFile(newLocation);
        }
        private async Task RemovePrevioursPackageEntityAsync()
        {
            var current = settings.generatedScriptLocation;
            current = settings.currentLocationType == LocationType.Project ? Path.Combine(Application.dataPath, current) : FileUtil.GetPhysicalPath(current);

            // todo: 使用 MoveFileOrDirectory 代替删除，如果移动失败，可以提示用户关闭占用文件的程序 后继续
            try
            {
                if (Directory.Exists(current))
                {
                    FileUtil.MoveFileOrDirectory(current, newLocation);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"转移 Generated 文件夹失败，请先关闭可能占用文件的程序后重试 :{e} ");
            }

            if (settings.currentLocationType != LocationType.Assets)
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

        /// <summary>
        /// 为降低反射遍历消息的次数、减小编译时长，故使用 AssemblyDefinition 
        /// </summary>
        /// <param name="root">生成脚本的根节点路径</param>
        private void TryCreateAssemblyDefinitionFile(string path)
        {
            var file = Path.Combine(path, TinyRpcEditorSettings.AsmdefName);
            if (!File.Exists(file))
            {
                var references = settings.assemblies
                    .Where(v => v) // 不为null
                    .Select(v => $"GUID:{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(v))}")
                    .ToList();

                var asmdef = new SimpleAssemblyDefinitionFile
                {
                    name = "com.zframework.tinyrpc.generated",
                    autoReferenced = true,
                    references = new List<string>
                    {
                        TinyRPCRuntimeAssembly
                    }
                };

                asmdef.references.AddRange(references);
                var content = JsonUtility.ToJson(asmdef, true);
                File.WriteAllText(file, content, Encoding.UTF8);
            }
        }

        /// <summary>
        /// 添加程序集引用到指定路径的 .asmdef 文件中
        /// </summary>
        /// <param name="path">指定路径</param>
        /// <param name="references">要添加的引用列表</param>
        private void AddAssemblyReference(string path, List<string> references)
        {
            var file = Path.Combine(path, TinyRpcEditorSettings.AsmdefName);
            if (File.Exists(file))
            {
                var asmdef = JsonUtility.FromJson<SimpleAssemblyDefinitionFile>(File.ReadAllText(file));
                var isDirty = false;
                var toRemove = asmdef.references.Where(v => !references.Contains(v) && v != TinyRPCRuntimeAssembly).ToList();
                foreach (var item in toRemove)
                {
                    asmdef.references.Remove(item);
                    isDirty = true;
                }

                var toAdd = references.Where(v => !asmdef.references.Contains(v)).ToList();
                foreach (var item in toAdd)
                {
                    asmdef.references.Add(item);
                    isDirty = true;
                }

                if (isDirty)
                {
                    var content = JsonUtility.ToJson(asmdef, true);
                    File.WriteAllText(file, content, Encoding.UTF8);
                    // 重新导入 .asmdef 文件
                    var packagePath = settings.currentLocationType != LocationType.Assets ? "Packages/com.zframework.tinyrpc.generated" : path;
                    AssetDatabase.ImportAsset($"{packagePath}/{TinyRpcEditorSettings.AsmdefName}");
                }
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

        #region ReorderableList Callbacks

        private void AskIfDeleteProtoFile(Object file)
        {
            if (file)
            {
                var path = AssetDatabase.GetAssetPath(file);
                var name = Path.GetFileName(path);
                if (File.Exists(path) && path.EndsWith(".proto"))
                {
                    var alsoDeleteFile = EditorUtility.DisplayDialog("删除提示", $"是否同时删除文件: {name}？", "删除", "取消");
                    if (alsoDeleteFile)
                    {
                        try
                        {
                            FileUtil.DeleteFileOrDirectory(path);
                            AssetDatabase.Refresh();
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"{nameof(EditorSettingsLayout)}: 删除 {name} 失败，更多 ↓ \n{e.Message}");
                        }
                        var message = $"删除 {name} {(File.Exists(path) ? "失败" : "成功")}!";
                        window.ShowNotification(new GUIContent(message));
                    }
                }
            }
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            var index = list.index;
            var element = protoProperty.GetArrayElementAtIndex(index);
            var file = element.FindPropertyRelative("file").objectReferenceValue;

            protoProperty.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
            if (file != null)
            {
                AskIfDeleteProtoFile(file);
            }
        }

        private async void OnAddDropdownCallback(Rect buttonRect, ReorderableList list)
        {
            var rect = new Rect(buttonRect.position, buttonRect.size);
            rect.x += window.position.x - 100;
            rect.y += window.position.y + 40;
            var protoName = await PopupInputWindow.WaitForInputAsync(settings, rect);
            if (!string.IsNullOrEmpty(protoName))
            {
                var path = settings.GetProtoFileContianerPath();
                //1. 指定路径生成一个 proto 文件
                path = Path.Combine(path, $"{protoName}.proto");
                var content = "#请在下面撰写网络协议： ";
                File.WriteAllText(path, content, Encoding.UTF8);
                AssetDatabase.Refresh();
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                if (asset)
                {
                    // 2. 添加到列表中
                    list.serializedProperty.arraySize++;
                    var itemData = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
                    itemData.FindPropertyRelative("file").objectReferenceValue = asset;
                    itemData.FindPropertyRelative("enable").boolValue = true;
                    serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogError($"{nameof(EditorSettingsLayout)}: create proto file failed!");
                }
            }
        }
        private void OnElementCallbackDrawing(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = m_list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        }
        private float OnCalcElementHeight(int index)
        {
            var ele = m_list.serializedProperty.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(ele, GUIContent.none, true);
        }

        private void OnHeaderDrawing(Rect rect)
        {
            EditorGUI.LabelField(rect, "Proto 文件列表:");
        }
        #endregion

        #region GUIContents and message
        readonly GUIContent indentwithtab_content = new("使用 Tab 缩进", "取消勾选使用 4 个空格代表一个 Tab (visual studio)");
        readonly GUIContent initBt_cnt = new GUIContent("请选择 proto 文件", "请选择用于生成 .cs 实体类的 proto 文件");
        readonly GUIContent updateBt_cnt = new GUIContent("更新", "选择新的 proto 文件，如果此文件在工程外，将会复制到工程内，覆盖原有的 proto 文件");
        readonly GUIContent generateBt_cnt = new GUIContent("生成消息实体类", "");
        readonly GUIContent showFolderBt_cnt = new GUIContent("Show", "Show in Explorer");
        readonly GUIContent tips = new GUIContent("操作完成，请等待编译...");
        readonly string notice = @"1. 基于 proto3 语法魔改版，跟谷歌Protobuf没任何关系
2. 支持为生成代码引用 .asmdef 定义的程序集
3. .proto 文件不能以 “Proto” 命名";
        #endregion
    }
}
