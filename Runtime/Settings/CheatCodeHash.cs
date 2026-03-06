namespace ProtoSystem.Settings
{
    /// <summary>
    /// Хранит SHA256 хэш чит-пароля. По умолчанию пустой (читы выключены).
    /// Значение устанавливается из сгенерированного файла Assets/{namespace}/Cheats/CheatCodeHash.g.cs
    /// через [RuntimeInitializeOnLoadMethod] до инициализации SettingsSystem.
    /// </summary>
    internal static class CheatCodeHash
    {
        internal static string Hash = "";
    }
}
