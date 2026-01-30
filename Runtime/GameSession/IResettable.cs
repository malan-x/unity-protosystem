// Packages/com.protosystem.core/Runtime/GameSession/IResettable.cs
namespace ProtoSystem
{
    /// <summary>
    /// Интерфейс для систем с поддержкой мягкого сброса состояния.
    /// Реализующие системы автоматически сбрасываются при событии Session.Reset.
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        /// Сбросить состояние к начальным значениям.
        /// Вызывается автоматически через SystemInitializationManager.ResetAllResettableSystems()
        /// или вручную при получении события Session.Reset.
        /// </summary>
        /// <param name="resetData">Опциональные данные для сброса (может быть null)</param>
        void ResetState(object resetData = null);
    }
}
