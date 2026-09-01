// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Настройки панели «Добавить в желаемое»: когда показывать, куда вести,
    /// что писать.
    ///
    /// Класс живёт в пакете, а ассет создаётся В ПРОЕКТЕ — там же выбираются
    /// проектные события шины (drawer собирает их из всех сборок). Пакет ничего
    /// не знает о конкретной игре: подключил систему, положил ассет — работает.
    ///
    /// Зачем вообще панель: у страницы в Steam кнопка «В желаемое» видна только
    /// снаружи, а игрок сидит в демо. Момент, когда он доволен (взял сектор,
    /// прокачал базу), — единственный, когда просьба уместна.
    /// </summary>
    [CreateAssetMenu(fileName = "WishlistPromptConfig",
                     menuName  = "ProtoSystem/LiveOps/Wishlist Prompt Config")]
    public class WishlistPromptConfig : ScriptableObject
    {
        /// <summary>Событие шины, по которому панель показывается.</summary>
        [Serializable]
        public class Trigger
        {
            [Tooltip("Событие шины. Список собирается из всех классов Evt — пакетных и проектных.")]
            [EventId] public int eventId;

            [Tooltip("Комментарий для инспектора: зачем этот триггер. На логику не влияет.")]
            public string note;

            [Tooltip("Какое по счёту срабатывание события показывает панель. 1 = первое.")]
            [Min(1)] public int occurrence = 1;

            [Tooltip("Пауза перед показом: событие обычно приходит под анимацию/переход, " +
                     "и панель поверх неё выглядит выскочившей из ниоткуда.")]
            [Min(0f)] public float delaySeconds = 1.0f;
        }

        [Header("Куда ведём")]
        [Tooltip("AppID ПОЛНОЙ игры (не демо и не плейтеста) — оверлей Steam добавит в желаемое именно её.")]
        public uint steamAppId;

        [Tooltip("Фолбэк, когда оверлей Steam недоступен (выключен игроком, не-Steam сборка). " +
                 "Метку utm стоит оставить — иначе переходы из игры не отличить от прочих.")]
        public string storeUrl = "";

        [Header("Когда показываем")]
        public List<Trigger> triggers = new();

        [Tooltip("Показать не больше N раз за всё время. Решение игрока (любая из двух кнопок) " +
                 "закрывает панель навсегда независимо от этого числа.")]
        [Min(1)] public int maxShows = 3;

        [Tooltip("Крестик закрывает панель, но НЕ считается решением: она придёт на следующем триггере.")]
        public bool showCloseButton = true;

        [Header("Тексты")]
        [Tooltip("Строка вида «#ключ» уходит в локализацию, обычный текст показывается как есть.")]
        public string titleText   = "#wishlist_prompt_title";
        public string bodyText    = "#wishlist_prompt_body";
        public string addText     = "#wishlist_prompt_add";
        public string alreadyText = "#wishlist_prompt_already";

        [Header("Визуал")]
        [Tooltip("Шаблон панели. По умолчанию — WishlistPrompt.uxml из пакета; можно подменить своим.")]
        public VisualTreeAsset template;

        [Tooltip("PanelSettings для оверлея. Пусто — берём первый попавшийся в проекте.")]
        public PanelSettings panelSettings;

        [Tooltip("Порядок отрисовки: панель должна лежать поверх игрового UI.")]
        public int sortingOrder = 500;

        /// <summary>Ссылка на магазин без UTM — на случай пустого поля в ассете.</summary>
        public string ResolveStoreUrl()
        {
            if (!string.IsNullOrWhiteSpace(storeUrl)) return storeUrl.Trim();
            return steamAppId > 0 ? $"https://store.steampowered.com/app/{steamAppId}/" : "";
        }
    }
}
