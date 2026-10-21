using UnityEditor;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Настройки AI-инструментов генерации иконок: адрес ComfyUI, пути к rembg, выбор модели/LoRA,
    /// референсы стиля, дефолтный стиль-пресет. Всё в EditorPrefs — переносимо между машинами и
    /// проектами, правится в «Project Settings ▸ AI Icon Tools» (см. AiToolsSettingsProvider).
    ///
    /// Ключи с нейтральным префиксом ProtoIcon.* — ядро не знает про Last Convoy.
    ///
    /// Значения кэшируются в статиках (ленивое чтение + write-through): свойства дёргаются из
    /// OnGUI каждый кадр, а EditorPrefs на Windows — это обращения к реестру, редакторы тормозили.
    /// </summary>
    public static class AiToolsSettings
    {
        // ── Кэшированные обёртки над EditorPrefs ──

        private static string GetS(ref string cache, string key, string def)
            => cache ??= EditorPrefs.GetString(key, def);

        private static void SetS(ref string cache, string key, string value)
        {
            cache = value;
            EditorPrefs.SetString(key, value);
        }

        private static float GetF(ref float? cache, string key, float def)
            => cache ??= EditorPrefs.GetFloat(key, def);

        private static void SetF(ref float? cache, string key, float value)
        {
            cache = value;
            EditorPrefs.SetFloat(key, value);
        }

        // ── ComfyUI ──

        private static string _comfyServer;
        public static string ComfyServer
        {
            get => GetS(ref _comfyServer, "ProtoIcon.ComfyServer", "http://127.0.0.1:8188");
            set => SetS(ref _comfyServer, "ProtoIcon.ComfyServer", value);
        }

        /// <summary>
        /// Чем запускать ComfyUI из студии: .bat/.exe (напр. …\ComfyUI\run_nvidia_gpu.bat).
        /// Пусто — кнопка запуска в студии недоступна, сервер поднимается вручную.
        /// </summary>
        private static string _comfyLaunch;
        public static string ComfyLaunch
        {
            get => GetS(ref _comfyLaunch, "ProtoIcon.ComfyLaunch", "");
            set => SetS(ref _comfyLaunch, "ProtoIcon.ComfyLaunch", value);
        }

        private static string _checkpoint;
        public static string Checkpoint
        {
            get => GetS(ref _checkpoint, "ProtoIcon.Checkpoint", "juggernautXL_v9.safetensors");
            set => SetS(ref _checkpoint, "ProtoIcon.Checkpoint", value);
        }

        /// <summary>Файл LoRA в models/loras. Пусто — чистый чекпоинт (обычно лучше).</summary>
        private static string _loraName;
        public static string LoraName
        {
            get => GetS(ref _loraName, "ProtoIcon.Lora", "");
            set => SetS(ref _loraName, "ProtoIcon.Lora", value);
        }

        private static float? _loraStrength;
        public static float LoraStrength
        {
            get => GetF(ref _loraStrength, "ProtoIcon.LoraStrength", 0.65f);
            set => SetF(ref _loraStrength, "ProtoIcon.LoraStrength", value);
        }

        /// <summary>Триггер-слово LoRA, добавляется в начало промпта (Redmond V2 — «icons»).</summary>
        private static string _loraTrigger;
        public static string LoraTrigger
        {
            get => GetS(ref _loraTrigger, "ProtoIcon.LoraTrigger", "icons");
            set => SetS(ref _loraTrigger, "ProtoIcon.LoraTrigger", value);
        }

        // ── rembg (вырез фона) — машинный инструмент, обычно вне проекта ──

        private static string _rembgPython;
        public static string RembgPython
        {
            get => GetS(ref _rembgPython, "ProtoIcon.RembgPython", @"E:\AI\rembg\venv\Scripts\python.exe");
            set => SetS(ref _rembgPython, "ProtoIcon.RembgPython", value);
        }

        private static string _rembgScript;
        public static string RembgScript
        {
            get => GetS(ref _rembgScript, "ProtoIcon.RembgScript", @"E:\AI\rembg\process_icon.py");
            set => SetS(ref _rembgScript, "ProtoIcon.RembgScript", value);
        }

        /// <summary>
        /// Модель выреза фона. isnet-general-use — дефолт (~4 с/картинка на CPU, на игровых
        /// генерациях с чистым фоном не уступает). birefnet-general — точнее края на тонких
        /// структурах, но ~23 с на CPU; birefnet-general-lite — компромисс (~14 с).
        /// </summary>
        private static string _rembgModel;
        public static string RembgModel
        {
            get => GetS(ref _rembgModel, "ProtoIcon.RembgModel", "isnet-general-use");
            set => SetS(ref _rembgModel, "ProtoIcon.RembgModel", value);
        }

        // ── Референсы стиля (IPAdapter): все генерации тянутся к эталонным картинкам ──

        /// <summary>
        /// Asset-пути спрайтов-эталонов. Пусто — IPAdapter выключен. Несколько эталонов ЛУЧШЕ
        /// одного: CLIP-эмбеддинги усредняются — композиции отдельных иконок гасят друг друга,
        /// остаётся общий стиль (материалы, контур, свет). Работает на SDXL, не на Flux.
        /// </summary>
        private static System.Collections.Generic.List<string> _styleRefs;
        public static System.Collections.Generic.List<string> StyleRefAssetPaths
        {
            get
            {
                if (_styleRefs != null) return new System.Collections.Generic.List<string>(_styleRefs);

                string raw = EditorPrefs.GetString("ProtoIcon.StyleRefPaths", null);
                _styleRefs = new System.Collections.Generic.List<string>();
                if (raw == null)
                {
                    // миграция со старой одиночной настройки
                    string old = EditorPrefs.GetString("ProtoIcon.StyleRefPath", "");
                    if (!string.IsNullOrEmpty(old)) _styleRefs.Add(old);
                }
                else
                {
                    foreach (var s in raw.Split(';'))
                        if (!string.IsNullOrEmpty(s)) _styleRefs.Add(s);
                }
                return new System.Collections.Generic.List<string>(_styleRefs);
            }
            set
            {
                _styleRefs = value == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(value);
                EditorPrefs.SetString("ProtoIcon.StyleRefPaths", string.Join(";", _styleRefs));
            }
        }

        /// <summary>Сила притяжения к референсу: 0.3–0.5 — стиль, 0.7+ — почти копирование композиции.</summary>
        private static float? _styleRefWeight;
        public static float StyleRefWeight
        {
            get => GetF(ref _styleRefWeight, "ProtoIcon.StyleRefWeight", 0.5f);
            set => SetF(ref _styleRefWeight, "ProtoIcon.StyleRefWeight", value);
        }

        /// <summary>
        /// Режим IPAdapter: "style transfer" — переносит палитру/подачу без композиции (дефолт,
        /// чище и проще); "standard" — тянет и композицию с плашками (сильнее похоже на референс).
        /// </summary>
        private static string _styleRefMode;
        public static string StyleRefMode
        {
            get => GetS(ref _styleRefMode, "ProtoIcon.StyleRefMode", "style transfer");
            set => SetS(ref _styleRefMode, "ProtoIcon.StyleRefMode", value);
        }

        // ── Дефолтный стиль-пресет (GUID ассета IconStylePreset) ──

        private static string _defaultStyleGuid;
        public static string DefaultStyleGuid
        {
            get => GetS(ref _defaultStyleGuid, "ProtoIcon.DefaultStyleGuid", "");
            set => SetS(ref _defaultStyleGuid, "ProtoIcon.DefaultStyleGuid", value);
        }
    }
}
