using System;

namespace ProtoSystem.ModelGen.Editor
{
    public enum ModelEngine
    {
        Hunyuan3D = 0,   // локальный ComfyUI (image-to-3D + PBR-текстуры)
        Meshy     = 1,   // облако: image-to-3D, при пустом референсе — text-to-3D
        Imported  = 2,   // готовый файл со стороны: вставлен в студию из буфера/с диска
    }

    /// <summary>Запрос генерации 3D-модели. Полный рецепт — воспроизводим.</summary>
    [Serializable]
    public class ModelGenRequest
    {
        public string EntityId;
        public ModelEngine Engine = ModelEngine.Hunyuan3D;

        /// <summary>Текстовое описание (Meshy text-to-3D; для Hunyuan — только подпись).</summary>
        public string Prompt = "";
        /// <summary>Абсолютный путь референс-картинки (image-to-3D). Может быть null.</summary>
        public string RefImagePath;

        public int   Seed        = 42;
        public int   Steps       = 25;    // диффузия меша (Hunyuan)
        public float Guidance    = 5f;
        public int   OctreeRes   = 256;   // разрешение marching cubes
        public int   MaxFaces    = 30000;
        public int   PaintSteps  = 10;    // диффузия мультивью-текстур
        public int   ViewSize    = 512;
        public int   TextureSize = 1024;
    }

    /// <summary>Результат: GLB + метаданные для истории вариантов.</summary>
    public class ModelGenResult
    {
        public ModelGenRequest Request;
        public byte[] GlbBytes;
        public string Error;
        public bool Ok => Error == null && GlbBytes != null && GlbBytes.Length > 0;
    }
}
