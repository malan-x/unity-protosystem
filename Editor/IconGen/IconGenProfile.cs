using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Профиль генерации — заведомо СОВМЕСТИМЫЙ набор «стиль + чекпоинт + LoRA» как ассет.
    /// Новичку достаточно переключать профили одним списком; отдельные поля (модель, LoRA)
    /// остаются для тонкой ручной настройки. Спасает от ловушек вида «Flux-чекпоинт
    /// с SDXL-параметрами стиля» — профиль собирает только то, что дружит между собой.
    ///
    /// Ассеты-профили лежат в проекте, пакет везёт только тип.
    /// </summary>
    [CreateAssetMenu(fileName = "IconGenProfile", menuName = "ProtoSystem/Icon Gen Profile")]
    public class IconGenProfile : ScriptableObject
    {
        [Tooltip("Для чего профиль и чего от него ожидать — показывается под выпадающим списком.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Стиль-пресет: шаблон промпта + параметры сэмплера, подобранные под модель ниже.")]
        public IconStylePreset style;

        [Tooltip("Файл чекпоинта в models/checkpoints (напр. juggernautXL_v9.safetensors).")]
        public string checkpoint;

        [Tooltip("Файл LoRA в models/loras. Пусто — без LoRA (для Flux — всегда пусто).")]
        public string lora = "";

        [Range(0f, 1.5f)]
        public float loraStrength = 0.65f;

        [Tooltip("Журнал неудачных вариантов этого профиля — пишется кнопкой «!» в витрине пака. " +
                 "Копится статистика: на каких сюжетах/сидах профиль лажает.")]
        public List<string> badNotes = new();

        public void AddBadNote(string note)
        {
            if (!badNotes.Contains(note)) badNotes.Add(note);
        }

        /// <summary>Снять пометку: убрать заметки этого варианта (id — в начале строки заметки).</summary>
        public void RemoveBadNotes(string variantId)
            => badNotes.RemoveAll(n => n != null && n.StartsWith("[" + variantId + "]"));

        /// <summary>Применить профиль: проставить стиль и модель в глобальные настройки генерации.</summary>
        public void Apply()
        {
            if (style != null) IconStyleRegistry.Default = style;
            if (!string.IsNullOrEmpty(checkpoint)) AiToolsSettings.Checkpoint = checkpoint;
            AiToolsSettings.LoraName = lora ?? "";
            if (!string.IsNullOrEmpty(lora)) AiToolsSettings.LoraStrength = loraStrength;
        }

        /// <summary>Совпадают ли текущие настройки с профилем (чтобы показать его выбранным в UI).</summary>
        public bool MatchesCurrent()
        {
            if (style != null && IconStyleRegistry.Default != style) return false;
            if (!string.IsNullOrEmpty(checkpoint) && AiToolsSettings.Checkpoint != checkpoint) return false;
            if (AiToolsSettings.LoraName != (lora ?? "")) return false;
            if (!string.IsNullOrEmpty(lora) &&
                !Mathf.Approximately(AiToolsSettings.LoraStrength, loraStrength)) return false;
            return true;
        }

        // Кэш: FindAssets на каждый кадр OnGUI (попапы профилей в двух окнах) — дорого.
        // Сбрасывается IconGenAssetWatcher'ом при изменении ассетов.
        private static List<IconGenProfile> _cache;

        public static void InvalidateCache() => _cache = null;

        /// <summary>Все профили проекта (кэшируется до изменения ассетов).</summary>
        public static List<IconGenProfile> All()
        {
            if (_cache != null)
            {
                _cache.RemoveAll(p => p == null);
                return _cache;
            }

            _cache = new List<IconGenProfile>();
            foreach (var guid in AssetDatabase.FindAssets("t:IconGenProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<IconGenProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null) _cache.Add(profile);
            }
            return _cache;
        }
    }
}
