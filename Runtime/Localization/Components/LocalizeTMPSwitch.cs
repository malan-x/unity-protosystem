// Packages/com.protosystem.core/Runtime/Localization/Components/LocalizeTMPSwitch.cs
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Переключает LocalizeTMP между двумя вариантами ключей
    /// по событию EventBus с bool payload.
    ///
    /// Пример: подсказки управления (клавиатура ↔ геймпад).
    /// Variant A = клавиатура (по умолчанию), Variant B = геймпад.
    /// При получении события с payload=true → вариант B, false → A.
    ///
    /// Требуется LocalizeTMP на том же GameObject.
    /// Начальное состояние задаётся через InitialStateProvider (для Steam Deck и т.п.).
    /// </summary>
    [RequireComponent(typeof(LocalizeTMP))]
    [AddComponentMenu("ProtoSystem/Localization/Localize TMP Switch")]
    public class LocalizeTMPSwitch : MonoBehaviour
    {
        [Header("Event")]
        [Tooltip("ID события EventBus для переключения (payload: bool)")]
        [SerializeField] private int eventId;

        [Header("Variant A (default)")]
        [Tooltip("Ключ локализации для варианта A")]
        [SerializeField] private string keyA;
        [Tooltip("Fallback текст для варианта A")]
        [SerializeField] private string fallbackA;

        [Header("Variant B (on event = true)")]
        [Tooltip("Ключ локализации для варианта B")]
        [SerializeField] private string keyB;
        [Tooltip("Fallback текст для варианта B")]
        [SerializeField] private string fallbackB;

        /// <summary>
        /// Провайдер начального состояния. Вызывается в OnEnable.
        /// Принимает eventId, возвращает bool? (null = не задано).
        /// </summary>
        public static System.Func<int, bool?> InitialStateProvider;

        private LocalizeTMP _loc;

        private void Awake()
        {
            _loc = GetComponent<LocalizeTMP>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe(eventId, OnSwitch);
            // Применить начальное состояние (Steam Deck: геймпад активен с запуска)
            var initial = InitialStateProvider?.Invoke(eventId);
            if (initial.HasValue)
                Apply(initial.Value);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(eventId, OnSwitch);
        }

        private void OnSwitch(object payload)
        {
            if (payload is bool useB)
                Apply(useB);
        }

        /// <summary>
        /// Применить вариант вручную (для начального состояния).
        /// </summary>
        /// <param name="useB">true — вариант B, false — вариант A.</param>
        public void Apply(bool useB)
        {
            if (_loc == null) return;
            _loc.SetKey(
                useB ? keyB : keyA,
                useB ? fallbackB : fallbackA
            );
        }
    }
}
