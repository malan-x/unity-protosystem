using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Контейнер эффектов для группировки и организации.
    /// Содержит коллекцию EffectConfig для использования в EffectsManagerSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Effects/Effect Container", fileName = "EffectContainer")]
    public class EffectContainer : ScriptableObject
    {
        [Header("Контейнер эффектов")]
        [Tooltip("Название контейнера для удобства")]
        [SerializeField] private string containerName = "New Effect Container";

        [Tooltip("Описание контейнера")]
        [SerializeField, TextArea] private string description = "";

        [Header("Эффекты")]
        [Tooltip("Список эффектов в этом контейнере")]
        [SerializeField] private List<EffectConfig> effects = new();

        /// <summary>
        /// Название контейнера
        /// </summary>
        public string ContainerName
        {
            get => containerName;
            set => containerName = value;
        }

        /// <summary>
        /// Описание контейнера
        /// </summary>
        public string Description
        {
            get => description;
            set => description = value;
        }

        /// <summary>
        /// Список эффектов (только для чтения)
        /// </summary>
        public IReadOnlyList<EffectConfig> Effects => effects;

        /// <summary>
        /// Количество эффектов в контейнере
        /// </summary>
        public int Count => effects.Count;

        /// <summary>
        /// Получить эффект по индексу
        /// </summary>
        public EffectConfig this[int index] => effects[index];

        /// <summary>
        /// Проверить, содержит ли контейнер эффект с указанным ID
        /// </summary>
        public bool ContainsEffect(string effectId)
        {
            return effects.Exists(config => config.effectId == effectId);
        }

        /// <summary>
        /// Найти эффект по ID
        /// </summary>
        public EffectConfig FindEffect(string effectId)
        {
            return effects.Find(config => config.effectId == effectId);
        }

        /// <summary>
        /// Добавить эффект в контейнер
        /// </summary>
        public void AddEffect(EffectConfig effect)
        {
            if (effect != null && !effects.Contains(effect))
            {
                effects.Add(effect);
            }
        }

        /// <summary>
        /// Удалить эффект из контейнера
        /// </summary>
        public void RemoveEffect(EffectConfig effect)
        {
            if (effect != null)
            {
                effects.Remove(effect);
            }
        }

        /// <summary>
        /// Найти эффекты по тегу
        /// </summary>
        public List<EffectConfig> FindEffectsByTag(string tag)
        {
            return effects.FindAll(config => config.HasTag(tag));
        }

        /// <summary>
        /// Проверить, содержит ли контейнер эффекты с указанным тегом
        /// </summary>
        public bool ContainsEffectsWithTag(string tag)
        {
            return effects.Exists(config => config.HasTag(tag));
        }

        /// <summary>
        /// Получить все уникальные теги в контейнере
        /// </summary>
        public HashSet<string> GetAllTags()
        {
            var allTags = new HashSet<string>();
            foreach (var effect in effects)
            {
                if (effect.tags != null)
                {
                    foreach (var tag in effect.tags)
                    {
                        allTags.Add(tag.ToLowerInvariant());
                    }
                }
            }
            return allTags;
        }

        /// <summary>
        /// Проверить валидность контейнера
        /// </summary>
        public bool IsValid()
        {
            if (effects == null) return false;

            // Проверить уникальность ID эффектов
            var ids = new HashSet<string>();
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (!ids.Add(effect.effectId))
                {
                    ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Дублированный ID эффекта: {effect.effectId} в контейнере '{containerName}'");
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            // Автоматическая валидация при изменении в Inspector
            if (!IsValid())
            {
                ProtoLogger.Log("effects_manager", LogCategory.Runtime, LogLevel.Warnings, $"Контейнер '{containerName}' содержит ошибки!");
            }
        }
    }
}
