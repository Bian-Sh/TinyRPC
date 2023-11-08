using System;
using UnityEditor;
using UnityEditorInternal;
namespace zFramework.TinyRPC.Editor
{
    /// <summary>
    /// 监听编辑器状态，当编辑器重新 focus 时，重新加载实例，避免某些情景下 svn 、git 等外部修改了数据却无法同步的异常。
    /// </summary>
    [InitializeOnLoad]
    public static class EditorSettingWatcher
    {
        public static Action OnEditorFocused;
        static bool isFocused;
        static EditorSettingWatcher() => EditorApplication.update += Update;
        static void Update()
        {
            if (isFocused != InternalEditorUtility.isApplicationActive)
            {
                isFocused = InternalEditorUtility.isApplicationActive;
                if (isFocused)
                {
                    OnEditorFocused?.Invoke();
                }
            }
        }
    }
}