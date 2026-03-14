// Packages/com.protosystem.core/Runtime/LiveOps/LiveOpsProviderType.cs
namespace ProtoSystem.LiveOps
{
    /// <summary>Тип HTTP-провайдера LiveOps.</summary>
    public enum LiveOpsProviderType
    {
        /// <summary>Универсальный REST-провайдер для кастомного бэкенда.</summary>
        Default,

        /// <summary>Провайдер для бэкенда на PocketBase.</summary>
        PocketBase,
    }
}
