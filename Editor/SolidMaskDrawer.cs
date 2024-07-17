using UnityEditor;
using UnityEngine;

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx.Editor
{
    [CustomPropertyDrawer(typeof(OutlineFxFeature.SolidMask))]
    public class SolidMaskDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = 3;
            
            if (property.isExpanded == false)
                lines = 1;
            
            return lines * EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var enabled  = property.FindPropertyRelative(nameof(OutlineFxFeature.SolidMask._enabled));
            var pattern  = property.FindPropertyRelative(nameof(OutlineFxFeature.SolidMask._pattern));
            var scale    = property.FindPropertyRelative(nameof(OutlineFxFeature.SolidMask._scale));
            var velocity = property.FindPropertyRelative(nameof(OutlineFxFeature.SolidMask._velocity));
            
            var line = 0;
            OptionalDrawer.OnGui(_fieldRect(line ++), label, enabled, pattern);
            property.isExpanded = EditorGUI.Foldout(_fieldRect(line - 1), property.isExpanded, GUIContent.none, true);
            
            if (property.isExpanded == false)
                return;
            EditorGUI.indentLevel ++;
            using (new EditorGUI.DisabledGroupScope(!enabled.boolValue))
            {
                EditorGUI.PropertyField(_fieldRect(line ++), scale, true);
                EditorGUI.PropertyField(_fieldRect(line ++), velocity, true);
            }
            EditorGUI.indentLevel --;
            
            // -----------------------------------------------------------------------
            Rect _fieldRect(int line)
            {
                return new Rect(position.x, position.y + line * EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}