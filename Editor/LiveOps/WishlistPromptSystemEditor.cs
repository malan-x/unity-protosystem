// Packages/com.protosystem.core/Editor/LiveOps/WishlistPromptSystemEditor.cs
using UnityEditor;
using UnityEngine;
using ProtoSystem.LiveOps;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Инспектор панели вишлиста: показывает состояние и даёт его сбросить.
    ///
    /// Панель по замыслу одноразовая — нажал любую из двух кнопок, и она молчит
    /// навсегда. Проверять её из-за этого неудобно: состояние лежит в
    /// PlayerPrefs, то есть своё на каждой машине и общее у редактора с билдом.
    /// Кнопка сброса избавляет от ковыряния в реестре.
    /// </summary>
    [CustomEditor(typeof(WishlistPromptSystem))]
    public class WishlistPromptSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (WishlistPromptSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Состояние показа", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                system.IsDecided
                    ? "Игрок уже нажал «Добавить» или «Уже добавил» — панель больше не появится."
                    : $"Показов сделано: {system.ShownCount}. Решение не принято.",
                system.IsDecided ? MessageType.Info : MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Сбросить показы и решение"))
                {
                    system.ResetPromptState();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Показать сейчас"))
                        system.ShowNow();
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.LabelField("«Показать сейчас» — только в Play Mode", EditorStyles.miniLabel);

            // Состояние живёт в PlayerPrefs и меняется в рантайме — иначе панель
            // в инспекторе показывала бы цифры на момент открытия
            if (Application.isPlaying) Repaint();
        }
    }
}
