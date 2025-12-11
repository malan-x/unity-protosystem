// Packages/com.protosystem.core/Runtime/EventBus/EventBus.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem
{
    public static partial class EventBus
    {
        private static readonly Dictionary<int, List<Action<object>>> handlers = new();

        public static void Publish(int eventId, object payload)
        {
            if (handlers.TryGetValue(eventId, out var list))
            {
                foreach (var handler in list.ToArray()) // ToArray to avoid modification during iteration
                {
                    handler(payload);
                }
            }
        }

        public static void Publish(int eventId, object payload, bool log)
        {
            Publish(eventId, payload);
            if (log)
            {
                Debug.Log($"Event published: {GetEventPath(eventId)} with payload: {payload}");
            }
        }

        public static void Subscribe(int eventId, Action<object> handler)
        {
            if (!handlers.ContainsKey(eventId))
            {
                handlers[eventId] = new List<Action<object>>();
            }
            handlers[eventId].Add(handler);
        }

        public static void Subscribe(int eventId, Action<object> handler, bool log)
        {
            Subscribe(eventId, handler);
            if (log)
            {
                Debug.Log($"Subscribed to event: {GetEventPath(eventId)}");
            }
        }

        public static void Unsubscribe(int eventId, Action<object> handler)
        {
            if (handlers.TryGetValue(eventId, out var list))
            {
                list.Remove(handler);
            }
        }

        public static void Unsubscribe(int eventId, Action<object> handler, bool log)
        {
            Unsubscribe(eventId, handler);
            if (log)
            {
                Debug.Log($"Unsubscribed from event: {GetEventPath(eventId)}");
            }
        }

        // Empty groups for extension
        public static partial class Сеть
        {
            // Empty for extension
        }

        public static partial class Система
        {
            // Empty for extension
        }

        public static partial class ФТД
        {
            // Empty for extension
        }

        public static partial class Аудио
        {
            // Empty for extension
        }

        public static partial class Верёвка
        {
            // Empty for extension
        }

        public static partial class Боевка
        {
            // Empty for extension
        }

        // Add more empty groups as needed

        public static string GetEventPath(int id)
        {
            // Basic implementation; can be overridden in partial classes
            return $"Event {id}";
        }

        public static string DebugInfo()
        {
            return $"Total handlers: {handlers.Count}";
        }
    }
}
