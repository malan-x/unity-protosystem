// Packages/com.protosystem.core/Runtime/Localization/UILocalizationKeys.cs
namespace ProtoSystem
{
    /// <summary>
    /// Ключи локализации для всех стандартных UI окон ProtoSystem.
    /// Fallback — значения по умолчанию (русский), используются если локализация не настроена.
    /// 
    /// Таблица по умолчанию: "UI"
    /// Формат ключей: ui.{window}.{element}
    /// </summary>
    public static class UIKeys
    {
        public const string Table = "UI";
        
        // ═══════════════════════════════════════════════════════════════
        // MAIN MENU
        // ═══════════════════════════════════════════════════════════════
        
        public static class MainMenu
        {
            public const string Title       = "ui.mainmenu.title";
            public const string Play        = "ui.mainmenu.play";
            public const string Settings    = "ui.mainmenu.settings";
            public const string Credits     = "ui.mainmenu.credits";
            public const string Quit        = "ui.mainmenu.quit";
            
            public static class Fallback
            {
                public const string Title       = "GAME TITLE";
                public const string Play        = "Начать игру";
                public const string Settings    = "Настройки";
                public const string Credits     = "Авторы";
                public const string Quit        = "Выход";
            }
            
            public static class FallbackEn
            {
                public const string Title       = "GAME TITLE";
                public const string Play        = "Play";
                public const string Settings    = "Settings";
                public const string Credits     = "Credits";
                public const string Quit        = "Quit";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PAUSE MENU
        // ═══════════════════════════════════════════════════════════════
        
        public static class Pause
        {
            public const string Title           = "ui.pause.title";
            public const string Resume          = "ui.pause.resume";
            public const string Settings        = "ui.pause.settings";
            public const string MainMenu        = "ui.pause.mainmenu";
            public const string Quit            = "ui.pause.quit";
            public const string QuitConfirmTitle   = "ui.pause.quit_confirm_title";
            public const string QuitConfirmMessage = "ui.pause.quit_confirm_message";
            public const string MenuConfirmTitle   = "ui.pause.menu_confirm_title";
            public const string MenuConfirmMessage = "ui.pause.menu_confirm_message";
            
            public static class Fallback
            {
                public const string Title           = "ПАУЗА";
                public const string Resume          = "Продолжить";
                public const string Settings        = "Настройки";
                public const string MainMenu        = "В главное меню";
                public const string Quit            = "Выход";
                public const string QuitConfirmTitle   = "Выйти из игры?";
                public const string QuitConfirmMessage = "Несохранённый прогресс будет потерян.";
                public const string MenuConfirmTitle   = "Выйти в главное меню?";
                public const string MenuConfirmMessage = "Несохранённый прогресс будет потерян.";
            }
            
            public static class FallbackEn
            {
                public const string Title           = "PAUSED";
                public const string Resume          = "Resume";
                public const string Settings        = "Settings";
                public const string MainMenu        = "Main Menu";
                public const string Quit            = "Quit";
                public const string QuitConfirmTitle   = "Quit game?";
                public const string QuitConfirmMessage = "Unsaved progress will be lost.";
                public const string MenuConfirmTitle   = "Return to main menu?";
                public const string MenuConfirmMessage = "Unsaved progress will be lost.";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SETTINGS
        // ═══════════════════════════════════════════════════════════════
        
        public static class Settings
        {
            public const string Title           = "ui.settings.title";
            public const string AudioSection    = "ui.settings.audio";
            public const string MasterVolume    = "ui.settings.master_volume";
            public const string MusicVolume     = "ui.settings.music_volume";
            public const string SfxVolume       = "ui.settings.sfx_volume";
            public const string VoiceVolume     = "ui.settings.voice_volume";
            public const string AmbientVolume   = "ui.settings.ambient_volume";
            public const string UIVolume        = "ui.settings.ui_volume";
            public const string GraphicsSection = "ui.settings.graphics";
            public const string Quality         = "ui.settings.quality";
            public const string Resolution      = "ui.settings.resolution";
            public const string Fullscreen      = "ui.settings.fullscreen";
            public const string VSync           = "ui.settings.vsync";
            public const string GameplaySection = "ui.settings.gameplay";
            public const string Sensitivity     = "ui.settings.sensitivity";
            public const string InvertY         = "ui.settings.invert_y";
            public const string Language        = "ui.settings.language";
            public const string Apply           = "ui.settings.apply";
            public const string Reset           = "ui.settings.reset";
            public const string Back            = "ui.settings.back";
            
            public static class Fallback
            {
                public const string Title           = "НАСТРОЙКИ";
                public const string AudioSection    = "Звук";
                public const string MasterVolume    = "Громкость";
                public const string MusicVolume     = "Музыка";
                public const string SfxVolume       = "Эффекты";
                public const string VoiceVolume     = "Голос";
                public const string AmbientVolume   = "Окружение";
                public const string UIVolume        = "Интерфейс";
                public const string GraphicsSection = "Графика";
                public const string Quality         = "Качество";
                public const string Resolution      = "Разрешение";
                public const string Fullscreen      = "Полный экран";
                public const string VSync           = "V-Sync";
                public const string GameplaySection = "Управление";
                public const string Sensitivity     = "Чувствительность";
                public const string InvertY         = "Инверсия Y";
                public const string Language        = "Язык";
                public const string Apply           = "Применить";
                public const string Reset           = "Сброс";
                public const string Back            = "Назад";
            }
            
            public static class FallbackEn
            {
                public const string Title           = "SETTINGS";
                public const string AudioSection    = "Audio";
                public const string MasterVolume    = "Volume";
                public const string MusicVolume     = "Music";
                public const string SfxVolume       = "Effects";
                public const string VoiceVolume     = "Voice";
                public const string AmbientVolume   = "Ambient";
                public const string UIVolume        = "UI";
                public const string GraphicsSection = "Graphics";
                public const string Quality         = "Quality";
                public const string Resolution      = "Resolution";
                public const string Fullscreen      = "Fullscreen";
                public const string VSync           = "V-Sync";
                public const string GameplaySection = "Controls";
                public const string Sensitivity     = "Sensitivity";
                public const string InvertY         = "Invert Y";
                public const string Language        = "Language";
                public const string Apply           = "Apply";
                public const string Reset           = "Reset";
                public const string Back            = "Back";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // GAME OVER
        // ═══════════════════════════════════════════════════════════════
        
        public static class GameOver
        {
            public const string Victory         = "ui.gameover.victory";
            public const string Defeat          = "ui.gameover.defeat";
            public const string VictoryMessage  = "ui.gameover.victory_message";
            public const string DefeatMessage   = "ui.gameover.defeat_message";
            public const string Restart         = "ui.gameover.restart";
            public const string Menu            = "ui.gameover.menu";
            public const string Quit            = "ui.gameover.quit";
            
            public static class Fallback
            {
                public const string Victory         = "ПОБЕДА";
                public const string Defeat          = "ПОРАЖЕНИЕ";
                public const string VictoryMessage  = "Поздравляем! Вы победили!";
                public const string DefeatMessage   = "К сожалению, вы проиграли.";
                public const string Restart         = "Заново";
                public const string Menu            = "Меню";
                public const string Quit            = "Выход";
            }
            
            public static class FallbackEn
            {
                public const string Victory         = "VICTORY";
                public const string Defeat          = "DEFEAT";
                public const string VictoryMessage  = "Congratulations! You won!";
                public const string DefeatMessage   = "Unfortunately, you lost.";
                public const string Restart         = "Restart";
                public const string Menu            = "Menu";
                public const string Quit            = "Quit";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // STATISTICS
        // ═══════════════════════════════════════════════════════════════
        
        public static class Statistics
        {
            public const string Title       = "ui.statistics.title";
            public const string PlayTime    = "ui.statistics.play_time";
            public const string EnemiesKilled = "ui.statistics.enemies_killed";
            public const string DamageDealt = "ui.statistics.damage_dealt";
            public const string DamageReceived = "ui.statistics.damage_received";
            public const string Accuracy    = "ui.statistics.accuracy";
            public const string Continue    = "ui.statistics.continue";
            public const string Back        = "ui.statistics.back";
            
            public static class Fallback
            {
                public const string Title       = "СТАТИСТИКА";
                public const string PlayTime    = "Время игры";
                public const string EnemiesKilled = "Убито врагов";
                public const string DamageDealt = "Урон нанесён";
                public const string DamageReceived = "Урон получен";
                public const string Accuracy    = "Точность";
                public const string Continue    = "Продолжить";
                public const string Back        = "Назад";
            }
            
            public static class FallbackEn
            {
                public const string Title       = "STATISTICS";
                public const string PlayTime    = "Play Time";
                public const string EnemiesKilled = "Enemies Killed";
                public const string DamageDealt = "Damage Dealt";
                public const string DamageReceived = "Damage Received";
                public const string Accuracy    = "Accuracy";
                public const string Continue    = "Continue";
                public const string Back        = "Back";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // CREDITS
        // ═══════════════════════════════════════════════════════════════
        
        public static class Credits
        {
            public const string Title   = "ui.credits.title";
            public const string Skip    = "ui.credits.skip";
            public const string Back    = "ui.credits.back";
            
            public static class Fallback
            {
                public const string Title   = "АВТОРЫ";
                public const string Skip    = "Пропустить";
                public const string Back    = "Назад";
            }
            
            public static class FallbackEn
            {
                public const string Title   = "CREDITS";
                public const string Skip    = "Skip";
                public const string Back    = "Back";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // LOADING
        // ═══════════════════════════════════════════════════════════════
        
        public static class Loading
        {
            public const string Status      = "ui.loading.status";
            public const string DefaultTip  = "ui.loading.default_tip";
            
            public static class Fallback
            {
                public const string Status      = "Загрузка...";
                public const string DefaultTip  = "Совет: Нажмите ESC для паузы";
            }
            
            public static class FallbackEn
            {
                public const string Status      = "Loading...";
                public const string DefaultTip  = "Tip: Press ESC to pause";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // GAME HUD
        // ═══════════════════════════════════════════════════════════════
        
        public static class HUD
        {
            public const string Health      = "ui.hud.health";
            public const string Stamina     = "ui.hud.stamina";
            public const string Interact    = "ui.hud.interact";
            
            public static class Fallback
            {
                public const string Health      = "Health";
                public const string Stamina     = "Stamina";
                public const string Interact    = "[E] Interact";
            }
            
            public static class FallbackEn
            {
                public const string Health      = "Health";
                public const string Stamina     = "Stamina";
                public const string Interact    = "[E] Interact";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // DIALOGS
        // ═══════════════════════════════════════════════════════════════
        
        public static class Dialog
        {
            public const string ConfirmTitle    = "ui.dialog.confirm_title";
            public const string AlertTitle      = "ui.dialog.alert_title";
            public const string InputTitle      = "ui.dialog.input_title";
            public const string ChoiceTitle     = "ui.dialog.choice_title";
            public const string Yes             = "ui.dialog.yes";
            public const string No              = "ui.dialog.no";
            public const string OK              = "ui.dialog.ok";
            public const string Cancel          = "ui.dialog.cancel";
            public const string ConfirmMessage  = "ui.dialog.confirm_message";
            public const string InputMessage    = "ui.dialog.input_message";
            public const string InputPlaceholder = "ui.dialog.input_placeholder";
            public const string ChoiceMessage   = "ui.dialog.choice_message";
            public const string InvalidInput    = "ui.dialog.invalid_input";
            
            public static class Fallback
            {
                public const string ConfirmTitle    = "Подтверждение";
                public const string AlertTitle      = "Внимание";
                public const string InputTitle      = "Ввод";
                public const string ChoiceTitle     = "Выбор";
                public const string Yes             = "Да";
                public const string No              = "Нет";
                public const string OK              = "OK";
                public const string Cancel          = "Отмена";
                public const string ConfirmMessage  = "Вы уверены?";
                public const string InputMessage    = "Введите значение:";
                public const string InputPlaceholder = "Введите текст...";
                public const string ChoiceMessage   = "Выберите вариант:";
                public const string InvalidInput    = "Некорректный ввод";
            }
            
            public static class FallbackEn
            {
                public const string ConfirmTitle    = "Confirm";
                public const string AlertTitle      = "Alert";
                public const string InputTitle      = "Input";
                public const string ChoiceTitle     = "Choice";
                public const string Yes             = "Yes";
                public const string No              = "No";
                public const string OK              = "OK";
                public const string Cancel          = "Cancel";
                public const string ConfirmMessage  = "Are you sure?";
                public const string InputMessage    = "Enter value:";
                public const string InputPlaceholder = "Enter text...";
                public const string ChoiceMessage   = "Choose an option:";
                public const string InvalidInput    = "Invalid input";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // COMMON
        // ═══════════════════════════════════════════════════════════════
        
        public static class Common
        {
            public const string Back    = "ui.common.back";
            public const string Close   = "ui.common.close";
            public const string Confirm = "ui.common.confirm";
            
            public static class Fallback
            {
                public const string Back    = "Назад";
                public const string Close   = "Закрыть";
                public const string Confirm = "Подтвердить";
            }
            
            public static class FallbackEn
            {
                public const string Back    = "Back";
                public const string Close   = "Close";
                public const string Confirm = "Confirm";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // HELPER
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Получить локализованную строку с fallback.
        /// Использует Loc.Get если система доступна, иначе fallback.
        /// </summary>
        public static string L(string key, string fallback)
        {
            if (!Loc.IsReady) return fallback;
            return Loc.Get(key, fallback);
        }
    }
}
