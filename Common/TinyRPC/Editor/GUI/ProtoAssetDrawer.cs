using UnityEditor;
using UnityEngine;
using zFramework.TinyRPC.Settings;
namespace zFramework.TinyRPC.Editors
{
    [CustomPropertyDrawer(typeof(ProtoAsset))]
    public class ProtoAssetDrawer : PropertyDrawer
    {
        readonly GUIContent enableContent = new GUIContent("", "请选择是否为此 .proto 文件生成代码！ ");
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var ctrl_id = GUIUtility.GetControlID(FocusType.Passive);
            position = EditorGUI.PrefixLabel(position, ctrl_id, GUIContent.none);
            var height = EditorGUIUtility.singleLineHeight;
            var enableRect = new Rect(position.x, position.y, 24, height);
            var fileRect = new Rect(position.x + 24, position.y, position.width - 24, height);
            EditorGUI.PropertyField(enableRect, property.FindPropertyRelative("enable"), enableContent);
            // hover " enable" toggle  to show tooltip: "请选择是否为此 .proto 文件生成代码！ "
            if (enableRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
            {
                EditorGUI.LabelField(enableRect, enableContent);
            }

            EditorGUI.PropertyField(fileRect, property.FindPropertyRelative("file"), GUIContent.none);
            var result = ValidateFileTypes(property, out var file);
            if (file != null && !result)
            {
                var rect = new Rect(position.x + 24, position.y + height + 4, position.width - 24, position.height - height - 6);
                EditorGUI.HelpBox(rect, "请选择 .proto 文件！", UnityEditor.MessageType.Error);
            }

            EditorGUI.EndProperty();
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var result = ValidateFileTypes(property, out var file);
            var height = base.GetPropertyHeight(property, label);
            if (file != null && !result)
            {
                height += 28;
            }
            return height;
        }

        // Validate Proto File , if not .proto file , return false
        public bool ValidateFileTypes(SerializedProperty property, out DefaultAsset file)
        {
            file = property.FindPropertyRelative("file").objectReferenceValue as DefaultAsset;
            if (file == null) return false;
            var path = AssetDatabase.GetAssetPath(file);
            return path.EndsWith(".proto");
        }
    }
}
