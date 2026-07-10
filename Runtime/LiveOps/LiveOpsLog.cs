// Packages/com.protosystem.core/Runtime/LiveOps/LiveOpsLog.cs
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Отладочное логирование LiveOps. Info-сообщения пишутся только при
    /// LiveOpsConfig.verboseLogging (флаг выставляет LiveOpsSystem при инициализации).
    /// Ошибки и предупреждения логируются через Debug.LogError/LogWarning всегда.
    /// </summary>
    public static class LiveOpsLog
    {
        /// <summary>Включены ли подробные логи (из LiveOpsConfig.verboseLogging).</summary>
        public static bool Verbose;

        public static void Info(string message)
        {
            if (Verbose) Debug.Log(message);
        }
    }
}
