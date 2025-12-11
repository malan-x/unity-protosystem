using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Базовый NetworkBehaviour класс с автоматической работой с EventBus
    /// Аналог MonoEventBus для сетевых компонентов
    /// Автоматически подписывается на события при спавне и отписывается при деспавне
    /// </summary>
    public abstract class NetworkEventBus : NetworkBehaviour
    {
        /// <summary>
        /// Список событий для автоматической подписки/отписки
        /// Используем кортежи как в MonoEventBus для единообразия
        /// </summary>
        internal List<(int id, Action<object> action)> events = new List<(int, Action<object>)>();

        /// <summary>
        /// Флаг состояния подписки (для сетевых компонентов важно отслеживать)
        /// </summary>
        private bool eventsSubscribed = false;

        #region Network Lifecycle

        /// <summary>
        /// Инициализация при спавне объекта в сети
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            try
            {
                InitEvents();
                SubscribeEvents();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Error in OnNetworkSpawn: {e.Message}", this);
            }
        }

        /// <summary>
        /// Очистка при деспавне объекта из сети
        /// </summary>
        public override void OnNetworkDespawn()
        {
            try
            {
                UnsubscribeEvents();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Error in OnNetworkDespawn: {e.Message}", this);
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Автоматическая подписка при включении объекта (если он заспавнен)
        /// </summary>
        protected virtual void OnEnable()
        {
            if (IsSpawned && !eventsSubscribed)
            {
                try
                {
                    SubscribeEvents();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{GetType().Name}] Error in OnEnable: {e.Message}", this);
                }
            }
        }

        /// <summary>
        /// Автоматическая отписка при отключении объекта
        /// </summary>
        protected virtual void OnDisable()
        {
            if (eventsSubscribed)
            {
                try
                {
                    UnsubscribeEvents();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{GetType().Name}] Error in OnDisable: {e.Message}", this);
                }
            }
        }

        /// <summary>
        /// Финальная очистка при уничтожении объекта
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (eventsSubscribed)
            {
                UnsubscribeEvents();
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
            if (eventsSubscribed) return;

            foreach (var eventItem in events)
            {
                EventBus.Subscribe(eventItem.id, eventItem.action, false);
            }

            eventsSubscribed = true;
        }

        /// <summary>
        /// Отписка от всех событий
        /// </summary>
        public virtual void UnsubscribeEvents()
        {
            if (!eventsSubscribed) return;

            foreach (var eventItem in events)
            {
                EventBus.Unsubscribe(eventItem.id, eventItem.action, false);
            }

            eventsSubscribed = false;
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

        #region Network Specific Methods

        /// <summary>
        /// Проверить, готов ли объект к работе с событиями
        /// </summary>
        /// <returns>True если объект заспавнен и готов</returns>
        protected bool IsReadyForEvents()
        {
            return IsSpawned && eventsSubscribed;
        }

        /// <summary>
        /// Отправить событие только на сервере
        /// </summary>
        protected void PublishEventServerOnly(int eventId, object data = null, bool log = false)
        {
            if (IsServer)
            {
                EventBus.Publish(eventId, data, log);
            }
        }

        /// <summary>
        /// Отправить событие только на клиенте
        /// </summary>
        protected void PublishEventClientOnly(int eventId, object data = null, bool log = false)
        {
            if (IsClient && !IsServer)
            {
                EventBus.Publish(eventId, data, log);
            }
        }

        /// <summary>
        /// Отправить событие только если это локальный игрок
        /// </summary>
        protected void PublishEventIfLocalPlayer(int eventId, object data = null, bool log = false)
        {
            if (IsLocalPlayer)
            {
                EventBus.Publish(eventId, data, log);
            }
        }

        #endregion

        #region Debug Support

#if UNITY_EDITOR
        /// <summary>
        /// Отобразить информацию о подписанных событиях в инспекторе
        /// </summary>
        [ContextMenu("Show Subscribed Events")]
        private void ShowSubscribedEvents()
        {
            Debug.Log($"[{GetType().Name}] Subscribed to {events.Count} events:");
            Debug.Log($"  Network State: IsSpawned={IsSpawned}, IsServer={IsServer}, IsClient={IsClient}, IsLocalPlayer={IsLocalPlayer}");
            Debug.Log($"  Events Subscribed: {eventsSubscribed}");

            for (int i = 0; i < events.Count; i++)
            {
                var eventItem = events[i];
                string eventPath = EventBus.GetEventPath(eventItem.id);
                string methodName = eventItem.action?.Method?.Name ?? "Unknown";
                Debug.Log($"  {i + 1}. {eventPath} -> {methodName}");
            }
        }

        /// <summary>
        /// Принудительная переподписка на события
        /// </summary>
        [ContextMenu("Force Resubscribe")]
        private void ForceResubscribe()
        {
            if (!IsSpawned)
            {
                Debug.LogWarning($"[{GetType().Name}] Cannot resubscribe - object is not spawned!");
                return;
            }

            UnsubscribeEvents();
            SubscribeEvents();
            Debug.Log($"[{GetType().Name}] Force resubscribed to {events.Count} events");
        }

        /// <summary>
        /// Отобразить сетевую информацию
        /// </summary>
        [ContextMenu("Show Network Info")]
        private void ShowNetworkInfo()
        {
            Debug.Log($"[{GetType().Name}] Network Info:");
            Debug.Log($"  NetworkObjectId: {NetworkObjectId}");
            Debug.Log($"  IsSpawned: {IsSpawned}");
            Debug.Log($"  IsServer: {IsServer}");
            Debug.Log($"  IsHost: {IsHost}");
            Debug.Log($"  IsClient: {IsClient}");
            Debug.Log($"  IsLocalPlayer: {IsLocalPlayer}");
            Debug.Log($"  IsOwner: {IsOwner}");
            Debug.Log($"  OwnerClientId: {OwnerClientId}");
        }
#endif

        #endregion
    }
}
