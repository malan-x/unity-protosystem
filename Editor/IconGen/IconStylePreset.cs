using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>Каким движком рендерит стиль.</summary>
    public enum IconGenEngine
    {
        ComfyUI = 0,       // локальный SDXL/Flux
        PixelLabUI = 1,    // облачный PixelLab UI Creator — ЦЕЛЬНЫЕ панели/рамки (не иконки!)
        PixelLabImage = 2, // облачный PixelLab pixflux — отдельные пиксель-арт иконки/глифы
    }

    /// <summary>
    /// Стиль генерации иконок как ассет-настройка: шаблон промпта, негатив, параметры сэмплера.
    /// Сюжет ("что нарисовано") подставляется в {subject} — вызывающий передаёт только предмет,
    /// а весь стиль (материалы, освещение, фон) живёт здесь. Так разные наборы (иконки зданий,
    /// карты апгрейдов, спрайты врагов) получают свой стиль без правки кода.
    ///
    /// Ассеты-пресеты лежат в проекте, а не в пакете — пакет везёт только этот тип.
    /// </summary>
    [CreateAssetMenu(fileName = "IconStyle", menuName = "ProtoSystem/Icon Style Preset")]
    public class IconStylePreset : ScriptableObject
    {
        [Tooltip("Шаблон позитивного промпта. {subject} заменяется на сюжет из запроса.")]
        [TextArea(3, 8)]
        public string positiveTemplate =
            "game asset icon, {subject}, clean stylized game icon, bold clear readable shapes, " +
            "single object centered, thick outline, dark background, high quality, professional game art";

        [TextArea(2, 5)]
        public string negative =
            "blurry, low quality, deformed, ugly, watermark, text, signature, " +
            "sprite sheet, grid of items, multiple objects, tiled, cluttered, photo";

        [Header("Движок")]
        [Tooltip("ComfyUI — локальный SDXL/Flux. PixelLabUI — облачный UI Creator: цельные панели " +
                 "9-slice (всегда рисует панель!). PixelLabImage — облачный pixflux: отдельные " +
                 "пиксель-арт иконки/глифы. У PixelLab параметры сэмплера ниже игнорируются.")]
        public IconGenEngine engine = IconGenEngine.ComfyUI;

        [Tooltip("PixelLab: размер генерации. UI Creator: 192–688; pixflux: 32–400")]
        public Vector2Int pixelLabSize = new(256, 256);

        [Tooltip("PixelLab: палитра словами («rust brown, dark gold, dieselpunk»). Пусто — свободная")]
        public string pixelLabPalette = "";

        [Tooltip("PixelLab: элементы-каркасы (button, icon_button, toolbar, tab, panel, window, " +
                 "health_bar, avatar). Пусто — панель на весь кадр")]
        public string[] pixelLabElements;

        [Header("Параметры генерации")]
        public int size = 1024;      // нативное разрешение SDXL
        public int steps = 30;
        public float cfg = 7f;
        public string sampler = "dpmpp_2m";
        public string scheduler = "karras";

        [Tooltip("Добавлять триггер-слово LoRA в начало промпта (если LoRA включена в настройках).")]
        public bool prependLoraTrigger = true;

        [Header("Пиксель-арт")]
        [Tooltip("Квантовать результат в пиксельную сетку N px по ширине (16/32/64…). " +
                 "0 — выключено. Модель рисует «примерно пиксели», а этот шаг делает их " +
                 "честными: жёсткая сетка, жёсткая альфа — итоговое разрешение пиксель-арта.")]
        public int pixelGrid = 0;

        [Tooltip("Обрезать прозрачные поля перед квантованием: объект займёт весь кадр, " +
                 "при мелкой сетке на него придётся больше пикселей (важно для 16–32px).")]
        public bool pixelTrim = true;

        [Tooltip("Ограничить палитру N цветов (median-cut) — «честный» пиксель-look и чёткие " +
                 "плашки в мелком размере. 0 — без ограничения. Обычно 8–16 на иконку.")]
        public int pixelColors = 0;

        /// <summary>Собрать запрос к ComfyUI из этого стиля и конкретного сюжета.</summary>
        public ComfyUIClient.IconRequest Compose(string subject, int seed)
        {
            string prefix = prependLoraTrigger ? ComfyUIClient.LoraTriggerPrefix : "";
            string positive = prefix + positiveTemplate.Replace("{subject}", subject ?? "");

            return new ComfyUIClient.IconRequest
            {
                Positive = positive,
                Negative = negative,
                Seed = seed,
                Size = size,
                Steps = steps,
                Cfg = cfg,
                Sampler = sampler,
                Scheduler = scheduler,
                PixelGrid = pixelGrid,
                PixelTrim = pixelTrim,
                PixelColors = pixelColors,
            };
        }
    }
}
