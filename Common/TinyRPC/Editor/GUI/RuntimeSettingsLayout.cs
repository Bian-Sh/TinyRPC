using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using zFramework.TinyRPC.Settings;

namespace zFramework.TinyRPC.Editor
{
    public class RuntimeSettingsLayout
    {
        UnityEditor.Editor editor;
        EditorWindow window;
        public RuntimeSettingsLayout(EditorWindow window)
        {
            this.window = window;
            editor = UnityEditor.Editor.CreateEditor(TinyRpcSettings.Instance);
        }
        public void Draw()
        {
            editor.DrawDefaultInspector();
        }

        private void DrawRuntimeSettings()
        {

            GUILayout.Space(15);

            /* 写到 Editor 里面，方便用户选择外部 proto 文件更新
            //using (new GUILayout.HorizontalScope())
            //{
            //    asset = EditorGUILayout.ObjectField("Proto 文件：", asset, typeof(DefaultAsset), false) as DefaultAsset;
            //    if (GUILayout.Button(updateBt_cnt, GUILayout.Width(60)))
            //    {
            //        SelectAndLoadProtoFile();
            //    }
            //}
             */


        }

    }
}