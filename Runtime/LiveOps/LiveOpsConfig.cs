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
        [Tooltip("URL сервера. Пример: https://api.mygame.com/v1")]
        public string serverUrl = "";

        [Tooltip("Идентификатор проекта. Передаётся в header X-Project-ID. Пример: last-convoy, km")]
        public string projectId = "";

        [Header("Localization")]
        [Tooltip("Код языка по умолчанию (ISO 639-1). Используется для LocalizedString.Get().")]
        public string defaultLanguage = "en";

        [Header("Features")]
        [Tooltip("Показывать сообщения от разработчиков (MOTD/новости).")]
        public bool enableMessages = true;

        [Tooltip("Отправлять аналитические события.")]
        public bool enableAnalytics = true;

        [Tooltip("Показывать опросы.")]
        public bool enablePolls = true;

        [Tooltip("Разрешить отправку фидбека.")]
        public bool enableFeedback = true;

        [Tooltip("Показывать новости/объявления.")]
        public bool enableAnnouncements = true;

        [Tooltip("Показывать Dev Log.")]
        public bool enableDevLog = true;

        [Tooltip("Показывать рейтинг билда.")]
        public bool enableRating = true;

        [Tooltip("Показывать прогресс-бар цели (milestone).")]
        public bool enableGoal = true;

        [Header("Behaviour")]
        [Tooltip("Интервал периодического обновления (секунды). 0 — отключить периодику.")]
        public float fetchIntervalSeconds = 300f;

        [Tooltip("Обновлять данные при открытии панели.")]
        public bool fetchOnPanelOpen = true;

        [Tooltip("Обновлять данные при открытии главного меню (по событию Evt.UI.WindowOpened).")]
        public bool fetchOnMainMenuOpen = true;

        [Tooltip("Имя окна главного меню для триггера обновления.")]
        public string mainMenuWindowName = "MainMenuWindow";

        [Tooltip("Таймаут health-check при старте (секунды). Если сервер не отвечает — панель не показывается.")]
        public float healthCheckTimeoutSeconds = 5f;

        [Tooltip("Максимальный размер очереди аналитики (offline-буфер).")]
        public int analyticsQueueLimit = 100;

        [Tooltip("Таймаут HTTP-запросов (секунды).")]
        public float requestTimeoutSeconds = 10f;

        [Header("Provider")]
        [Tooltip("Default — универсальный REST (кастомный бэкенд).\nPocketBase — для бэкенда на PocketBase.")]
        public LiveOpsProviderType providerType = LiveOpsProviderType.PocketBase;

        // Провайдер устанавливается программно, не через инспектор
        [NonSerialized] private ILiveOpsProvider _provider;

        /// <summary>Установить провайдер бэкенда.</summary>
        public void SetProvider(ILiveOpsProvider provider) => _provider = provider;

        /// <summary>Получить провайдер бэкенда.</summary>
        public ILiveOpsProvider GetProvider() => _provider;

        /// <summary>Создать провайдер по настройкам конфига.</summary>
        public ILiveOpsProvider CreateProvider(string playerId = null)
        {
            return providerType == LiveOpsProviderType.PocketBase
                ? new PocketBaseHttpLiveOpsProvider(serverUrl, projectId, playerId, requestTimeoutSeconds)
                : (ILiveOpsProvider)new DefaultHttpLiveOpsProvider(serverUrl, projectId, playerId, requestTimeoutSeconds);
        }
    }
}
