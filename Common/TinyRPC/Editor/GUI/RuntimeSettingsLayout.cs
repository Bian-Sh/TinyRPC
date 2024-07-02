using UnityEditor;
using UnityEngine;
using zFramework.TinyRPC.Settings;

namespace zFramework.TinyRPC.Editors
{
    public class RuntimeSettingsLayout
    {
        UnityEditor.Editor editor;
        EditorWindow window;
        SerializedProperty pingInterval;
        SerializedProperty pingRetry;
        SerializedProperty rpcTimeout;
        SerializedProperty assemblyNames; //where handlers located
        SerializedProperty logFilters; //log filters, such as ping etc. in case of too many logs at a time
        SerializedProperty logEnabled;
        Vector2 scrollPos;

        public RuntimeSettingsLayout(EditorWindow window)
        {
            this.window = window;
        }
        internal void OnEnable()
        {
            editor = UnityEditor.Editor.CreateEditor(TinyRpcSettings.Instance);

            assemblyNames = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.assemblyNames));
            logFilters = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.logFilters));
            pingInterval = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.pingInterval));
            pingRetry = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.pingRetry));
            rpcTimeout = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.rpcTimeout));
            logEnabled = editor.serializedObject.FindProperty(nameof(TinyRpcSettings.logEnabled));
        }
        public void Draw()
        {
            // draw inspector without script field
            using var changeScope = new EditorGUI.ChangeCheckScope();
            var so = editor.serializedObject;
            so.Update();
            // draw delay inputfield as their values should be validate then
            // validate input value is on going at TinyRpcSettings.Onvalidate Function
            EditorGUILayout.LabelField(pingIntervalContent, EditorStyles.boldLabel);
            EditorGUILayout.DelayedIntField(pingInterval);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(pingRetryContent, EditorStyles.boldLabel);
            EditorGUILayout.DelayedIntField(pingRetry);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(rpcTimeoutContent, EditorStyles.boldLabel);
            EditorGUILayout.DelayedIntField(rpcTimeout);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(logEnabled);
            EditorGUILayout.Space(4);

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                EditorGUILayout.LabelField(asmNameContent, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(assemblyNames);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(logFilterContent, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(logFilters);
                scrollPos = scroll.scrollPosition;
            }

            if (changeScope.changed)
            {
                so.ApplyModifiedProperties();
            }
        }

        #region GUIContents
        GUIContent pingIntervalContent = new("心跳包发送频率 （单位：毫秒）", "请勿设置过快的发送频次！");
        GUIContent pingRetryContent = new("心跳包重试次数", "请勿设置过大的重试次数，且不可负值！");
        GUIContent rpcTimeoutContent = new("RPC 最小超时 （单位：毫秒）", "用户 Response 设定的值过小时，将以此设定值为准！ Ping 消息也受此影响~");
        GUIContent asmNameContent = new("包含 MessageHandler 的Assembly ", "为减少遍历，在编辑器下会自动收集 MessageHandler 所在的程序集，如果在程序集名称前加英文字符的惊叹号，则代表编辑器下无需监测此程序集（代表你认为此程序集绝对不会出现 MessageHandler）");
        GUIContent logFilterContent = new("不输出到控制台的消息", "有些消息接受发送的非常频繁，为了避免干扰开发，可以加入到这个列表, 例如 Ping 消息");
        #endregion
    }
}