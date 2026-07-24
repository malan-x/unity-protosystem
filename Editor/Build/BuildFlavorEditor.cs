// Packages/com.protosystem.core/Editor/Build/BuildFlavorEditor.cs
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Симуляция типа сборки в редакторе: пункты меню ProtoSystem/Build/Simulated Flavor/* и
    /// дропдаун в главном тулбаре (BuildFlavorMainToolbar) пишут выбор в EditorPrefs и
    /// прокидывают в BuildInfo.EditorOverride. Позволяет проверять ограничения контента
    /// (Demo/Playtest) в Play Mode без реальной сборки. В билд не попадает — плеер читает
    /// тип из scripting-define.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildFlavorEditor
    {
        private const string PrefKey = "ProtoSystem.SimulatedFlavor";
        private const string MenuRoot = "ProtoSystem/Build/Simulated Flavor/";

        /// <summary>Событие смены симулируемого типа — на него подписан тулбар, чтобы обновить подпись.</summary>
        public static event System.Action Changed;

        static BuildFlavorEditor()
        {
            Apply();
        }

        /// <summary>Текущий симулируемый в редакторе тип сборки.</summary>
        public static BuildFlavor Simulated =>
            (BuildFlavor)EditorPrefs.GetInt(PrefKey, (int)BuildFlavor.Normal);

        private static void Apply()
        {
            BuildInfo.EditorOverride = Simulated;
        }

        /// <summary>Задать симулируемый тип сборки (обновляет BuildInfo.EditorOverride и тулбар).</summary>
        public static void SetSimulated(BuildFlavor flavor)
        {
            EditorPrefs.SetInt(PrefKey, (int)flavor);
            Apply();
            UnityEngine.Debug.Log($"[ProtoSystem] Симуляция типа сборки в редакторе: {flavor}");
            Changed?.Invoke();
        }

        [MenuItem(MenuRoot + "Normal", false, 0)]
        private static void SetNormal() => SetSimulated(BuildFlavor.Normal);
        [MenuItem(MenuRoot + "Normal", true)]
        private static bool SetNormalValidate()
        {
            Menu.SetChecked(MenuRoot + "Normal", Simulated == BuildFlavor.Normal);
            return true;
        }

        [MenuItem(MenuRoot + "Demo", false, 1)]
        private static void SetDemo() => SetSimulated(BuildFlavor.Demo);
        [MenuItem(MenuRoot + "Demo", true)]
        private static bool SetDemoValidate()
        {
            Menu.SetChecked(MenuRoot + "Demo", Simulated == BuildFlavor.Demo);
            return true;
        }

        [MenuItem(MenuRoot + "Playtest", false, 2)]
        private static void SetPlaytest() => SetSimulated(BuildFlavor.Playtest);
        [MenuItem(MenuRoot + "Playtest", true)]
        private static bool SetPlaytestValidate()
        {
            Menu.SetChecked(MenuRoot + "Playtest", Simulated == BuildFlavor.Playtest);
            return true;
        }
    }
}
