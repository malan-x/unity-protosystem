using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Схема звуков для UI системы
    /// </summary>
    [CreateAssetMenu(fileName = "UISoundScheme", menuName = "ProtoSystem/Sound/UI Sound Scheme")]
    public class UISoundScheme : ScriptableObject
    {
        [Header("Window Events")]
        [Tooltip("Звук открытия обычного окна")]
        public string windowOpen = "ui_whoosh";
        
        [Tooltip("Звук закрытия обычного окна")]
        public string windowClose = "ui_close";
        
        [Tooltip("Звук открытия модального окна")]
        public string modalOpen = "ui_modal_open";
        
        [Tooltip("Звук закрытия модального окна")]
        public string modalClose = "ui_modal_close";
        
        [Header("Button Events")]
        [Tooltip("Звук клика по кнопке")]
        public string buttonClick = "ui_click";
        
        [Tooltip("Звук наведения на кнопку")]
        public string buttonHover = "ui_hover";
        
        [Tooltip("Звук клика по неактивной кнопке")]
        public string buttonDisabled = "ui_disabled";
        
        [Header("Navigation")]
        [Tooltip("Звук навигации между элементами")]
        public string navigate = "ui_navigate";
        
        [Tooltip("Звук возврата назад")]
        public string back = "ui_back";
        
        [Tooltip("Звук переключения вкладки")]
        public string tabSwitch = "ui_tab";
        
        [Header("Feedback")]
        [Tooltip("Звук успешного действия")]
        public string success = "ui_success";
        
        [Tooltip("Звук ошибки")]
        public string error = "ui_error";
        
        [Tooltip("Звук предупреждения")]
        public string warning = "ui_warning";
        
        [Tooltip("Звук уведомления")]
        public string notification = "ui_notification";
        
        [Header("Input Controls")]
        [Tooltip("Звук изменения слайдера")]
        public string sliderChange = "ui_slider";
        
        [Tooltip("Звук включения toggle")]
        public string toggleOn = "ui_toggle_on";
        
        [Tooltip("Звук выключения toggle")]
        public string toggleOff = "ui_toggle_off";
        
        [Tooltip("Звук открытия dropdown")]
        public string dropdownOpen = "ui_dropdown";
        
        [Tooltip("Звук выбора в dropdown")]
        public string dropdownSelect = "ui_select";
        
        [Header("Snapshots")]
        [Tooltip("Snapshot для модальных окон")]
        public SoundSnapshotId modalSnapshot = SoundSnapshotPreset.MenuFocus;
        
        [Tooltip("Snapshot для паузы")]
        public SoundSnapshotId pauseSnapshot = SoundSnapshotPreset.Paused;
        
        [Header("Per-Window Overrides")]
        [Tooltip("Переопределения звуков для конкретных окон")]
        public List<WindowSoundOverride> windowOverrides = new();
        
        /// <summary>
        /// Получить звук открытия для конкретного окна
        /// </summary>
        public string GetOpenSound(string windowId, bool isModal)
        {
            var over = windowOverrides.Find(o => o.windowId == windowId);
            if (over != null && !string.IsNullOrEmpty(over.openSound))
                return over.openSound;
            
            return isModal ? modalOpen : windowOpen;
        }
        
        /// <summary>
        /// Получить звук закрытия для конкретного окна
        /// </summary>
        public string GetCloseSound(string windowId, bool isModal)
        {
            var over = windowOverrides.Find(o => o.windowId == windowId);
            if (over != null && !string.IsNullOrEmpty(over.closeSound))
                return over.closeSound;
            
            return isModal ? modalClose : windowClose;
        }
        
        /// <summary>
        /// Получить snapshot для конкретного окна
        /// </summary>
        public SoundSnapshotId GetSnapshot(string windowId)
        {
            var over = windowOverrides.Find(o => o.windowId == windowId);
            if (over != null && !over.snapshot.IsEmpty)
                return over.snapshot;
            
            return default;
        }
    }
    
    /// <summary>
    /// Переопределение звуков для конкретного окна
    /// </summary>
    [Serializable]
    public class WindowSoundOverride
    {
        [Tooltip("ID окна из UIWindow атрибута")]
        public string windowId;
        
        [Tooltip("Звук открытия (пусто = использовать дефолт)")]
        public string openSound;
        
        [Tooltip("Звук закрытия (пусто = использовать дефолт)")]
        public string closeSound;
        
        [Tooltip("Snapshot при открытии")]
        public SoundSnapshotId snapshot;
    }
}
