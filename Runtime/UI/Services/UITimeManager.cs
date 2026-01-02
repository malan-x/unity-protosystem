// Packages/com.protosystem.core/Runtime/UI/Services/UITimeManager.cs
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Управление временем для UI (пауза при открытии окон).
    /// Хранит базовый timeScale отдельно от паузы.
    /// </summary>
    public class UITimeManager
    {
        private static UITimeManager _instance;
        public static UITimeManager Instance => _instance ??= new UITimeManager();

        /// <summary>
        /// Базовая скорость времени (используется когда игра не на паузе)
        /// </summary>
        public float BaseTimeScale { get; private set; } = 1f;

        /// <summary>
        /// Счётчик окон, требующих паузу
        /// </summary>
        public int PauseRequestCount { get; private set; }

        /// <summary>
        /// Игра на паузе?
        /// </summary>
        public bool IsPaused => PauseRequestCount > 0;

        /// <summary>
        /// Установить базовую скорость времени (slow-mo, fast-forward и т.д.)
        /// </summary>
        public void SetBaseTimeScale(float scale)
        {
            BaseTimeScale = Mathf.Clamp(scale, 0f, 10f);
            ApplyTimeScale();
            Debug.Log($"[UITimeManager] BaseTimeScale set to {BaseTimeScale}");
        }

        /// <summary>
        /// Запросить паузу (вызывается при открытии окна с PauseGame=true)
        /// </summary>
        public void RequestPause(string windowId)
        {
            PauseRequestCount++;
            Debug.Log($"[UITimeManager] Pause requested by '{windowId}'. Count: {PauseRequestCount}");
            ApplyTimeScale();
        }

        /// <summary>
        /// Снять запрос паузы (вызывается при закрытии окна с PauseGame=true)
        /// </summary>
        public void ReleasePause(string windowId)
        {
            PauseRequestCount = Mathf.Max(0, PauseRequestCount - 1);
            Debug.Log($"[UITimeManager] Pause released by '{windowId}'. Count: {PauseRequestCount}");
            ApplyTimeScale();
        }

        /// <summary>
        /// Принудительно сбросить все запросы паузы
        /// </summary>
        public void ResetAllPauses()
        {
            PauseRequestCount = 0;
            ApplyTimeScale();
            Debug.Log("[UITimeManager] All pauses reset");
        }

        /// <summary>
        /// Применить timeScale в зависимости от состояния
        /// </summary>
        private void ApplyTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : BaseTimeScale;
        }

        /// <summary>
        /// Сбросить состояние (вызывается при выходе из игры/смене сцены)
        /// </summary>
        public void Reset()
        {
            PauseRequestCount = 0;
            BaseTimeScale = 1f;
            Time.timeScale = 1f;
        }
    }
}
