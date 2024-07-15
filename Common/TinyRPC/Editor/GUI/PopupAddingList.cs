using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using zFramework.TinyRPC.Editors;
using Object = UnityEngine.Object;

public class PopupAddingList
{
    // 重绘 ReorderableList Add 功能，实现点击“+”出现弹窗要求用户输入 proto 文件名
    // 重绘 ReorderableList Remove 功能，实现点击“-”出现确认弹窗：是否删除该 proto 文件
    // 拖入 proto 文件需要被自动添加到列表中
    readonly ReorderableList m_list;
    readonly EditorWindow window;
    readonly SerializedObject serializedObject;
    readonly SerializedProperty protoProperty;
    public PopupAddingList(EditorWindow window, SerializedObject so, SerializedProperty property)
    {
        serializedObject = so;
        protoProperty = property;
        this.window = window;
        m_list = new ReorderableList(so, property, true, true, true, true)
        {
            drawHeaderCallback = OnHeaderDrawing,
            drawElementCallback = OnElementCallbackDrawing,
            onRemoveCallback = OnRemoveCallback,
            onAddDropdownCallback = OnAddDropdownCallback,
            elementHeightCallback = OnCalcElementHeight
        };
        m_list.onChangedCallback += list => serializedObject.ApplyModifiedProperties();
    }

    public void DoLayoutList()
    {
        m_list.DoLayoutList();
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
        var file = list.serializedProperty.GetArrayElementAtIndex(list.index).FindPropertyRelative("file").objectReferenceValue;
        ReorderableList.defaultBehaviours.DoRemoveButton(list);
        if (file != null)
        {
            AskIfDeleteProtoFile(file);
        }
    }

    private async void OnAddDropdownCallback(Rect buttonRect, ReorderableList list)
    {
        if (EditorApplication.isCompiling)
        {
            window.ShowNotification(new GUIContent("请等待编译完成！"));
            return;
        }

        var rect = new Rect(buttonRect.position, buttonRect.size);
        rect.x += window.position.x - 60;
        rect.y += window.position.y + 40;
        var settings = TinyRpcEditorSettings.Instance;
        var protoName = await PopupInputWindow.WaitForInputAsync(settings, rect);
        if (!string.IsNullOrEmpty(protoName))
        {
            var path = settings.GetProtoFileContianerPath();
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
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
                list.onChangedCallback?.Invoke(list);
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
        if (m_list.count == 0)
        {
            return EditorGUIUtility.singleLineHeight;
        }
        var ele = m_list.serializedProperty.GetArrayElementAtIndex(index);
        var height = EditorGUI.GetPropertyHeight(ele, GUIContent.none, true);
        height += 2;
        return height;
    }

    private void OnHeaderDrawing(Rect rect)
    {
        EditorGUI.LabelField(rect, "Proto 文件列表:");
        // 处理将 proto 文件拖入列表 Header 中的情况
        var evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (rect.Contains(evt.mousePosition))
            {
                var reject = DragAndDrop.objectReferences.Any(obj =>
                {
                    if (obj is DefaultAsset asset)
                    {
                        var path = AssetDatabase.GetAssetPath(asset);
                        return !path.EndsWith(".proto");
                    }
                    return true; // other case should be rejected
                });
                if (reject)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        var assets = new List<DefaultAsset>();
                        for (int i = 0; i < m_list.count; i++)
                        {
                            var item = m_list.serializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative("file").objectReferenceValue;
                            if (item is DefaultAsset proto)
                            {
                                assets.Add(proto);
                            }
                        }
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is DefaultAsset asset)
                            {
                                if (assets.Contains(asset))
                                {
                                    continue;
                                }
                                protoProperty.arraySize++;
                                var itemData = protoProperty.GetArrayElementAtIndex(protoProperty.arraySize - 1);
                                itemData.FindPropertyRelative("file").objectReferenceValue = asset;
                                itemData.FindPropertyRelative("enable").boolValue = true;
                                m_list.onChangedCallback?.Invoke(m_list);
                            }
                        }
                        assets.Clear();
                    }
                }
                evt.Use();
            }
        }
    }
    #endregion
}
