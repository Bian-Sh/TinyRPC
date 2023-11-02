using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
// todo : 整合编辑器设置和运行时设置，编辑器下做代码生成，运行时做 Assembly 过滤和 Log 过滤
// todo:  支持将生成的代码放置在 3 个不同文件夹，分别是 Assets 内、Project 同级目录，Packages 文件夹
// Assets 内： 支持存到 Assets 下的任意路径             ， 路径是 ：TinyRPC/Generated
// Project 同级：方便代码公用，但是需要我生成 package.json ，顺便 version 自增，同时还要自动加到 manifest.json 中 , 路径是：../../TinyRPC Generated
// Packages 文件夹：方便代码公用，但是需要我生成 package.json ，顺便 version 自增 ，路径是：Packages/TinyRPC Generated
// 以上需要控制编辑器编译时机，这个 API 需要慎重，生成代码前关闭自动编译，代码生成完成或者代码自动生成过程中抛异常一定要重新开启编译功能
// 计划是使用 Tab 页签切换，页签分别是：Editor 、Runtime

//支持多个 proto 文件，逻辑是：
// 为每个proto 文件内的消息创建一个文件夹，文件夹名字为 proto 文件名字，为所有消息生成以消息名字命名的 cs 单文件
// 如果位于不同文件夹的 proto 同名，则生成的 cs 文件会放在同一个文件夹下，重复的消息仅作告警处理
// 由于消息的量级可能会越来越大， proto 匹配的文件夹中还会生成 Normal + RPC 文件夹
// Normal 文件夹中存放的是普通消息
// RPC 文件夹中存放的是 RPC 消息,并且 RPC 消息是 Request + Response 生成在同一个 .cs 文件中，方便查看

// Editor Tab 需要有一个输入框+按钮组成的消息查询功能，避免用户遗忘消息名字，导致无法找到对应的消息，抽象查询，高亮展示在 Tab 查询功能下方，且具备下拉框功能，ping 消息所在的文件 
// 由于没有消息 ID这一说法，所以，可能不需要查询功能，或者简单的查询
// Runtime Tab, 编辑器下 Assembly 最好使用 AssemblyDefinitionFile ，方便 Ping, 实际上存的依旧是 Assembly.Name,判断依旧是  StartWith
// Runtime Tab ,编辑器下 Log Filter 最好使用 高级下拉窗口，方便用户选择，实际上存的依旧是 Type.Name,判断依旧是  Contain 就不输出收到网络消息的 log,比如 ping

namespace zFramework.TinyRPC.Editor
{
    public class TinyRpcEditorWindow : EditorWindow
    {
        int selected = 0;
        static GUIContent[] toolbarContents;

        static string MessagePath;
        static string ProtoPath;
        static string ProtoPathKey = $"{nameof(TinyRpcEditorWindow)}-ProtoPath-Key";
        DefaultAsset asset;
        SerializedObject serializedObject_editor;

        static EditorWindow window;
        [MenuItem("Tools/.proto 转 .cs 实体类")]
        public static void ShowWindow()
        {
            window = GetWindow(typeof(TinyRpcEditorWindow));
        }
        public void OnEnable()
        {
            // title with version
            var package = PackageInfo.FindForAssembly(typeof(TinyRpcEditorWindow).Assembly);
            var version = package.version;
            titleContent = new GUIContent($"TinyRPC (ver {version})");

            // init Editor Settings
            toolbarContents = new GUIContent[] { BT_LT, BT_RT };
            serializedObject_editor = new SerializedObject(TinyRpcEditorSettings.Instance);

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
            using (var changescope = new EditorGUI.ChangeCheckScope())
            {
                //draw tab Editor and Runtime
                GUILayout.Space(10);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    var idx = EditorPrefs.GetInt(key, 0);
                    selected = GUILayout.Toolbar(idx, toolbarContents, GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.2f));

                    if (selected != idx)
                    {
                        idx = selected;
                        EditorPrefs.SetInt(key, idx);
                    }
                    GUILayout.FlexibleSpace();
                }
                if (selected == 0)
                {
                    DrawEditorSettings();
                    if (changescope.changed)
                    {
                        serializedObject_editor.ApplyModifiedProperties();
                        TinyRpcEditorSettings.Save();
                    }
                    DrawCodeGenerateButton();
                }
                else
                {
                    DrawRuntimeSettings();
                    // 检测ObjectField是否有修改
                    if (changescope.changed)
                    {
                        ProtoPath = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;
                        EditorPrefs.SetString(ProtoPathKey, ProtoPath);
                    }
                }
            }
        }

        private void DrawEditorSettings()
        {
            //Draw Editor Settings
            GUILayout.Space(15);
            serializedObject_editor.Update();
            EditorGUILayout.PropertyField(serializedObject_editor.FindProperty("protos"), true);
        }

        private void DrawCodeGenerateButton()
        {
            var rt = GUILayoutUtility.GetLastRect();
            rt.width = 200;
            rt.height = 48;
            rt.x = (position.width - rt.width) / 2;
            rt.y = position.height - rt.height - 10;
            if (GUI.Button(rt, "生成 .cs 实体类"))
            {
                TryCreateAssemblyDefinitionFile();
                TinyProtoHandler.Proto2CS("zFramework.TinyRPC.Generated", asset, MessagePath);
                ShowNotification(tips);
                AssetDatabase.Refresh();
            }
        }
        private void DrawRuntimeSettings()
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
            // Debug.Log($"{nameof(TinyRpcEditorWindow)}: relative path = {relativePath} ");
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


        #region GUIContents and message
        static GUIContent BT_LT = new GUIContent("Editor", "编辑器下使用的配置");
        static GUIContent BT_RT = new GUIContent("Runtime", "运行时使用的配置");
        GUIContent initBt_cnt = new GUIContent("请选择 proto 文件", "请选择用于生成 .cs 实体类的 proto 文件");
        GUIContent updateBt_cnt = new GUIContent("更新", "选择新的 proto 文件，如果此文件在工程外，将会复制到工程内，覆盖原有的 proto 文件");
        GUIContent tips = new GUIContent("操作完成，请等待编译...");
        string notice = @"1. 选择的 .proto 文件不在工程中则拷贝至工程中
2. 拷贝的副本只存在一份，永远执行覆盖操作
3. 选择的 .proto 文件位于工程中则不做上述处理
4.  proto 文件中的语法是基于 proto3 语法的变体（精简版）";
        const string key = "TinyRPC Tab Index";
        #endregion
    }
}