using System;
using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Режим пространства для эффекта.
    /// Все эффекты используют пул, но отличаются привязкой.
    /// </summary>
    public enum EffectSpaceMode
    {
        /// <summary>Эффект в мировых координатах (остаётся в пуле)</summary>
        WorldSpace,
        /// <summary>Эффект привязывается к цели (временно выходит из пула, возвращается после завершения)</summary>
        LocalSpace
    }

    /// <summary>
    /// Категория эффекта для фильтрации событий в редакторе
    /// </summary>
    public enum EffectCategory
    {
        /// <summary>VFX/Particle эффект — требует IEffectTarget для позиции</summary>
        Spatial,
        /// <summary>Звуковой эффект — может быть пространственным или глобальным</summary>
        Audio,
        /// <summary>UI/Screen эффект — не требует позиции</summary>
        Screen
    }

    /// <summary>
    /// Тип анимации появления/исчезновения UI
    /// </summary>
    public enum UIAnimationType
    {
        None,
        Scale,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        ScaleAndFade,
        Bounce,
        Rotate
    }

    /// <summary>
    /// Тип easing для анимаций
    /// </summary>
    public enum UIEaseType
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseInBack,
        EaseOutBack,
        EaseInOutBack,
        EaseOutBounce,
        EaseOutElastic
    }

    /// <summary>
    /// ScriptableObject для конфигурации эффектов геймдизайнерами.
    /// Позволяет добавлять/модифицировать эффекты без кода.
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Effects/Effect Config", fileName = "NewEffectConfig")]
    public class EffectConfig : ScriptableObject
    {
        [Header("Основные настройки")]
        [Tooltip("Уникальный ID эффекта для идентификации")]
        public string effectId;

        [Tooltip("Отображаемое имя эффекта")]
        public string displayName;

        [Tooltip("Описание эффекта для геймдизайнеров")]
        [TextArea] public string description;

        [Header("Тип эффекта")]
        [Tooltip("Тип эффекта (VFX, Audio, UI и т.д.)")]
        public EffectType effectType;

        [Header("VFX настройки (если effectType == VFX)")]
        [Tooltip("Префаб эффекта (ParticleSystem, VFX Graph и т.д.)")]
        public GameObject vfxPrefab;

        [Tooltip("Позиция эффекта относительно цели")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("Поворот эффекта")]
        public Vector3 rotation = Vector3.zero;

        [Tooltip("Масштаб эффекта")]
        public Vector3 scale = Vector3.one;

        [Tooltip("Время жизни эффекта (0 = бесконечно)")]
        public float lifetime = 2f;

        [Header("Audio настройки (если effectType == Audio)")]
        [Tooltip("Клип для воспроизведения")]
        public AudioClip audioClip;

        [Tooltip("Громкость эффекта")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Тон эффекта")]
        [Range(0.1f, 3f)] public float pitch = 1f;

        [Tooltip("Пространственный звук")]
        public bool spatial = false;

        [Header("UI настройки (если effectType == UI)")]
        [Tooltip("UI префаб для отображения")]
        public GameObject uiPrefab;

        [Tooltip("Позиция UI эффекта")]
        public Vector2 uiPosition;

        [Tooltip("Масштаб UI эффекта")]
        public Vector3 uiScale = Vector3.one;

        [Tooltip("Время отображения UI эффекта")]
        public float uiDisplayTime = 1f;

        [Header("UI Анимация появления")]
        [Tooltip("Тип анимации появления")]
        public UIAnimationType uiShowAnimation = UIAnimationType.None;

        [Tooltip("Длительность анимации появления")]
        public float uiShowDuration = 0.3f;

        [Tooltip("Easing для анимации появления")]
        public UIEaseType uiShowEase = UIEaseType.EaseOutBack;

        [Header("UI Анимация исчезновения")]
        [Tooltip("Тип анимации исчезновения")]
        public UIAnimationType uiHideAnimation = UIAnimationType.None;

        [Tooltip("Длительность анимации исчезновения")]
        public float uiHideDuration = 0.2f;

        [Tooltip("Easing для анимации исчезновения")]
        public UIEaseType uiHideEase = UIEaseType.EaseInQuad;

        [Header("Общие настройки")]
        [Tooltip("Может ли эффект быть прерван")]
        public bool canBeInterrupted = true;

        [Tooltip("Приоритет эффекта (выше = важнее)")]
        public int priority = 0;

        [Tooltip("Тэги для фильтрации эффектов")]
        public string[] tags;

        [Header("Режим пространства")]
        [Tooltip("WorldSpace = эффект в мировых координатах, LocalSpace = привязан к цели")]
        public EffectSpaceMode spaceMode = EffectSpaceMode.WorldSpace;

        [Tooltip("Категория эффекта (влияет на требования к данным события)")]
        public EffectCategory category = EffectCategory.Spatial;

        [Tooltip("Имя кости для привязки (опционально, если LocalSpace)")]
        public string attachBoneName = "";

        [Tooltip("Смещение относительно точки привязки")]
        public Vector3 localOffset = Vector3.zero;

        [Header("Автоматический триггер (опционально)")]
        [Tooltip("Включить автоматический запуск эффекта по событиям EventBus")]
        public bool autoTrigger = false;

        [Tooltip("Текстовый путь события для запуска (например, 'Боевка.Урон_нанесен')")]
        public string triggerEventPath = "";

        [Tooltip("Текстовый путь события для остановки (например, 'Система.Система_инициализирована')")]
        public string stopEventPath = "";

        [Tooltip("Условие для триггера (опционально, для фильтрации данных события)")]
        public string triggerCondition;

        [Tooltip("Передавать данные события для определения позиции эффекта")]
        public bool passEventData = true;

        [Tooltip("Включить автоматическую остановку эффекта по событиям EventBus")]
        public bool autoStop = false;

        // Runtime-кешированные ID событий (заполняются при инициализации)
        [NonSerialized] private int _cachedTriggerEventId = -1;
        [NonSerialized] private int _cachedStopEventId = -1;

        // Legacy поля для обратной совместимости (помечены как Obsolete)
        [HideInInspector]
        [Obsolete("Используйте triggerEventPath вместо triggerEventId")]
        public int triggerEventId = 0;

        [HideInInspector]
        [Obsolete("Используйте stopEventPath вместо stopEventId")]
        public int stopEventId = 0;

        public enum EffectType
        {
            VFX,
            Audio,
            UI,
            ScreenEffect,
            Particle,
            Combined
        }

        /// <summary>
        /// Валидация конфигурации
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(effectId))
            {
                effectId = name.Replace(" ", "").ToLower();
            }

            // Валидация по типу
            switch (effectType)
            {
                case EffectType.VFX:
                    if (vfxPrefab == null)
                        ProtoLogger.Log("EffectsSystem", LogCategory.Runtime, LogLevel.Warnings, $"VFX эффект без префаба: {name}");
                    break;
                case EffectType.Audio:
                    if (audioClip == null)
                        ProtoLogger.Log("EffectsSystem", LogCategory.Runtime, LogLevel.Warnings, $"Audio эффект без клипа: {name}");
                    break;
                case EffectType.UI:
                    if (uiPrefab == null)
                        ProtoLogger.Log("EffectsSystem", LogCategory.Runtime, LogLevel.Warnings, $"UI эффект без префаба: {name}");
                    break;
            }
        }

        /// <summary>
        /// Проверка, содержит ли эффект указанный тэг
        /// </summary>
        public bool HasTag(string tag)
        {
            if (tags == null) return false;
            return Array.Exists(tags, t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Проверка, настроен ли автоматический триггер
        /// </summary>
        public bool HasAutoTrigger()
        {
            return autoTrigger && !string.IsNullOrEmpty(triggerEventPath);
        }

        /// <summary>
        /// Проверка, настроена ли автоматическая остановка
        /// </summary>
        public bool HasAutoStop()
        {
            return autoTrigger && autoStop && !string.IsNullOrEmpty(stopEventPath);
        }

        /// <summary>
        /// Получает ID события запуска (кешируется при первом вызове)
        /// </summary>
        public int GetTriggerEventId()
        {
            if (_cachedTriggerEventId < 0)
            {
                _cachedTriggerEventId = EventPathResolver.Resolve(triggerEventPath);
            }
            return _cachedTriggerEventId;
        }

        /// <summary>
        /// Получает ID события остановки (кешируется при первом вызове)
        /// </summary>
        public int GetStopEventId()
        {
            if (_cachedStopEventId < 0)
            {
                _cachedStopEventId = EventPathResolver.Resolve(stopEventPath);
            }
            return _cachedStopEventId;
        }

        /// <summary>
        /// Сбрасывает кеш ID событий (вызывать при изменении путей)
        /// </summary>
        public void InvalidateEventCache()
        {
            _cachedTriggerEventId = -1;
            _cachedStopEventId = -1;
        }

        /// <summary>
        /// Проверка условия триггера (если указано)
        /// </summary>
        public bool CheckTriggerCondition(object eventData)
        {
            if (string.IsNullOrEmpty(triggerCondition)) return true;

            // Простая проверка по типу данных (можно расширить)
            if (eventData == null) return false;

            // Для примера: проверка по имени типа
            return eventData.GetType().Name.Contains(triggerCondition) ||
                   eventData.ToString().Contains(triggerCondition);
        }

        /// <summary>
        /// Валидация конфигурации эффекта
        /// </summary>
        public bool IsValid()
        {
            var errors = GetValidationErrors();
            return string.IsNullOrEmpty(errors);
        }

        /// <summary>
        /// Получение списка ошибок валидации
        /// </summary>
        public string GetValidationErrors()
        {
            var errors = new System.Text.StringBuilder();

            if (string.IsNullOrEmpty(effectId))
                errors.AppendLine("- ID эффекта не указан");

            if (string.IsNullOrEmpty(displayName))
                errors.AppendLine("- Отображаемое имя не указано");

            // Валидация по типу эффекта
            switch (effectType)
            {
                case EffectType.VFX:
                case EffectType.Particle:
                    if (vfxPrefab == null)
                        errors.AppendLine("- VFX префаб не назначен");
                    break;

                case EffectType.Audio:
                    if (audioClip == null)
                        errors.AppendLine("- Аудио клип не назначен");
                    break;

                case EffectType.UI:
                case EffectType.ScreenEffect:
                    if (uiPrefab == null)
                        errors.AppendLine("- UI префаб не назначен");
                    break;

                case EffectType.Combined:
                    // Для комбинированных эффектов проверяем хотя бы один компонент
                    if (vfxPrefab == null && audioClip == null && uiPrefab == null)
                        errors.AppendLine("- Для комбинированного эффекта должен быть назначен хотя бы один компонент");
                    break;
            }

            // Валидация автоматических триггеров
            if (autoTrigger && string.IsNullOrEmpty(triggerEventPath))
                errors.AppendLine("- Автоматический триггер включен, но путь события запуска не указан");

            if (autoTrigger && !string.IsNullOrEmpty(triggerEventPath) && !EventPathResolver.Exists(triggerEventPath))
                errors.AppendLine($"- Путь события запуска '{triggerEventPath}' не найден в Evt.*");

            if (autoStop && string.IsNullOrEmpty(stopEventPath))
                errors.AppendLine("- Автоматическая остановка включена, но путь события остановки не указан");

            if (autoStop && !string.IsNullOrEmpty(stopEventPath) && !EventPathResolver.Exists(stopEventPath))
                errors.AppendLine($"- Путь события остановки '{stopEventPath}' не найден в Evt.*");

            // Валидация категории и режима пространства
            if (category == EffectCategory.Spatial && spaceMode == EffectSpaceMode.LocalSpace && string.IsNullOrEmpty(attachBoneName))
            {
                // Это предупреждение, не ошибка — кость опциональна
            }

            return errors.ToString().Trim();
        }

        /// <summary>
        /// Проверяет, требует ли эффект данные IEffectTarget для спавна
        /// </summary>
        public bool RequiresEffectTarget()
        {
            // Spatial эффекты требуют позицию/цель
            // Audio может быть пространственным
            // Screen эффекты не требуют
            return category == EffectCategory.Spatial || 
                   (category == EffectCategory.Audio && spatial);
        }

        /// <summary>
        /// Авто-определение категории по типу эффекта
        /// </summary>
        public EffectCategory GetAutoCategory()
        {
            return effectType switch
            {
                EffectType.VFX => EffectCategory.Spatial,
                EffectType.Particle => EffectCategory.Spatial,
                EffectType.Audio => spatial ? EffectCategory.Audio : EffectCategory.Screen,
                EffectType.UI => EffectCategory.Screen,
                EffectType.ScreenEffect => EffectCategory.Screen,
                EffectType.Combined => EffectCategory.Spatial,
                _ => EffectCategory.Spatial
            };
        }
    }
}
