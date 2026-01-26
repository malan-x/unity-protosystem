using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Базовый MonoBehaviour класс с автоматической работой с EventBus
    /// Автоматически подписывается на события при включении и отписывается при отключении
    /// </summary>
    public abstract class MonoEventBus : MonoBehaviour
    {
        /// <summary>
        /// Список событий для автоматической подписки/отписки
        /// </summary>
        internal List<(int id, Action<object> action)> events = new List<(int, Action<object>)>();

        #region Unity Lifecycle

        /// <summary>
        /// Инициализация событий в Awake
        /// </summary>
        protected virtual void Awake()
        {
            try
            {
                InitEvents();
            }
            catch (Exception e)
            {
                ProtoLogger.LogError(GetType().Name, $"Error in InitEvents: {e.Message}");
            }
        }

        /// <summary>
        /// Автоматическая подписка при включении объекта
        /// </summary>
        protected virtual void OnEnable()
        {
            try
            {
                SubscribeEvents();
            }
            catch (Exception e)
            {
                ProtoLogger.LogError(GetType().Name, $"Error in SubscribeEvents: {e.Message}");
            }
        }

        /// <summary>
        /// Автоматическая отписка при отключении объекта
        /// </summary>
        protected virtual void OnDisable()
        {
            try
            {
                UnsubscribeEvents();
            }
            catch (Exception e)
            {
                ProtoLogger.LogError(GetType().Name, $"Error in UnsubscribeEvents: {e.Message}");
            }
        }

        #endregion

        #region IEventBus Implementation

        /// <summary>
        /// Инициализация событий - должна быть переопределена в наследниках
        /// </summary>
        protected abstract void InitEvents();

        /// <summary>
        /// Подписка на все события
        /// </summary>
        public virtual void SubscribeEvents()
        {
            foreach (var eventItem in events)
            {
                EventBus.Subscribe(eventItem.id, eventItem.action, false);
            }
        }

        /// <summary>
        /// Отписка от всех событий
        /// </summary>
        public virtual void UnsubscribeEvents()
        {
            foreach (var eventItem in events)
            {
                EventBus.Unsubscribe(eventItem.id, eventItem.action, false);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Добавить событие в список для автоматической подписки
        /// </summary>
        /// <param name="eventId">ID события</param>
        /// <param name="handler">Обработчик события</param>
        protected void AddEvent(int eventId, Action<object> handler)
        {
            events.Add((eventId, handler));
        }

        /// <summary>
        /// Удалить событие из списка
        /// </summary>
        /// <param name="eventId">ID события</param>
        /// <param name="handler">Обработчик события</param>
        protected void RemoveEvent(int eventId, Action<object> handler)
        {
            events.RemoveAll(e => e.id == eventId && e.action == handler);
        }

        /// <summary>
        /// Проверить, подписаны ли мы на определенное событие
        /// </summary>
        /// <param name="eventId">ID события</param>
        /// <returns>True если подписаны</returns>
        protected bool IsSubscribedTo(int eventId)
        {
            return events.Exists(e => e.id == eventId);
        }

        /// <summary>
        /// Получить количество подписанных событий
        /// </summary>
        /// <returns>Количество событий</returns>
        protected int GetSubscribedEventsCount()
        {
            return events.Count;
        }

        /// <summary>
        /// Очистить все события (осторожно!)
        /// </summary>
        protected void ClearAllEvents()
        {
            UnsubscribeEvents();
            events.Clear();
        }

        #endregion

        #region Debug Support

#if UNITY_EDITOR
        /// <summary>
        /// Отобразить информацию о подписанных событиях в инспекторе
        /// </summary>
        [ContextMenu("Show Subscribed events")]
        private void ShowSubscribedEvents()
        {
            ProtoLogger.LogEvent(GetType().Name, $"Subscribed to {events.Count} events:");
            for (int i = 0; i < events.Count; i++)
            {
                var eventItem = events[i];
                string eventPath = EventBus.GetEventPath(eventItem.id);
                string methodName = eventItem.action?.Method?.Name ?? "Unknown";
                ProtoLogger.LogEvent(GetType().Name, $"  {i + 1}. {eventPath} -> {methodName}");
            }
        }

        /// <summary>
        /// Принудительная переподписка на события
        /// </summary>
        [ContextMenu("Force Resubscribe")]
        private void ForceResubscribe()
        {
            UnsubscribeEvents();
            SubscribeEvents();
            ProtoLogger.LogEvent(GetType().Name, $"Force resubscribed to {events.Count} events");
        }
#endif

        #endregion
    }
}
