namespace ProtoSystem.UI
{
    /// <summary>
    /// Текущее устройство ввода для UI. Ставит проект (из своего input-слоя),
    /// читает UIToolkitWindowBase: на root видимых окон поддерживается класс
    /// «input-gamepad», которым USS глушит :hover-подсветки — на Steam Deck
    /// невидимый курсор стоит в одной точке, и строка под ним горит вечно.
    /// </summary>
    public static class UIInputState
    {
        /// <summary>Класс на root окна при активном геймпаде.</summary>
        public const string GamepadClass = "input-gamepad";

        /// <summary>Последнее активное устройство — геймпад.</summary>
        public static bool GamepadActive;
    }
}
