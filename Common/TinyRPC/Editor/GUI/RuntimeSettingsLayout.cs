using System;
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
        SerializedProperty pingInterval;
        SerializedProperty pingRetry;
        SerializedProperty rpcTimeout;
        SerializedProperty assemblyNames; //where handlers located
        SerializedProperty logFilters; //log filters, such as ping etc. in case of too many logs at a time
        SerializedProperty logEnabled;

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
            EditorGUILayout.DelayedIntField(pingInterval);
            EditorGUILayout.DelayedIntField(pingRetry);
            EditorGUILayout.DelayedIntField(rpcTimeout);

            EditorGUILayout.PropertyField(logEnabled);
            EditorGUILayout.PropertyField(assemblyNames);
            EditorGUILayout.PropertyField(logFilters);

            if (changeScope.changed)
            {
                so.ApplyModifiedProperties();
            }
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