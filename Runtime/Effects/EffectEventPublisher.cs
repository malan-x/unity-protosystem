using System;
using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Данные события с информацией для спавна эффекта.
    /// Используется для автоматического создания эффектов при публикации событий.
    /// </summary>
    public class EffectEventData
    {
        /// <summary>Данные для спавна эффекта</summary>
        public EffectSpawnData SpawnData { get; }
        
        /// <summary>Источник события (опционально)</summary>
        public object Source { get; }

        public EffectEventData(EffectSpawnData spawnData, object source = null)
        {
            SpawnData = spawnData;
            Source = source;
        }

        /// <summary>
        /// Создаёт данные события из IEffectTarget
        /// </summary>
        public static EffectEventData FromTarget(IEffectTarget target, object source = null)
        {
            return new EffectEventData(target.GetEffectSpawnData(), source);
        }

        /// <summary>
        /// Создаёт данные события из IEffectTarget с указанием точки привязки
        /// </summary>
        public static EffectEventData FromTarget(IEffectTarget target, string attachPoint, object source = null)
        {
            return new EffectEventData(target.GetEffectSpawnData(attachPoint), source);
        }

        /// <summary>
        /// Создаёт данные события для позиции в мировых координатах
        /// </summary>
        public static EffectEventData AtPosition(Vector3 position, object source = null)
        {
            return new EffectEventData(EffectSpawnData.AtPosition(position), source);
        }

        /// <summary>
        /// Создаёт данные события для Transform
        /// </summary>
        public static EffectEventData AttachedTo(Transform target, Vector3 localOffset = default, object source = null)
        {
            return new EffectEventData(EffectSpawnData.AttachedTo(target, localOffset), source);
        }
    }

    /// <summary>
    /// Утилиты для публикации событий с данными эффектов
    /// </summary>
    public static class EffectEventPublisher
    {
        /// <summary>
        /// Публикует событие с данными для эффекта от IEffectTarget
        /// </summary>
        /// <param name="eventId">ID события (из Evt.*)</param>
        /// <param name="target">Объект-источник эффекта</param>
        /// <param name="attachPoint">Точка привязки (опционально)</param>
        public static void Publish(int eventId, IEffectTarget target, string attachPoint = null)
        {
            EffectEventData data;
            
            if (string.IsNullOrEmpty(attachPoint))
            {
                data = EffectEventData.FromTarget(target);
            }
            else
            {
                data = EffectEventData.FromTarget(target, attachPoint);
            }

            EventBus.Publish(eventId, data);
        }

        /// <summary>
        /// Публикует событие с данными для эффекта по позиции
        /// </summary>
        public static void PublishAtPosition(int eventId, Vector3 position)
        {
            var data = EffectEventData.AtPosition(position);
            EventBus.Publish(eventId, data);
        }

        /// <summary>
        /// Публикует событие с данными для эффекта, привязанного к Transform
        /// </summary>
        public static void PublishAttachedTo(int eventId, Transform target, Vector3 localOffset = default)
        {
            var data = EffectEventData.AttachedTo(target, localOffset);
            EventBus.Publish(eventId, data);
        }

        /// <summary>
        /// Публикует событие без данных эффекта (для Screen эффектов и глобальных звуков)
        /// </summary>
        public static void PublishGlobal(int eventId, object customData = null)
        {
            EventBus.Publish(eventId, customData);
        }
    }

    /// <summary>
    /// Расширения для MonoBehaviour для удобной публикации событий с эффектами
    /// </summary>
    public static class EffectTargetExtensions
    {
        /// <summary>
        /// Публикует событие с данными эффекта, если объект реализует IEffectTarget
        /// </summary>
        public static void PublishEffectEvent(this MonoBehaviour source, int eventId, string attachPoint = null)
        {
            if (source is IEffectTarget target)
            {
                EffectEventPublisher.Publish(eventId, target, attachPoint);
            }
            else
            {
                // Пробуем найти IEffectTarget на объекте
                var effectTarget = source.GetComponent<IEffectTarget>();
                if (effectTarget != null)
                {
                    EffectEventPublisher.Publish(eventId, effectTarget, attachPoint);
                }
                else
                {
                    // Fallback: используем transform
                    EffectEventPublisher.PublishAttachedTo(eventId, source.transform);
                }
            }
        }

        /// <summary>
        /// Публикует событие в мировых координатах
        /// </summary>
        public static void PublishEffectEventAtPosition(this MonoBehaviour source, int eventId, Vector3 position)
        {
            EffectEventPublisher.PublishAtPosition(eventId, position);
        }

        /// <summary>
        /// Публикует глобальное событие (для Screen эффектов)
        /// </summary>
        public static void PublishGlobalEffectEvent(this MonoBehaviour source, int eventId, object data = null)
        {
            EffectEventPublisher.PublishGlobal(eventId, data);
        }
    }
}
