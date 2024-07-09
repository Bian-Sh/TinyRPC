using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

// 任何情况下 .proto 文件不得同名，同名会导致生成中断
// todo :先生成新的，再删除旧的，如果生成的过程中报错，不删除旧的，保证旧的代码可以正常使用
// Editor Tab 需要有一个输入框+按钮组成的消息查询功能，避免用户遗忘消息名字，导致无法找到对应的消息，抽象查询，高亮展示在 Tab 查询功能下方，且具备下拉框功能，ping 消息所在的文件 
// Runtime Tab, Assembly 列表存 string 但展示 AssemblyDefinitionFile ，方便 Ping
// Runtime Tab ,Log Filter 高级下拉窗口选择要过滤的消息

namespace zFramework.TinyRPC.Editors
{
    public class TinyRpcEditorWindow : EditorWindow
    {
        private RuntimeSettingsLayout runtimeSettingsLayout;
        private EditorSettingsLayout editorSettingsLayout;

        int selected = 0;
        static GUIContent[] toolbarContents;

        [MenuItem("TinyRPC/Tool and Settings")]
        public static void ShowWindow() => GetWindow(typeof(TinyRpcEditorWindow));

        private void Awake()
        {
            // title with version
            var package = PackageInfo.FindForAssembly(typeof(TinyRpcEditorWindow).Assembly);
            string version;
            if (null != package)
            {
                version = package.version;
            }
            else // if TinyRPC is not installed by Package Manager, read version info in package.json
            {
                var stack = new StackTrace(true);
                var path = stack.GetFrame(0).GetFileName();
                var root = Path.GetDirectoryName(path);
                root = root.Substring(0, root.LastIndexOf("\\Editor", StringComparison.Ordinal));
                var content = File.ReadAllText($"{root}/package.json");
                version = JsonUtility.FromJson<Version>(content).version;
            }
            titleContent = new GUIContent($"TinyRPC (v{version})");
            minSize = new Vector2(460, 420);
        }

        public void OnEnable()
        {
            //init layout instance
            runtimeSettingsLayout ??= new RuntimeSettingsLayout(this);
            editorSettingsLayout ??= new EditorSettingsLayout(this);
            runtimeSettingsLayout.OnEnable();
            editorSettingsLayout.OnEnable();

            // init Editor Settings
            toolbarContents = new GUIContent[] { BT_LT, BT_RT };
            EditorSettingWatcher.OnEditorFocused += OnEditorFocused;
            EditorSettingWatcher.OnEditorLostFocus += OnEditorLostFocus;
            // Repaint window if undo redo
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            EditorSettingWatcher.OnEditorFocused -= OnEditorFocused;
            EditorSettingWatcher.OnEditorLostFocus -= OnEditorLostFocus;
            Undo.undoRedoPerformed -= Repaint;
        }

        // 当编辑器失去焦点时，保存配置，避免 svn、git 等外部的修改导致数据被覆盖
        private void OnEditorLostFocus()
        {
            TinyRpcEditorSettings.Save();
        }

        private void OnEditorFocused()
        {
            // 当编辑器重新 focus 时，重新加载实例，避免某些情景下 svn 、git 等外部修改了数据却无法同步的异常。
            TinyRpcEditorSettings.TryLoadOrCreate(true);
            editorSettingsLayout.OnEnable();
            runtimeSettingsLayout.OnEnable();
            this.Repaint();
        }

        private void OnGUI()
        {
            //draw tab Editor and Runtime
            GUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                selected = GUILayout.Toolbar(selected, toolbarContents, GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.2f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(15);
            if (selected == 0)
            {
                // Draw Editor Settings 
                editorSettingsLayout.Draw();
            }
            else
            {
                //Draw Runtime Settings
                runtimeSettingsLayout.Draw();
            }
        }

        #region GUIContents for tabs
        static GUIContent BT_LT = new GUIContent("Editor", "编辑器下使用的配置");
        static GUIContent BT_RT = new GUIContent("Runtime", "运行时使用的配置");
        #endregion
        #region Assistance Type
        [Serializable]
        public class Version
        {
            public string version;
        }
        #endregion
    }
}