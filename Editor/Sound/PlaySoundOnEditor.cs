using UnityEngine;
using UnityEditor;
using ProtoSystem.Sound;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Кастомный редактор для PlaySoundOn — скрывает нерелевантные поля
    /// </summary>
    [CustomEditor(typeof(PlaySoundOn))]
    public class PlaySoundOnEditor : UnityEditor.Editor
    {
        private SerializedProperty _soundId;
        private SerializedProperty _volume;
        private SerializedProperty _pitchVariation;
        private SerializedProperty _trigger;
        private SerializedProperty _targetTag;
        private SerializedProperty _minCollisionForce;
        private SerializedProperty _eventBusId;
        private SerializedProperty _inputKey;
        private SerializedProperty _cooldown;
        private SerializedProperty _playOnce;
        private SerializedProperty _useObjectPosition;
        private SerializedProperty _stopPrevious;
        
        private void OnEnable()
        {
            _soundId = serializedObject.FindProperty("soundId");
            _volume = serializedObject.FindProperty("volume");
            _pitchVariation = serializedObject.FindProperty("pitchVariation");
            _trigger = serializedObject.FindProperty("trigger");
            _targetTag = serializedObject.FindProperty("targetTag");
            _minCollisionForce = serializedObject.FindProperty("minCollisionForce");
            _eventBusId = serializedObject.FindProperty("eventBusId");
            _inputKey = serializedObject.FindProperty("inputKey");
            _cooldown = serializedObject.FindProperty("cooldown");
            _playOnce = serializedObject.FindProperty("playOnce");
            _useObjectPosition = serializedObject.FindProperty("useObjectPosition");
            _stopPrevious = serializedObject.FindProperty("stopPrevious");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Sound section
            EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_soundId);
            EditorGUILayout.PropertyField(_volume);
            EditorGUILayout.PropertyField(_pitchVariation);
            
            EditorGUILayout.Space(5);
            
            // Trigger section
            EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_trigger);
            
            // Показать релевантные настройки в зависимости от триггера
            var triggerType = (SoundTrigger)_trigger.enumValueIndex;
            
            EditorGUI.indentLevel++;
            
            switch (triggerType)
            {
                case SoundTrigger.CollisionEnter:
                case SoundTrigger.CollisionExit:
                    EditorGUILayout.PropertyField(_targetTag, new GUIContent("Tag Filter"));
                    if (triggerType == SoundTrigger.CollisionEnter)
                    {
                        EditorGUILayout.PropertyField(_minCollisionForce);
                    }
                    break;
                    
                case SoundTrigger.TriggerEnter:
                case SoundTrigger.TriggerExit:
                    EditorGUILayout.PropertyField(_targetTag, new GUIContent("Tag Filter"));
                    break;
                    
                case SoundTrigger.KeyDown:
                case SoundTrigger.KeyUp:
                    EditorGUILayout.PropertyField(_inputKey, new GUIContent("Key"));
                    break;
                    
                case SoundTrigger.EventBus:
                    EditorGUILayout.PropertyField(_eventBusId, new GUIContent("Event ID"));
                    break;
                    
                case SoundTrigger.Manual:
                    EditorGUILayout.HelpBox("Call Play() method from script or UnityEvent", MessageType.Info);
                    break;
            }
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(5);
            
            // Conditions section
            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_cooldown);
            EditorGUILayout.PropertyField(_playOnce);
            EditorGUILayout.PropertyField(_useObjectPosition, new GUIContent("3D Position"));
            
            EditorGUILayout.Space(5);
            
            // Advanced section
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_stopPrevious);
            
            // Play button in Play Mode
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button("▶ Play Sound", GUILayout.Height(24)))
                {
                    ((PlaySoundOn)target).Play();
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
