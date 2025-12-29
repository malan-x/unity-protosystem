// Packages/com.protosystem.core/Runtime/UI/UISystemConfig.cs
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Конфигурация UI системы
    /// </summary>
    [CreateAssetMenu(fileName = "UISystemConfig", menuName = "ProtoSystem/UI/System Config")]
    public class UISystemConfig : ScriptableObject
    {
        [Header("Animation Defaults")]
        [Tooltip("Длительность анимации по умолчанию")]
        public float defaultAnimationDuration = 0.25f;
        
        [Tooltip("Анимация показа по умолчанию")]
        public TransitionAnimation defaultShowAnimation = TransitionAnimation.Fade;
        
        [Tooltip("Анимация скрытия по умолчанию")]
        public TransitionAnimation defaultHideAnimation = TransitionAnimation.Fade;

        [Header("Modal Settings")]
        [Tooltip("Цвет затемнения под модальными окнами")]
        public Color modalOverlayColor = new Color(0, 0, 0, 0.5f);
        
        [Tooltip("Закрывать модальное при клике на overlay")]
        public bool closeModalOnOverlayClick = true;

        [Header("Toast Settings")]
        [Tooltip("Длительность тоста по умолчанию")]
        public float defaultToastDuration = 3f;
        
        [Tooltip("Максимум одновременных тостов")]
        public int maxToasts = 3;
        
        [Tooltip("Позиция тостов")]
        public ToastPosition toastPosition = ToastPosition.TopCenter;

        [Header("Tooltip Settings")]
        [Tooltip("Задержка перед показом тултипа")]
        public float tooltipDelay = 0.5f;
        
        [Tooltip("Отступ тултипа от курсора")]
        public Vector2 tooltipOffset = new Vector2(10, -10);

        // Dialog Prefabs - без [Header], рисуется в кастомном редакторе
        [Tooltip("Префаб диалога подтверждения")]
        public GameObject confirmDialogPrefab;
        
        [Tooltip("Префаб диалога с вводом")]
        public GameObject inputDialogPrefab;
        
        [Tooltip("Префаб диалога выбора")]
        public GameObject choiceDialogPrefab;

        // Common Prefabs - без [Header], рисуется в кастомном редакторе
        [Tooltip("Префаб тоста")]
        public GameObject toastPrefab;
        
        [Tooltip("Префаб тултипа")]
        public GameObject tooltipPrefab;
        
        [Tooltip("Префаб прогресс-бара")]
        public GameObject progressPrefab;
        
        [Tooltip("Префаб оверлея для модальных окон")]
        public GameObject modalOverlayPrefab;

        /// <summary>
        /// Создать конфиг с настройками по умолчанию
        /// </summary>
        public static UISystemConfig CreateDefault()
        {
            return CreateInstance<UISystemConfig>();
        }
    }

    /// <summary>
    /// Позиция тостов на экране
    /// </summary>
    public enum ToastPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
