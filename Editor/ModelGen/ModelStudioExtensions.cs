using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Точки расширения ИИ 3D студии для проекта: мост из 3D в собственный 2D-пайплайн игры.
    /// Студия живёт в пакете и не знает типов игры — проект регистрирует надстройки из
    /// [InitializeOnLoad]-класса:
    ///
    ///   ModelStudioExtensions.EntityDetailsGUI.Add(item => { if (GUILayout.Button(...)) ... });
    ///   ModelStudioExtensions.AnimatedSheetRendered.Add((item, sheet, clips) => "Атлас назначен");
    /// </summary>
    public static class ModelStudioExtensions
    {
        /// <summary>
        /// IMGUI-блоки панели сущности, рисуются сразу под промптом. Колбэк получает выбранную
        /// сущность (можно читать/писать промпт через её делегаты).
        /// </summary>
        public static readonly List<Action<ModelContentItem>> EntityDetailsGUI = new();

        /// <summary>
        /// Анимированный спрайт-лист отрендерен (пункт меню варианта): сущность, текстура
        /// листа, имена клипов по рядам снизу вверх. Вернуть текст уведомления или null.
        /// </summary>
        public static readonly List<Func<ModelContentItem, Texture2D, string[], string>> AnimatedSheetRendered = new();
    }
}
