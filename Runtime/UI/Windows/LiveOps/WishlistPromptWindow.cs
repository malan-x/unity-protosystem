// Packages/com.protosystem.core/Runtime/UI/Windows/LiveOps/WishlistPromptWindow.cs
using System;
using UnityEngine.UIElements;
using ProtoSystem.LiveOps;

namespace ProtoSystem.UI
{
    /// <summary>Что ответил игрок на просьбу о вишлисте.</summary>
    public enum WishlistPromptAnswer
    {
        /// <summary>Нажал «Добавить» — открылась страница магазина.</summary>
        Added,
        /// <summary>Нажал «Уже добавил».</summary>
        AlreadyHas,
        /// <summary>Закрыл крестиком или Esc — решение не принято.</summary>
        Dismissed,
    }

    /// <summary>
    /// Окно «Добавить в желаемое» — обычное модальное окно UISystem.
    ///
    /// Первая версия жила в собственном UIDocument мимо UISystem, ради того
    /// чтобы пакету не требовался префаб. Оказалось дороже: пришлось руками
    /// повторять порядок отрисовки, затемнение, модальность и фокус, а на
    /// чужие окна панель всё равно наслаивалась и ломала им выход. Здесь всё
    /// это даёт сама UISystem — слой Modals, затемнение из UISystemConfig,
    /// стек окон и возврат фокуса при закрытии.
    ///
    /// Класс и разметка живут в пакете, префаб создаётся в проекте
    /// (ProtoSystem → LiveOps → Создать префаб окна вишлиста) — то же
    /// разделение, что у ConfirmDialog и Splash.
    ///
    /// Конфиг приходит payload'ом от WishlistPromptSystem; ответ уходит
    /// обратно событием Answered — телеметрию и состояние пишет система,
    /// окно занимается только показом.
    /// </summary>
    [UIWindow("WishlistPrompt", WindowType.Modal, WindowLayer.Modals)]
    public class WishlistPromptWindow : UIToolkitWindowBase
    {
        /// <summary>Идентификатор окна в UISystem — им же помечен атрибут класса.</summary>
        public const string WindowId = "WishlistPrompt";

        /// <summary>Игрок ответил. Подписчик — WishlistPromptSystem.</summary>
        public static event Action<WishlistPromptAnswer> Answered;

        private WishlistPromptConfig _config;

        protected override void OnPayload(object payload)
        {
            _config = payload as WishlistPromptConfig;

            // Payload может прийти и после сборки дерева — тогда наполняем сразу
            if (Root != null) Apply(Root);
        }

        protected override void OnBuildUI(VisualElement root)
        {
            Apply(root);
        }

        private void Apply(VisualElement root)
        {
            if (root == null) return;

            var add     = root.Q<Button>("add-button");
            var already = root.Q<Button>("already-button");
            var close   = root.Q<Button>("close-button");

            if (_config != null)
            {
                SetText(root, "title", _config.titleText);
                SetText(root, "body",  _config.bodyText);
                if (add != null)     add.text     = _config.addText;
                if (already != null) already.text = _config.alreadyText;
                if (close != null)
                    close.style.display = _config.showCloseButton ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Переподписка вместо накопления: дерево пересоздаётся при каждом
            // Show (окна лежат в пуле), а Apply может вызваться дважды —
            // из OnBuildUI и из OnPayload
            if (add != null)     { add.clicked     -= OnAdd;     add.clicked     += OnAdd; }
            if (already != null) { already.clicked -= OnAlready; already.clicked += OnAlready; }
            if (close != null)   { close.clicked   -= OnClose;   close.clicked   += OnClose; }

            Localization.Localize(root);
        }

        private static void SetText(VisualElement root, string name, string value)
        {
            var label = root.Q<Label>(name);
            if (label != null && !string.IsNullOrEmpty(value)) label.text = value;
        }

        private void OnAdd()
        {
            StoreLink.OpenStorePage(_config);
            Answer(WishlistPromptAnswer.Added);
        }

        private void OnAlready() => Answer(WishlistPromptAnswer.AlreadyHas);

        private void OnClose() => Answer(WishlistPromptAnswer.Dismissed);

        /// <summary>
        /// Esc и «кружок» геймпада — то же, что крестик. Если крестик выключен
        /// в конфиге, окно не закрываем: игрок должен ответить.
        /// </summary>
        public override void OnBackPressed()
        {
            if (_config != null && !_config.showCloseButton) return;
            Answer(WishlistPromptAnswer.Dismissed);
        }

        private void Answer(WishlistPromptAnswer answer)
        {
            Answered?.Invoke(answer);
            Close();
        }
    }
}
