using UnityEditor;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Настройки ИИ 3D-студии. Всё в EditorPrefs (ключи API — НИКОГДА в git).
    /// ComfyUI-сервер общий с арт/аудио-студией (ключ ProtoIcon.ComfyServer).
    /// </summary>
    public static class ModelAiSettings
    {
        // Общий сервер с арт-студией
        private const string ComfyServerKey = "ProtoIcon.ComfyServer";
        private const string ComfyLaunchKey = "ProtoIcon.ComfyLaunch";

        private const string ComfyOutputDirKey = "ProtoModel.ComfyOutputDir";
        private const string MeshyApiKeyKey   = "ProtoModel.MeshyApiKey";
        private const string DitModelKey      = "ProtoModel.DitModel";
        private const string VaeModelKey      = "ProtoModel.VaeModel";

        public static string ComfyServer
        {
            get => EditorPrefs.GetString(ComfyServerKey, "http://127.0.0.1:8188");
            set => EditorPrefs.SetString(ComfyServerKey, value);
        }

        /// <summary>Команда запуска ComfyUI (общая с арт-студией).</summary>
        public static string ComfyLaunch
        {
            get => EditorPrefs.GetString(ComfyLaunchKey, "E:\\AI\\ComfyUI\\start.bat");
            set => EditorPrefs.SetString(ComfyLaunchKey, value);
        }

        /// <summary>Локальная папка output ComfyUI — оттуда забираем готовые GLB.</summary>
        public static string ComfyOutputDir
        {
            get => EditorPrefs.GetString(ComfyOutputDirKey, "E:/AI/ComfyUI/output");
            set => EditorPrefs.SetString(ComfyOutputDirKey, value);
        }

        public static string MeshyApiKey
        {
            get => EditorPrefs.GetString(MeshyApiKeyKey, "");
            set => EditorPrefs.SetString(MeshyApiKeyKey, value);
        }

        public static string DitModel
        {
            get => EditorPrefs.GetString(DitModelKey, "hunyuan3d-dit-v2-1.ckpt");
            set => EditorPrefs.SetString(DitModelKey, value);
        }

        public static string VaeModel
        {
            get => EditorPrefs.GetString(VaeModelKey, "hunyuan3d-vae-v2-1.ckpt");
            set => EditorPrefs.SetString(VaeModelKey, value);
        }
    }

    /// <summary>Страница Project Settings ▸ AI Model Tools.</summary>
    internal static class ModelToolsSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            var provider = new SettingsProvider("Project/AI Model Tools", SettingsScope.Project)
            {
                label = "AI Model Tools (3D)",
                guiHandler = _ =>
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("ComfyUI (Hunyuan3D, локально)", EditorStyles.boldLabel);
                    ModelAiSettings.ComfyServer    = EditorGUILayout.TextField("Сервер", ModelAiSettings.ComfyServer);
                    ModelAiSettings.ComfyLaunch    = EditorGUILayout.TextField("Команда запуска", ModelAiSettings.ComfyLaunch);
                    ModelAiSettings.ComfyOutputDir = EditorGUILayout.TextField("Папка output", ModelAiSettings.ComfyOutputDir);
                    ModelAiSettings.DitModel       = EditorGUILayout.TextField("DIT чекпоинт", ModelAiSettings.DitModel);
                    ModelAiSettings.VaeModel       = EditorGUILayout.TextField("VAE чекпоинт", ModelAiSettings.VaeModel);

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Meshy (облако)", EditorStyles.boldLabel);
                    ModelAiSettings.MeshyApiKey = EditorGUILayout.PasswordField("API-ключ", ModelAiSettings.MeshyApiKey);
                    EditorGUILayout.HelpBox("Ключ хранится только в EditorPrefs этой машины, в git не попадает.", MessageType.Info);
                },
                keywords = new[] { "AI", "3D", "Meshy", "Hunyuan", "ComfyUI", "Model" }
            };
            return provider;
        }
    }
}
