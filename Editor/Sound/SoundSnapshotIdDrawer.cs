using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Property drawer для SoundSnapshotId
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundSnapshotId))]
    public class SoundSnapshotIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var presetProp = property.FindPropertyRelative("preset");
            var customProp = property.FindPropertyRelative("custom");
            
            // Label
            position = EditorGUI.PrefixLabel(position, label);
            
            // Разделить на две части
            float halfWidth = position.width * 0.5f - 2;
            Rect presetRect = new Rect(position.x, position.y, halfWidth, position.height);
            Rect customRect = new Rect(position.x + halfWidth + 4, position.y, halfWidth, position.height);
            
            // Preset dropdown
            EditorGUI.PropertyField(presetRect, presetProp, GUIContent.none);
            
            // Custom field (enabled only if preset is None)
            bool isCustom = presetProp.enumValueIndex == 0;
            EditorGUI.BeginDisabledGroup(!isCustom);
            customProp.stringValue = EditorGUI.TextField(customRect, customProp.stringValue);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.EndProperty();
        }
    }
}
