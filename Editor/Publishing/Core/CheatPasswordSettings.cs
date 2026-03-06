using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Хранение пароля чит-кодов в EditorPrefs (project-scoped).
    /// </summary>
    public static class CheatPasswordSettings
    {
        private static string _projectKey;

        private static string ProjectKey
        {
            get
            {
                if (string.IsNullOrEmpty(_projectKey))
                    _projectKey = Application.dataPath.GetHashCode().ToString("X8");
                return _projectKey;
            }
        }

        private static string Key(string name) => $"ProtoSystem_Cheats_{ProjectKey}_{name}";

        public static string CheatPassword
        {
            get => EditorPrefs.GetString(Key("Password"), "");
            set => EditorPrefs.SetString(Key("Password"), value);
        }

        public static bool CheatsEnabled
        {
            get => EditorPrefs.GetBool(Key("Enabled"), false);
            set => EditorPrefs.SetBool(Key("Enabled"), value);
        }
    }
}
