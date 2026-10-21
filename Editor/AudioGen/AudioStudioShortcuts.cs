using ProtoSystem.Sound;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Мостики «SoundLibrary → ИИ Аудио студия»: контекстное меню ассета библиотеки
    /// и переход от выделенного AudioClip к его звуковой сущности (реверс-поиск по id).
    /// </summary>
    [InitializeOnLoad]
    public static class AudioStudioShortcuts
    {
        static AudioStudioShortcuts()
        {
            // ПКМ по ЛЮБОМУ полю записи в инспекторе SoundLibrary → переход к сущности.
            // Глобальный хук свойств — пакетный инспектор библиотеки не трогаем.
            EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;
        }

        private static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
        {
            if (property.serializedObject.targetObject is not SoundLibrary library) return;

            var m = System.Text.RegularExpressions.Regex.Match(
                property.propertyPath, @"coreEntries\.Array\.data\[(\d+)\]");
            if (!m.Success) return;

            int index = int.Parse(m.Groups[1].Value);
            if (index < 0 || index >= library.coreEntries.Count) return;

            string id = library.coreEntries[index]?.id;
            if (string.IsNullOrEmpty(id)) return;

            menu.AddItem(new GUIContent($"Открыть «{id}» в ИИ Аудио студии"), false,
                () => AudioStudioWindow.OpenAt(id));
        }

        [MenuItem("CONTEXT/SoundLibrary/Открыть в ИИ Аудио студии")]
        private static void OpenStudioFromLibrary(MenuCommand cmd) => AudioStudioWindow.Open();

        [MenuItem("Assets/ProtoSystem/Найти звук в ИИ Аудио студии", true)]
        private static bool ValidateFindInStudio() => Selection.activeObject is AudioClip;

        /// <summary>Выделен клип (напр., из поля записи SoundLibrary) → прыжок к сущности в студии.</summary>
        [MenuItem("Assets/ProtoSystem/Найти звук в ИИ Аудио студии")]
        private static void FindInStudio()
        {
            if (Selection.activeObject is not AudioClip clip) return;

            var library = AudioVariantStore.SoundLibrary;
            if (library == null) return;

            foreach (var e in library.coreEntries)
            {
                if (e == null) continue;
                bool match = e.clip == clip;
                if (!match && e.clipVariants != null)
                    foreach (var v in e.clipVariants)
                        if (v == clip) { match = true; break; }

                if (match)
                {
                    AudioStudioWindow.OpenAt(e.id);
                    return;
                }
            }

            AudioStudioWindow.Open();
            EditorUtility.DisplayDialog("ИИ Аудио студия",
                $"Клип «{clip.name}» не найден в SoundLibrary — открыта студия без перехода.", "Ок");
        }
    }
}
