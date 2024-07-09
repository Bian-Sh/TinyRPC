using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Editors
{
    /// <summary>
    ///  弹窗输入文件名
    /// </summary>
    public class PopupInputWindow : EditorWindow
    {
        private string protoName;
        private string[] files;
        private Rect rect;
        private readonly TaskCompletionSource<string> tcs;
        static PopupInputWindow instance;
        private void OnEnable() => instance = this;
        public PopupInputWindow() => tcs = new TaskCompletionSource<string>();

        public void OnGUI()
        {
            // 绘制一个 Rect 作为背景，填充整个窗口
            rect.width = 160;
            rect.height = 48;
            EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height + 38), new Color32(70, 96, 124, 255));
            GUILayout.Space(5);
            protoName = EditorGUILayout.TextField("", protoName);
            var lastRect = GUILayoutUtility.GetLastRect();
            if (lastRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.LabelField(lastRect, inputContent);
            }
            var isEmpty = string.IsNullOrEmpty(protoName);
            var isDuplicated = files.Any(f => Path.GetFileNameWithoutExtension(f) == protoName);
            var isExclude = protoName == TinyRpcEditorSettings.ProtoFileContainer;
            if (isExclude)
            {
                EditorGUILayout.HelpBox("此文件名不被允许！", UnityEditor.MessageType.Error);
                rect.height += 38;
            }
            if (isDuplicated)
            {
                EditorGUILayout.HelpBox("此文件名已存在！", UnityEditor.MessageType.Error);
                rect.height += 38;
            }
            using var horzScope = new EditorGUILayout.HorizontalScope();
            using var disableScope = new EditorGUI.DisabledScope(isEmpty || isDuplicated || isExclude);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("确定", GUILayout.Width(80)))
            {
                tcs.SetResult(protoName);
                this.Close();
            }
            GUILayout.FlexibleSpace();
            this.position = rect;
            this.minSize = new Vector2(160, rect.height);
        }
        public static Task<string> WaitForInputAsync(TinyRpcEditorSettings settings, Rect rect)
        {
            if (!instance)
            {
                instance = CreateInstance<PopupInputWindow>();
                instance.rect = rect;
                // calc if it is duplicated
                var dir = settings.GetProtoFileContianerPath();
                if (!Directory.Exists(dir))
                {
                    instance.files = new string[0];
                }
                else
                {
                    instance.files = Directory.GetFiles(dir, "*.proto");
                }
            }
            instance.ShowPopup();
            instance.Focus();
            return instance.tcs.Task;
        }
        // 当 PopupInputWindow 丢失焦点，窗口关闭
        private void OnLostFocus()
        {
            tcs.SetResult(null);
            this.Close();
        }
        readonly GUIContent inputContent = new("", "请输入文件名！");
    }
}