using System;
using System.Collections.Generic;

namespace ProtoSystem
{
    /// <summary>
    /// Интерфейс для классов, которые не могут наследоваться от MonoEventBus,
    /// но хотят использовать автоматическую подписку/отписку на события
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Список событий для автоматической подписки/отписки
        /// </summary>
        List<(int id, Action<object> action)> events { get; set; }

        /// <summary>
        /// Инициализация событий - вызывать в Awake()
        /// </summary>
        void InitEvents();

        /// <summary>
        /// Подписка на все события - вызывать в OnEnable() или OnShow()
        /// </summary>
        void SubscribeEvents()
        {
            foreach (var item in events)
                EventBus.Subscribe(item.id, item.action, false);
        }

        /// <summary>
        /// Отписка от всех событий - вызывать в OnDisable() или OnHide()
        /// </summary>
        void UnsubscribeEvents()
        {
            foreach (var item in events)
                EventBus.Unsubscribe(item.id, item.action, false);
        }

        /// <summary>
        /// Добавить событие в список
        /// </summary>
        void AddEvent(int eventId, Action<object> handler)
        {
            events.Add((eventId, handler));
        }
    }
}
