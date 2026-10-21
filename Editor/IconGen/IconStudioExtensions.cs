using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Точки расширения ИИ Арт студии для проекта. Студия живёт в пакете и не знает типов игры —
    /// проект регистрирует свои надстройки из [InitializeOnLoad]-класса:
    ///
    ///   IconStudioExtensions.PlayModeArtChanged += () => EventBus.Publish(...);
    ///   IconStudioExtensions.MoreMenuItems.Add(("Переключатель паков", MySwitcher.Show));
    ///   IconStudioExtensions.RenditionPanelExtras.Add(col => ...);
    /// </summary>
    public static class IconStudioExtensions
    {
        /// <summary>
        /// Активный арт поменялся в Play Mode (клик по варианту, пак, настройки рендишенов) —
        /// проекту пора перерисовать игровой UI, который держит старые спрайты.
        /// </summary>
        public static event Action PlayModeArtChanged;

        /// <summary>Дополнительные пункты меню «⋯» студии: (путь пункта, действие).</summary>
        public static readonly List<(string label, Action action)> MoreMenuItems = new();

        /// <summary>
        /// Дополнительные блоки панели рендишенов набора. Колбэк получает набор и возвращает
        /// элемент (или null, если набору нечего показать).
        /// </summary>
        public static readonly List<Func<IconCollection, VisualElement>> RenditionPanelExtras = new();

        internal static void RaisePlayModeArtChanged() => PlayModeArtChanged?.Invoke();
    }
}
