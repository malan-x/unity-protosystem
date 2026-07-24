// Packages/com.protosystem.core/Editor/Build/BuildFlavorEditor.cs
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Симуляция типа сборки в редакторе: пункты меню ProtoSystem/Build/Simulated Flavor/*
    /// пишут выбор в EditorPrefs и прокидывают в BuildInfo.EditorOverride. Позволяет
    /// проверять ограничения контента (Demo/Playtest) в Play Mode без реальной сборки.
    /// В билд не попадает — плеер читает тип из scripting-define.
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildFlavorEditor
    {
        private const string PrefKey = "ProtoSystem.SimulatedFlavor";
        private const string MenuRoot = "ProtoSystem/Build/Simulated Flavor/";

        static BuildFlavorEditor()
        {
            Apply();
        }

        private static BuildFlavor Current =>
            (BuildFlavor)EditorPrefs.GetInt(PrefKey, (int)BuildFlavor.Normal);

        private static void Apply()
        {
            BuildInfo.EditorOverride = Current;
        }

        private static void Set(BuildFlavor flavor)
        {
            EditorPrefs.SetInt(PrefKey, (int)flavor);
            Apply();
            UnityEngine.Debug.Log($"[ProtoSystem] Симуляция типа сборки в редакторе: {flavor}");
        }

        [MenuItem(MenuRoot + "Normal", false, 0)]
        private static void SetNormal() => Set(BuildFlavor.Normal);
        [MenuItem(MenuRoot + "Normal", true)]
        private static bool SetNormalValidate()
        {
            Menu.SetChecked(MenuRoot + "Normal", Current == BuildFlavor.Normal);
            return true;
        }

        [MenuItem(MenuRoot + "Demo", false, 1)]
        private static void SetDemo() => Set(BuildFlavor.Demo);
        [MenuItem(MenuRoot + "Demo", true)]
        private static bool SetDemoValidate()
        {
            Menu.SetChecked(MenuRoot + "Demo", Current == BuildFlavor.Demo);
            return true;
        }

        [MenuItem(MenuRoot + "Playtest", false, 2)]
        private static void SetPlaytest() => Set(BuildFlavor.Playtest);
        [MenuItem(MenuRoot + "Playtest", true)]
        private static bool SetPlaytestValidate()
        {
            Menu.SetChecked(MenuRoot + "Playtest", Current == BuildFlavor.Playtest);
            return true;
        }
    }
}
