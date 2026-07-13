// Packages/com.protosystem.core/Runtime/Compat/UnityVersionCompat.cs
using System.Collections.Generic;
using System.Reflection;

namespace ProtoSystem.Compat
{
    /// <summary>
    /// Единая точка для API, которые Unity меняет между минорными версиями.
    /// Пакет должен собираться и на 6000.3, и на 6000.7+ — весь #if живёт ЗДЕСЬ,
    /// остальной код вызывает эти обёртки и не знает о версиях.
    ///
    /// Границы версий (проверены по метаданным редактора):
    /// - CurrentAssemblies.GetLoadedAssemblies() — public с 6000.4 (в 6000.3 тип internal);
    /// - Object.GetEntityId() / EditorUtility.EntityIdToObject — с 6000.7,
    ///   при этом GetInstanceID() там же помечен Obsolete с уровнем ERROR.
    /// </summary>
    public static class UnityVersionCompat
    {
        /// <summary>
        /// Сборки, загруженные Unity. На 6000.4+ — Unity-API (AppDomain.GetAssemblies()
        /// может вернуть уже выгруженные сборки: утечки + анализатор UAC0005).
        /// </summary>
        public static IReadOnlyList<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            return System.AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        /// <summary>
        /// Стабильный id объекта в рамках сессии (ключ словарей, сравнение).
        /// 6000.7+ — GetEntityId(), раньше — GetInstanceID(). Null-безопасно (0).
        ///
        /// Через GetHashCode(), а не (int)-каст: implicit operator int(EntityId) сам помечен
        /// Obsolete-as-error («EntityId will not be representable by an int in the future»).
        /// Так же поступают пакеты самой Unity (2d.*, netcode, URP). Если понадобится
        /// полноразмерный id — брать EntityId.ToULong(), а не расширять этот метод.
        /// </summary>
        public static int StableId(UnityEngine.Object obj)
        {
            if (obj == null) return 0;
#if UNITY_6000_7_OR_NEWER
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
