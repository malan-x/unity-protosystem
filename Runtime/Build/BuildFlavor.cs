// Packages/com.protosystem.core/Runtime/Build/BuildFlavor.cs
namespace ProtoSystem
{
    /// <summary>
    /// Тип сборки. Определяет, какие ограничения контента активны в проекте.
    /// Normal — полная игра; Demo/Playtest — урезанные версии (что именно они ограничивают,
    /// решает сам проект, читая BuildInfo.Flavor).
    /// </summary>
    public enum BuildFlavor
    {
        Normal,
        Demo,
        Playtest,
    }

    /// <summary>
    /// Информация о текущей сборке. В плеере тип берётся из scripting-define
    /// (PROTO_FLAVOR_DEMO / PROTO_FLAVOR_PLAYTEST), которые Build Publisher добавляет по
    /// активному Steam-таргету. В редакторе — из симуляции (BuildFlavorEditor / EditorPrefs),
    /// чтобы можно было тестировать ограничения в Play Mode без сборки.
    /// </summary>
    public static class BuildInfo
    {
#if UNITY_EDITOR
        /// <summary>
        /// Симуляция типа сборки в редакторе. Ставится BuildFlavorEditor по InitializeOnLoad
        /// из EditorPrefs; в билд не попадает. null → берём значение из define (обычно Normal).
        /// </summary>
        public static BuildFlavor? EditorOverride;
#endif

        /// <summary>Тип текущей сборки.</summary>
        public static BuildFlavor Flavor
        {
            get
            {
#if UNITY_EDITOR
                if (EditorOverride.HasValue) return EditorOverride.Value;
#endif
#if PROTO_FLAVOR_PLAYTEST
                return BuildFlavor.Playtest;
#elif PROTO_FLAVOR_DEMO
                return BuildFlavor.Demo;
#else
                return BuildFlavor.Normal;
#endif
            }
        }

        public static bool IsNormal   => Flavor == BuildFlavor.Normal;
        public static bool IsDemo     => Flavor == BuildFlavor.Demo;
        public static bool IsPlaytest => Flavor == BuildFlavor.Playtest;
    }
}
