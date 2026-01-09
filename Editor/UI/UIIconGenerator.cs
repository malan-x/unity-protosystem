// Packages/com.protosystem.core/Editor/UI/UIIconGenerator.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Процедурная генерация UI спрайтов и элементов с параметризацией стиля
    /// </summary>
    public static class UIIconGenerator
    {
        // Метки для поиска сгенерированных спрайтов
        public const string LABEL_UI_SPRITE = "UISprite";
        public const string LABEL_UI_ICON = "UIIcon";
        public const string LABEL_UI_BACKGROUND = "UIBackground";
        public const string LABEL_UI_BUTTON = "UIButton";
        public const string LABEL_UI_CHECKBOX = "UICheckbox";
        public const string LABEL_UI_SLIDER = "UISlider";
        public const string LABEL_UI_DROPDOWN = "UIDropdown";

        /// <summary>
        /// Генерирует все UI элементы на основе конфигурации стиля
        /// </summary>
        public static void GenerateAllSprites(UIStyleConfiguration config, string outputPath)
        {
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                CreateFolderRecursive(outputPath);
            }

            Debug.Log($"[UIIconGenerator] === НАЧАЛО ГЕНЕРАЦИИ ===");
            Debug.Log($"[UIIconGenerator] Config: {config.name}");
            Debug.Log($"[UIIconGenerator] BorderWidth: {config.borderWidth}, BorderColor: {config.borderColor}");
            Debug.Log($"[UIIconGenerator] WindowRadius: {config.windowBorderRadius}, ButtonRadius: {config.buttonBorderRadius}");

            // Иконки
            GenerateCheckmarkIcon(config, outputPath);
            GenerateArrowDownIcon(config, outputPath);
            GenerateArrowRightIcon(config, outputPath);
            GenerateCloseIcon(config, outputPath);
            GenerateCloseButton(config, outputPath);
            
            // Фоны элементов - БЕЛЫЕ спрайты, цвет задаётся через Image.color
            GenerateRoundedRect(config, outputPath, "WindowBackground", config.windowBorderRadius, Color.white, LABEL_UI_BACKGROUND);
            GenerateRoundedRect(config, outputPath, "ButtonBackground", config.buttonBorderRadius, Color.white, LABEL_UI_BUTTON);
            GenerateRoundedRect(config, outputPath, "ButtonBackgroundSecondary", config.buttonBorderRadius, Color.white, LABEL_UI_BUTTON);
            GenerateRoundedRect(config, outputPath, "InputBackground", config.inputBorderRadius, Color.white, LABEL_UI_BACKGROUND);
            GenerateRoundedRect(config, outputPath, "CheckboxBackground", config.checkboxBorderRadius, Color.white, LABEL_UI_CHECKBOX);
            
            // Dropdown - отдельный спрайт с меньшим радиусом (8px как в HTML)
            GenerateDropdownBackground(config, outputPath);
            
            // Элементы слайдера - тоже белые
            GenerateSliderTrack(config, outputPath);
            GenerateSliderHandle(config, outputPath);
            GenerateSliderFill(config, outputPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UIIconGenerator] === ГЕНЕРАЦИЯ ЗАВЕРШЕНА в {outputPath} ===");
        }

        /// <summary>
        /// Рекурсивно создаёт папки
        /// </summary>
        private static void CreateFolderRecursive(string path)
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];
            
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                currentPath = newPath;
            }
        }

        /// <summary>
        /// Генерирует галочку для checkbox
        /// </summary>
        private static void GenerateCheckmarkIcon(UIStyleConfiguration config, string outputPath)
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            // Прозрачный фон
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Рисуем галочку БЕЛЫМ цветом (цвет будет задан через Image.color)
            // ИНВЕРТИРУЕМ Y координаты (Unity Y=0 внизу)
            int strokeWidth = Mathf.Max(config.iconStrokeWidth, 4);
            // Галочка: левая нижняя -> центр -> правая верхняя
            DrawLineAntiAliased(pixels, size, 12, size - 32, 26, size - 46, Color.white, strokeWidth);
            DrawLineAntiAliased(pixels, size, 26, size - 46, 52, size - 18, Color.white, strokeWidth);

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "Checkmark.png", LABEL_UI_ICON, LABEL_UI_CHECKBOX);
            Debug.Log($"[UIIconGenerator] Checkmark: size={size}, strokeWidth={strokeWidth}, Y-INVERTED");
        }

        /// <summary>
        /// Генерирует стрелку вниз для dropdown (треугольник)
        /// </summary>
        private static void GenerateArrowDownIcon(UIStyleConfiguration config, string outputPath)
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Треугольник вниз - БЕЛЫЙ (цвет через Image.color)
            // ИНВЕРТИРУЕМ Y координаты (Unity Y=0 внизу)
            // Вершины: нижний левый (6, 21), нижний правый (26, 21), верхний центр (16, 11)
            Vector2 p1 = new Vector2(6, size - 11);   // верхний левый (в Unity это низ)
            Vector2 p2 = new Vector2(26, size - 11);  // верхний правый  
            Vector2 p3 = new Vector2(16, size - 21);  // нижний центр (в Unity это верх)

            float antiAliasWidth = 1.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    
                    // Расстояние до треугольника с учётом anti-aliasing
                    float dist = DistanceToTriangle(p, p1, p2, p3);
                    float alpha = 1f - Mathf.Clamp01(dist / antiAliasWidth);
                    
                    if (alpha > 0.01f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "ArrowDown.png", LABEL_UI_ICON);
            Debug.Log($"[UIIconGenerator] ArrowDown: size={size}, triangle Y-INVERTED, points=({p1}, {p2}, {p3})");
        }

        /// <summary>
        /// Расстояние от точки до треугольника (отрицательное = внутри)
        /// </summary>
        private static float DistanceToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Проверяем, внутри ли точка треугольника
            float sign(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            }

            float d1 = sign(p, a, b);
            float d2 = sign(p, b, c);
            float d3 = sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            if (!(hasNeg && hasPos))
            {
                // Внутри треугольника
                return -1f;
            }

            // Вне треугольника - расстояние до ближайшего ребра
            float distAB = DistanceToSegment(p, a, b);
            float distBC = DistanceToSegment(p, b, c);
            float distCA = DistanceToSegment(p, c, a);

            return Mathf.Min(distAB, Mathf.Min(distBC, distCA));
        }

        /// <summary>
        /// Расстояние от точки до отрезка
        /// </summary>
        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
            Vector2 closest = a + t * ab;
            return Vector2.Distance(p, closest);
        }

        /// <summary>
        /// Генерирует стрелку вправо
        /// </summary>
        private static void GenerateArrowRightIcon(UIStyleConfiguration config, string outputPath)
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Треугольник вправо - БЕЛЫЙ
            // ИНВЕРТИРУЕМ Y координаты (Unity Y=0 внизу)
            Vector2 p1 = new Vector2(11, size - 6);   // верхний (в Unity)
            Vector2 p2 = new Vector2(11, size - 26);  // нижний (в Unity)
            Vector2 p3 = new Vector2(21, size - 16);  // правый центр

            float antiAliasWidth = 1.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = DistanceToTriangle(p, p1, p2, p3);
                    float alpha = 1f - Mathf.Clamp01(dist / antiAliasWidth);
                    
                    if (alpha > 0.01f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "ArrowRight.png", LABEL_UI_ICON);
            Debug.Log($"[UIIconGenerator] ArrowRight: size={size}, Y-INVERTED");
        }

        /// <summary>
        /// Генерирует крестик для кнопки закрытия
        /// </summary>
        private static void GenerateCloseIcon(UIStyleConfiguration config, string outputPath)
        {
            int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            int strokeWidth = config.iconStrokeWidth;
            DrawLineAntiAliased(pixels, size, 12, 12, 36, 36, Color.white, strokeWidth);
            DrawLineAntiAliased(pixels, size, 36, 12, 12, 36, Color.white, strokeWidth);

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "Close.png", LABEL_UI_ICON);
        }

        /// <summary>
        /// Круглая кнопка закрытия с X внутри (для правого верхнего угла окон)
        /// </summary>
        private static void GenerateCloseButton(UIStyleConfiguration config, string outputPath)
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            float center = size / 2f;
            float radius = size / 2f - 2f;
            float aa = 1.5f;
            float borderWidth = Mathf.Max(config.borderWidth * 2f, 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    // SDF до края круга (отрицательно внутри)
                    float distOuter = dist - radius;
                    float alpha = 1f - Mathf.Clamp01(distOuter / aa);
                    
                    if (alpha > 0.001f)
                    {
                        // Маска рамки
                        float distFromEdge = -distOuter;
                        float borderMask = 0f;
                        
                        if (borderWidth > 0.01f)
                        {
                            if (distFromEdge < borderWidth - aa)
                                borderMask = 1f;
                            else if (distFromEdge < borderWidth + aa)
                                borderMask = 1f - Mathf.Clamp01((distFromEdge - borderWidth + aa) / (aa * 2f));
                        }
                        
                        // PREMULTIPLIED ALPHA
                        float borderContrib = borderMask * alpha;
                        float fillContrib = (1f - borderMask) * alpha;
                        
                        pixels[y * size + x] = new Color(borderContrib, fillContrib, 0f, alpha);
                    }
                }
            }

            // Рисуем X поверх
            int strokeWidth = config.iconStrokeWidth;
            int margin = 18;
            DrawLineAntiAliased(pixels, size, margin, margin, size - margin, size - margin, Color.white, strokeWidth);
            DrawLineAntiAliased(pixels, size, size - margin, margin, margin, size - margin, Color.white, strokeWidth);

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "CloseButton.png", LABEL_UI_ICON, LABEL_UI_BUTTON);
            Debug.Log($"[UIIconGenerator] CloseButton: size={size}, borderWidth={borderWidth}");
        }

        /// <summary>
        /// Генерирует прямоугольник со скруглёнными углами
        /// Для шейдера UI/TwoColor: R канал = рамка (1.0), заливка (0.0), A = общая альфа
        /// 
        /// ТОЧНЫЙ ПОДХОД: используем SDF для определения зоны рамки
        /// Рамка = область между внешним краем и внутренним краем (на расстоянии borderWidth)
        /// </summary>
        private static void GenerateRoundedRect(UIStyleConfiguration config, string outputPath, string name, int radius, Color fillColor, params string[] labels)
        {
            int size = 128;
            int scaledRadius = Mathf.Min(radius * 2, size / 2 - 4);
            
            // borderWidth в пикселях текстуры (×2 для 128px)
            float borderWidth = Mathf.Max(config.borderWidth * 2f, 1f);

            Debug.Log($"[UIIconGenerator] GenerateRoundedRect: {name}");
            Debug.Log($"[UIIconGenerator]   size={size}, radius={radius}, scaledRadius={scaledRadius}");
            Debug.Log($"[UIIconGenerator]   borderWidth={config.borderWidth}, scaledBorderWidth={borderWidth}");

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            float aa = 1.0f; // anti-alias width

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    
                    // SDF до внешнего края (отрицательно внутри формы)
                    float distOuter = CalculateRoundedRectSDF(px, py, size, scaledRadius);
                    
                    // Альфа формы (внешний край с AA)
                    float alpha = 1f - Mathf.Clamp01(distOuter / aa);
                    
                    if (alpha > 0.001f)
                    {
                        float borderMask;
                        
                        if (config.borderWidth > 0.01f)
                        {
                            // distOuter отрицательный внутри формы
                            // -distOuter = расстояние ОТ внешнего края вглубь формы
                            // Рамка там где -distOuter < borderWidth
                            
                            float distFromEdge = -distOuter; // Расстояние от внешнего края вглубь
                            
                            // Плавный переход на внутренней границе рамки
                            // borderMask = 1 у внешнего края, плавно переходит в 0 на расстоянии borderWidth
                            if (distFromEdge < borderWidth - aa)
                            {
                                // Внутри зоны рамки
                                borderMask = 1f;
                            }
                            else if (distFromEdge < borderWidth + aa)
                            {
                                // Переходная зона
                                borderMask = 1f - Mathf.Clamp01((distFromEdge - borderWidth + aa) / (aa * 2f));
                            }
                            else
                            {
                                // Внутри заливки
                                borderMask = 0f;
                            }
                        }
                        else
                        {
                            // Без рамки - всё заливка
                            borderMask = 0f;
                        }
                        
                        pixels[y * size + x] = new Color(borderMask, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            List<string> allLabels = new List<string> { LABEL_UI_SPRITE, LABEL_UI_BACKGROUND };
            allLabels.AddRange(labels);

            SaveTextureWithBorder(texture, outputPath, $"{name}.png", scaledRadius, allLabels.ToArray());
        }

        /// <summary>
        /// Генерирует фон для Dropdown - прямоугольник со скруглёнными углами С РАМКОЙ
        /// </summary>
        private static void GenerateDropdownBackground(UIStyleConfiguration config, string outputPath)
        {
            int width = 128;
            int height = 64;
            int radius = config.dropdownBorderRadius;
            int scaledRadius = radius * 2;
            float borderWidth = Mathf.Max(config.borderWidth * 2f, 1f);
            
            Debug.Log($"[UIIconGenerator] DropdownBackground: {width}x{height}, radius={radius}, borderWidth={borderWidth}");

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            float aa = 1.0f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    
                    // SDF до внешнего края
                    float distOuter = CalculateRoundedRectSDFNonSquare(px, py, width, height, scaledRadius);
                    float alpha = 1f - Mathf.Clamp01(distOuter / aa);
                    
                    if (alpha > 0.001f)
                    {
                        float borderMask;
                        
                        if (config.borderWidth > 0.01f)
                        {
                            float distFromEdge = -distOuter;
                            
                            if (distFromEdge < borderWidth - aa)
                            {
                                borderMask = 1f;
                            }
                            else if (distFromEdge < borderWidth + aa)
                            {
                                borderMask = 1f - Mathf.Clamp01((distFromEdge - borderWidth + aa) / (aa * 2f));
                            }
                            else
                            {
                                borderMask = 0f;
                            }
                        }
                        else
                        {
                            borderMask = 0f;
                        }
                        
                        pixels[y * width + x] = new Color(borderMask, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTextureWithBorder(texture, outputPath, "DropdownBackground.png", scaledRadius, LABEL_UI_SPRITE, LABEL_UI_DROPDOWN);
        }

        /// <summary>
        /// SDF для не-квадратного прямоугольника с закруглёнными углами
        /// </summary>
        private static float CalculateRoundedRectSDFNonSquare(float px, float py, int width, int height, int radius)
        {
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;
            float r = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));
            
            // Переводим в координаты относительно центра
            float x = Mathf.Abs(px - halfWidth);
            float y = Mathf.Abs(py - halfHeight);
            
            // Расстояние до скруглённого угла
            float dx = Mathf.Max(0, x - (halfWidth - r));
            float dy = Mathf.Max(0, y - (halfHeight - r));
            
            float cornerDist = Mathf.Sqrt(dx * dx + dy * dy) - r;
            float edgeDistX = x - halfWidth;
            float edgeDistY = y - halfHeight;
            
            return Mathf.Max(cornerDist, Mathf.Max(edgeDistX, edgeDistY));
        }

        /// <summary>
        /// Signed Distance Function для скруглённого прямоугольника
        /// Возвращает отрицательное значение внутри, положительное снаружи
        /// </summary>
        private static float CalculateRoundedRectSDF(float px, float py, int size, int radius)
        {
            float halfSize = size / 2f;
            float r = radius;
            
            // Переводим в координаты относительно центра
            float x = Mathf.Abs(px - halfSize);
            float y = Mathf.Abs(py - halfSize);
            
            // Расстояние до скруглённого угла
            float dx = Mathf.Max(0, x - (halfSize - r));
            float dy = Mathf.Max(0, y - (halfSize - r));
            
            float cornerDist = Mathf.Sqrt(dx * dx + dy * dy) - r;
            float edgeDist = Mathf.Max(x - halfSize, y - halfSize);
            
            return Mathf.Max(cornerDist, edgeDist);
        }

        /// <summary>
        /// Генерирует трек слайдера с anti-aliasing - БЕЛЫЙ
        /// Текстура 1:1 с UI размером чтобы избежать масштабирования
        /// </summary>
        private static void GenerateSliderTrack(UIStyleConfiguration config, string outputPath)
        {
            // Текстура 1:1 с UI - без масштабирования!
            int height = Mathf.Max(config.sliderTrackHeight, 4);
            int width = Mathf.Max(height * 8, 32); // Достаточно для 9-slice
            
            // Радиус: если 0 - автоматически половина высоты (полностью круглые концы)
            int cornerRadius = config.sliderBorderRadius > 0 
                ? config.sliderBorderRadius 
                : height / 2;

            Debug.Log($"[UIIconGenerator] SliderTrack: width={width}, height={height}, cornerRadius={cornerRadius} (1:1 NO SCALE)");

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            float aa = 0.75f; // Меньше AA для маленькой текстуры

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float centerY = height / 2f;
                    float distance;

                    if (x < cornerRadius)
                    {
                        float dx = cornerRadius - x - 0.5f;
                        float dy = y - centerY + 0.5f;
                        distance = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                    }
                    else if (x >= width - cornerRadius)
                    {
                        float dx = x - (width - cornerRadius) + 0.5f;
                        float dy = y - centerY + 0.5f;
                        distance = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                    }
                    else
                    {
                        distance = Mathf.Abs(y - centerY + 0.5f) - cornerRadius;
                    }

                    float alpha = 1f - Mathf.Clamp01(distance / aa);

                    if (alpha > 0f)
                    {
                        pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Border для 9-slice = cornerRadius
            SaveTextureWithBorder(texture, outputPath, "SliderTrack.png", cornerRadius, LABEL_UI_SPRITE, LABEL_UI_SLIDER);
        }

        /// <summary>
        /// Генерирует ручку слайдера (круглая) - БЕЛАЯ
        /// </summary>
        private static void GenerateSliderHandle(UIStyleConfiguration config, string outputPath)
        {
            int size = config.sliderHandleSize * 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            float center = size / 2f;
            float radius = size / 2f - 2f;
            float antiAliasWidth = 1.5f;

            Debug.Log($"[UIIconGenerator] SliderHandle: size={size}, radius={radius}");

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    float alpha = 1f - Mathf.Clamp01(distance / antiAliasWidth);

                    if (alpha > 0f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTexture(texture, outputPath, "SliderHandle.png", LABEL_UI_SPRITE, LABEL_UI_SLIDER);
        }

        /// <summary>
        /// Генерирует заливку слайдера - БЕЛАЯ
        /// Текстура генерируется 1:1 с UI размером
        /// </summary>
        private static void GenerateSliderFill(UIStyleConfiguration config, string outputPath)
        {
            // Текстура 1:1 с UI - без масштабирования!
            int height = Mathf.Max(config.sliderTrackHeight, 4);
            int width = Mathf.Max(height * 8, 32);
            
            int cornerRadius = config.sliderBorderRadius > 0 
                ? config.sliderBorderRadius 
                : height / 2;
            
            Debug.Log($"[UIIconGenerator] SliderFill: width={width}, height={height}, cornerRadius={cornerRadius} (1:1 NO SCALE)");
            
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            float aa = 0.75f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float centerY = height / 2f;
                    float distance;
                    
                    if (x < cornerRadius)
                    {
                        float dx = cornerRadius - x - 0.5f;
                        float dy = y - centerY + 0.5f;
                        distance = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                    }
                    else if (x >= width - cornerRadius)
                    {
                        float dx = x - (width - cornerRadius) + 0.5f;
                        float dy = y - centerY + 0.5f;
                        distance = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                    }
                    else
                    {
                        distance = Mathf.Abs(y - centerY + 0.5f) - cornerRadius;
                    }

                    float alpha = 1f - Mathf.Clamp01(distance / aa);

                    if (alpha > 0f)
                    {
                        pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            SaveTextureWithBorder(texture, outputPath, "SliderFill.png", cornerRadius, LABEL_UI_SPRITE, LABEL_UI_SLIDER);
        }

        /// <summary>
        /// Рисует линию с anti-aliasing
        /// </summary>
        private static void DrawLineAntiAliased(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            float halfThickness = thickness / 2f;
            
            // Bounding box
            int minX = Mathf.Max(0, Mathf.Min(x0, x1) - thickness);
            int maxX = Mathf.Min(size - 1, Mathf.Max(x0, x1) + thickness);
            int minY = Mathf.Max(0, Mathf.Min(y0, y1) - thickness);
            int maxY = Mathf.Min(size - 1, Mathf.Max(y0, y1) + thickness);
            
            Vector2 a = new Vector2(x0, y0);
            Vector2 b = new Vector2(x1, y1);
            
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = DistanceToSegment(p, a, b);
                    float alpha = 1f - Mathf.Clamp01((dist - halfThickness + 1f) / 1.5f);
                    
                    if (alpha > 0f)
                    {
                        int idx = y * size + x;
                        Color existing = pixels[idx];
                        // Blend
                        float newAlpha = alpha + existing.a * (1f - alpha);
                        if (newAlpha > 0f)
                        {
                            pixels[idx] = new Color(
                                (color.r * alpha + existing.r * existing.a * (1f - alpha)) / newAlpha,
                                (color.g * alpha + existing.g * existing.a * (1f - alpha)) / newAlpha,
                                (color.b * alpha + existing.b * existing.a * (1f - alpha)) / newAlpha,
                                newAlpha
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Сохраняет текстуру как PNG и настраивает импорт с метками
        /// </summary>
        private static void SaveTexture(Texture2D texture, string outputPath, string filename, params string[] labels)
        {
            SaveTextureWithBorder(texture, outputPath, filename, 0, labels);
        }
        
        /// <summary>
        /// Сохраняет текстуру как спрайт с настройкой 9-slice border
        /// </summary>
        private static void SaveTextureWithBorder(Texture2D texture, string outputPath, string filename, int border, params string[] labels)
        {
            byte[] bytes = texture.EncodeToPNG();
            string fullPath = Path.Combine(outputPath, filename);
            File.WriteAllBytes(fullPath, bytes);

            AssetDatabase.ImportAsset(fullPath);

            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                
                if (border > 0)
                {
                    importer.spriteBorder = new Vector4(border, border, border, border);
                }
                
                importer.SaveAndReimport();
            }

            if (labels != null && labels.Length > 0)
            {
                AssetDatabase.SetLabels(AssetDatabase.LoadAssetAtPath<Object>(fullPath), labels);
            }
            
            Debug.Log($"[UIIconGenerator] Saved: {filename}, border={border}");
        }

        /// <summary>
        /// Поиск спрайта по метке
        /// </summary>
        public static Sprite FindSpriteByLabel(string label, string spriteName = null)
        {
            string[] guids = AssetDatabase.FindAssets($"l:{label} t:Sprite");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                if (spriteName == null || Path.GetFileNameWithoutExtension(path) == spriteName)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        Debug.Log($"[UIIconGenerator] FindSpriteByLabel: Found {spriteName} at {path}");
                        return sprite;
                    }
                }
            }

            Debug.LogWarning($"[UIIconGenerator] FindSpriteByLabel: NOT FOUND - label={label}, name={spriteName}");
            return null;
        }

        /// <summary>
        /// Загружает все спрайты с указанной меткой
        /// </summary>
        public static List<Sprite> LoadAllSpritesByLabel(string label)
        {
            List<Sprite> sprites = new List<Sprite>();
            string[] guids = AssetDatabase.FindAssets($"l:{label} t:Sprite");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    sprites.Add(sprite);
            }

            return sprites;
        }
    }
}
