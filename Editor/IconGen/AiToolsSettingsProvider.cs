using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// «Project Settings ▸ AI Icon Tools» — единое место для адресов ComfyUI/rembg, выбора модели,
    /// LoRA и дефолтного стиль-пресета. Раньше это были захардкоженные пути и LoRA-блок в окне базы.
    /// </summary>
    public static class AiToolsSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/AI Icon Tools", SettingsScope.Project)
            {
                label = "AI Icon Tools",
                keywords = new HashSet<string> { "ComfyUI", "rembg", "icon", "AI", "LoRA", "checkpoint" },
                guiHandler = _ => Draw(),
            };
        }

        private static void Draw()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ComfyUI", EditorStyles.boldLabel);
            AiToolsSettings.ComfyServer = EditorGUILayout.TextField("Сервер", AiToolsSettings.ComfyServer);

            // Чем поднимать сервер кнопкой из студии
            using (new EditorGUILayout.HorizontalScope())
            {
                AiToolsSettings.ComfyLaunch = EditorGUILayout.TextField(
                    new GUIContent("Запуск (.bat/.exe)",
                        "Файл запуска ComfyUI, напр. …\\ComfyUI\\run_nvidia_gpu.bat. " +
                        "Пусто — кнопка запуска в студии недоступна."),
                    AiToolsSettings.ComfyLaunch);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFilePanel("Файл запуска ComfyUI", "", "bat,exe,cmd,ps1");
                    if (!string.IsNullOrEmpty(picked)) AiToolsSettings.ComfyLaunch = picked;
                }
            }

            AiToolsSettings.Checkpoint = EditorGUILayout.TextField(
                new GUIContent("Checkpoint", "Файл модели в models/checkpoints"), AiToolsSettings.Checkpoint);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("LoRA (опционально)", EditorStyles.miniBoldLabel);
            AiToolsSettings.LoraName = EditorGUILayout.TextField(
                new GUIContent("Файл LoRA", "Файл в models/loras. Пусто — без LoRA (обычно лучше)."),
                AiToolsSettings.LoraName);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(AiToolsSettings.LoraName)))
            {
                AiToolsSettings.LoraTrigger = EditorGUILayout.TextField("Триггер-слово", AiToolsSettings.LoraTrigger);
                AiToolsSettings.LoraStrength = EditorGUILayout.Slider("Сила LoRA", AiToolsSettings.LoraStrength, 0f, 1f);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("rembg (вырез фона)", EditorStyles.boldLabel);
            AiToolsSettings.RembgPython = EditorGUILayout.TextField("python.exe", AiToolsSettings.RembgPython);
            AiToolsSettings.RembgScript = EditorGUILayout.TextField("process_icon.py", AiToolsSettings.RembgScript);

            // Замеры на этой машине (CPU): isnet ~4 c, birefnet-lite ~14 c, birefnet ~23 c.
            // На игровых генерациях с чистым фоном isnet не уступает; birefnet — для тонких краёв.
            var rembgModels = new[] { "isnet-general-use", "birefnet-general-lite", "birefnet-general" };
            var rembgLabels = new[] { "isnet (быстрый, ~4 с)", "BiRefNet lite (~14 с)", "BiRefNet (точные края, ~23 с)" };
            int curModel = System.Array.IndexOf(rembgModels, AiToolsSettings.RembgModel);
            if (curModel < 0) curModel = 0;
            int newModel = EditorGUILayout.Popup(
                new GUIContent("Модель выреза", "Чем режем фон. BiRefNet точнее на тонких структурах " +
                                                "(антенны, проволока), но заметно медленнее на CPU."),
                curModel, rembgLabels);
            if (newModel != curModel) AiToolsSettings.RembgModel = rembgModels[newModel];

            EditorGUILayout.LabelField(
                RembgProcessor.IsAvailable ? "✓ rembg найден" : "✗ rembg не найден по указанным путям",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Стиль по умолчанию", EditorStyles.boldLabel);
            DrawDefaultStylePicker();
        }

        private static void DrawDefaultStylePicker()
        {
            var all = IconStyleRegistry.All();
            if (all.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Нет ни одного IconStylePreset. Создайте: Create ▸ ProtoSystem ▸ Icon Style Preset.",
                    MessageType.Info);
                return;
            }

            var current = IconStyleRegistry.Default;
            var names = new string[all.Count];
            int selected = 0;
            for (int i = 0; i < all.Count; i++)
            {
                names[i] = all[i].name;
                if (all[i] == current) selected = i;
            }

            int picked = EditorGUILayout.Popup("Дефолтный пресет", selected, names);
            if (picked != selected || current == null)
                IconStyleRegistry.Default = all[picked];
        }
    }
}
