// Packages/com.protosystem.core/Runtime/LiveOps/LiveOpsConfig.cs
using System;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Конфигурация LiveOps-системы. Не содержит специфики бэкенда —
    /// только универсальные параметры поведения.
    /// </summary>
    [CreateAssetMenu(fileName = "LiveOpsConfig", menuName = "ProtoSystem/LiveOps Config")]
    public class LiveOpsConfig : ScriptableObject
    {
        [Header("Server")]
        [Tooltip("URL сервера. Пример: https://example.com или http://192.168.1.1:8090")]
        public string serverUrl = "";

        [Header("Features")]
        [Tooltip("Показывать сообщения от разработчиков (MOTD/новости).")]
        public bool enableMessages = true;

        [Tooltip("Отправлять аналитические события.")]
        public bool enableAnalytics = true;

        [Tooltip("Показывать опросы.")]
        public bool enablePolls = true;

        [Tooltip("Разрешить отправку фидбека.")]
        public bool enableFeedback = true;

        [Header("Behaviour")]
        [Tooltip("Интервал обновления сообщений и опросов (секунды). 0 — только при старте.")]
        public float fetchIntervalSeconds = 300f;

        [Tooltip("Максимальный размер очереди аналитики (offline-буфер).")]
        public int analyticsQueueLimit = 100;

        [Tooltip("Таймаут HTTP-запросов (секунды).")]
        public float requestTimeoutSeconds = 10f;

        // Провайдер устанавливается программно, не через инспектор
        [NonSerialized] private ILiveOpsProvider _provider;

        /// <summary>Установить провайдер бэкенда.</summary>
        public void SetProvider(ILiveOpsProvider provider) => _provider = provider;

        /// <summary>Получить провайдер бэкенда.</summary>
        public ILiveOpsProvider GetProvider() => _provider;
    }
}
