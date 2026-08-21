using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Нелинейный масштаб ШРИФТОВ (настройка «Масштаб интерфейса», крупный текст
    /// на Steam Deck). В отличие от PanelSettings.scale не трогает раскладку и
    /// действует с затуханием: мелкий текст растёт на полный множитель, крупные
    /// заголовки — почти нет. Логика: заголовки и так читаются, а равномерный
    /// масштаб раздувает их первыми, и текст съедает весь экран.
    ///
    ///   буст(size) = lerp(Scale, 1, InverseLerp(SmallRef, BigRef, size))
    ///
    /// Применение: UIToolkitWindowBase после построения окна запускает
    /// периодический Apply(root) — он же подхватывает элементы, созданные
    /// кодом позже (карточки, списки). Исходный размер каждого элемента
    /// запоминается в слабой таблице, повторные проходы идемпотентны.
    /// </summary>
    public static class UIFontScaler
    {
        /// <summary>Полный множитель для самых мелких шрифтов. 1 — выключено.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>Размер, до которого действует полный множитель.</summary>
        public const float SmallRef = 14f;

        /// <summary>Размер, с которого множитель сходит на нет (заголовки не трогаем).</summary>
        public const float BigRef = 30f;

        // Исходные размеры: слабые ссылки — пул окон пересоздаёт деревья,
        // таблица чистится сборщиком сама
        private static readonly ConditionalWeakTable<TextElement, StrongBox<float>> _baseSizes = new();

        public static void SetScale(float scale)
        {
            Scale = Mathf.Clamp(scale, 0.5f, 2f);
        }

        /// <summary>Применить масштаб ко всем текстовым элементам поддерева.</summary>
        public static void Apply(VisualElement root)
        {
            if (root == null || root.panel == null) return;

            // Масштаб 1 = выключено: НИЧЕГО не кэшируем и снимаем свои inline-стили —
            // они перекрывают USS, и любая ошибка кэша иначе прибивает размеры навсегда
            bool off = Mathf.Abs(Scale - 1f) < 0.005f;
            if (off)
            {
                root.Query<TextElement>().ForEach(RestoreOriginal);
                return;
            }

            root.Query<TextElement>().ForEach(ApplyTo);
        }

        private static void RestoreOriginal(TextElement el)
        {
            if (!_baseSizes.TryGetValue(el, out _)) return;
            el.style.fontSize = StyleKeyword.Null; // вернуть размер из USS
            _baseSizes.Remove(el);
        }

        private static void ApplyTo(TextElement el)
        {
            // До первого layout resolvedStyle отдаёт дефолт вместо размера из USS —
            // кэшировать его нельзя (этим и была сломана первая версия)
            if (float.IsNaN(el.layout.width)) return;

            float baseSize;
            if (_baseSizes.TryGetValue(el, out var box))
            {
                baseSize = box.Value;
            }
            else
            {
                baseSize = el.resolvedStyle.fontSize;
                if (baseSize <= 0f) return; // стиль ещё не разрешён — возьмём следующим проходом
                _baseSizes.Add(el, new StrongBox<float>(baseSize));
            }

            float t = Mathf.InverseLerp(SmallRef, BigRef, baseSize);
            float target = baseSize * Mathf.Lerp(Scale, 1f, t);

            if (Mathf.Abs(el.resolvedStyle.fontSize - target) > 0.1f)
                el.style.fontSize = target;
        }
    }
}
