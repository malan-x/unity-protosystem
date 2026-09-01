// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Настройки просьбы о вишлисте: когда показывать, куда вести, что писать.
    ///
    /// Класс живёт в пакете, а ассет создаётся В ПРОЕКТЕ — там же выбираются
    /// проектные события шины (drawer собирает их из всех сборок). Пакет ничего
    /// не знает о конкретной игре: подключил систему, положил ассет, создал
    /// префаб окна — работает.
    ///
    /// Внешний вид здесь не настраивается: окно — обычное модальное окно
    /// UISystem, его разметка в WishlistPrompt.uxml/.uss, затемнение и слой
    /// берутся из UISystemConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "WishlistPromptConfig",
                     menuName  = "ProtoSystem/LiveOps/Wishlist Prompt Config")]
    public class WishlistPromptConfig : ScriptableObject
    {
        /// <summary>Событие шины, по которому окно показывается.</summary>
        [Serializable]
        public class Trigger
        {
            [Tooltip("Событие шины. Список собирается из всех классов Evt — пакетных и проектных.")]
            [EventId] public int eventId;

            [Tooltip("Комментарий для инспектора: зачем этот триггер. На логику не влияет.")]
            public string note;

            [Tooltip("Какое по счёту срабатывание события показывает окно. 1 = первое.")]
            [Min(1)] public int occurrence = 1;

            [Tooltip("Пауза перед показом: событие обычно приходит под анимацию или переход экрана.")]
            [Min(0f)] public float delaySeconds = 1.0f;
        }

        [Header("Куда ведём")]
        [Tooltip("AppID ПОЛНОЙ игры (не демо и не плейтеста) — оверлей Steam откроет именно её страницу.")]
        public uint steamAppId;

        [Tooltip("Фолбэк, когда оверлей Steam недоступен (выключен игроком, не-Steam сборка). " +
                 "Метку utm стоит оставить — иначе переходы из игры не отличить от прочих.")]
        public string storeUrl = "";

        [Header("Когда показываем")]
        public List<Trigger> triggers = new();

        [Tooltip("События, отменяющие ОТЛОЖЕННЫЙ показ: игрок ушёл из спокойного места, " +
                 "и просьба там уже неуместна. Типичный случай — начался новый забег, пока " +
                 "окно ждало закрытия экрана итогов.")]
        [EventId] public List<int> cancelEventIds = new();

        [Tooltip("Дождаться, пока закроются чужие модальные окна. Иначе просьба ляжет " +
                 "поверх экрана итогов и перекроет его кнопки.")]
        public bool waitForQuietMoment = true;

        [Tooltip("Показать не больше N раз за всё время. Решение игрока (любая из двух кнопок) " +
                 "закрывает окно навсегда независимо от этого числа.")]
        [Min(1)] public int maxShows = 3;

        [Tooltip("Крестик закрывает окно, но НЕ считается решением: оно придёт на следующем триггере.")]
        public bool showCloseButton = true;

        [Header("Тексты")]
        [Tooltip("Строка вида «#ключ» уходит в локализацию, обычный текст показывается как есть.")]
        public string titleText   = "#wishlist_prompt_title";
        public string bodyText    = "#wishlist_prompt_body";
        public string addText     = "#wishlist_prompt_add";
        public string alreadyText = "#wishlist_prompt_already";

        /// <summary>Ссылка на магазин; если поле пустое — собираем из AppID.</summary>
        public string ResolveStoreUrl()
        {
            if (!string.IsNullOrWhiteSpace(storeUrl)) return storeUrl.Trim();
            return steamAppId > 0 ? $"https://store.steampowered.com/app/{steamAppId}/" : "";
        }
    }
}
