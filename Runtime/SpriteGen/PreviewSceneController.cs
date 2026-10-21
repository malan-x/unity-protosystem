using System.Collections.Generic;
using UnityEngine;
#if PROTO_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProtoSystem.SpriteGen
{
    public class PreviewSceneController : MonoBehaviour
    {
        [Header("References")]
        public Camera previewCamera;
        public Light mainLight;
        public GameObject modelInstance;
        public Animator modelAnimator;
        public SpriteGenConfig config;

        [Header("Camera Orbit")]
        public float orbitSpeed = 100f;
        public float zoomSpeed = 2f;
        public float panSpeed = 0.5f;

        [Header("State")]
        public float yaw;
        public float pitch = 30f;
        public float distance = 5f;
        public Vector3 orbitCenter = Vector3.zero;
        public int currentAnimationIndex;
        public float animationSpeed = 1f;
        public bool isPaused;
        public float scrubTime;

        // Данные для таймлайна
        private List<float> _editingFrameTimes = new List<float>();
        private Dictionary<int, float> _savedScrubTimes = new Dictionary<int, float>();
        private float _currentClipLength = 1f;
        private string _currentClipName = "";
        private bool _ready;
        private Vector2 _animListScroll;
        private bool _showHelp = true;

        // Angle preset
        private CameraAnglePreset _currentAnglePreset;
        private float[] _presetAngles = new float[] { 0f };
        private int _currentAngleIndex;

        // Sprite preview
        private List<Texture2D> _capturedFrames = new List<Texture2D>();
        private bool _spritePreviewPlaying;
        private int _spritePreviewFrame;
        private float _spritePreviewTimer;
        private float _spritePreviewFPS = 12f;
        private float _spritePreviewTexSize = 300f;
        private bool _isDraggingPreviewResize;

        // Sprite preview — in-scene
        private GameObject _spritePreviewGO;
        private SpriteRenderer _spriteRenderer;
        private List<Sprite> _capturedSprites = new List<Sprite>();
        private float _inSceneSpriteScale = 1f;
        private float _baseSceneScale = 1f;

        // Стили GUI (ленивая инициализация)
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _timelineMarkerStyle;
        private bool _stylesInitialized;

        // Live style editing
        private RenderStylePreset _workingPreset;
        private bool _ownWorkingPreset;
        private Material[][] _originalMaterials;
        private Material[] _styleMaterials;
        private RenderStyle _appliedStyle = (RenderStyle)(-1);
        private GameObject _backLightGO;
        private GameObject _topAccentGO;
        private bool _showStyleSection = true;
        private bool _showStyleParams = true;
        private bool _showStyleLights = false;
        private bool _showStyleSobel = false;

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            _selectedButtonStyle = new GUIStyle(GUI.skin.button);
            _selectedButtonStyle.normal.textColor = Color.yellow;
            _selectedButtonStyle.fontStyle = FontStyle.Bold;

            _timelineMarkerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 9,
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        private void Start()
        {
            // В Play mode — подхватываем сериализованные ссылки
            if (modelInstance != null && previewCamera != null)
            {
                modelAnimator = modelInstance.GetComponent<Animator>();

                // Пересчитать bounds если orbitCenter не был задан
                if (orbitCenter == Vector3.zero)
                {
                    var renderers = modelInstance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                            bounds.Encapsulate(renderers[i].bounds);
                        orbitCenter = bounds.center;
                    }
                }

                LoadFrameTimesFromConfig(0);
                InitAnglePreset();
                EnsureWorkingPreset();
                CaptureOriginalMaterialsSnapshot();
                ReapplyStyleFromPreset();
                _ready = true;
                UpdateCameraPosition();
            }
        }

        private void EnsureWorkingPreset()
        {
            if (config != null && config.stylePreset != null)
            {
                _workingPreset = config.stylePreset;
                _ownWorkingPreset = false;
            }
            else if (_workingPreset == null)
            {
                _workingPreset = ScriptableObject.CreateInstance<RenderStylePreset>();
                _workingPreset.name = "PreviewWorkingPreset";
                _ownWorkingPreset = true;
            }
        }

        private void CaptureOriginalMaterialsSnapshot()
        {
            if (modelInstance == null) return;
            if (_originalMaterials != null) return;
            _originalMaterials = StyleApplier.CaptureOriginalMaterials(modelInstance);
        }

        private void ReapplyStyleFromPreset()
        {
            if (modelInstance == null || _workingPreset == null) return;

            bool needsCustomMat =
                _workingPreset.style != RenderStyle.Realistic &&
                _workingPreset.style != RenderStyle.PixelArt;

            if (needsCustomMat)
            {
                bool styleChanged = _appliedStyle != _workingPreset.style;
                if (styleChanged || _styleMaterials == null)
                {
                    DisposeStyleMaterials();
                    _styleMaterials = StyleApplier.ApplyStyleToInstance(modelInstance, _workingPreset, _originalMaterials);
                }
                else
                {
                    StyleApplier.UpdateStyleMaterialPropertiesAll(_styleMaterials, _workingPreset);
                }
            }
            else
            {
                if (_originalMaterials != null)
                    StyleApplier.RestoreOriginalMaterials(modelInstance, _originalMaterials);
                DisposeStyleMaterials();
            }

            _appliedStyle = _workingPreset.style;
            RebuildExtraLights();
        }

        private void DisposeStyleMaterials()
        {
            if (_styleMaterials == null) return;
            foreach (var m in _styleMaterials)
                if (m != null) Destroy(m);
            _styleMaterials = null;
        }

        private void RebuildExtraLights()
        {
            if (_workingPreset == null) return;
            bool styleAcceptsLights = _workingPreset.style != RenderStyle.Silhouette;

            int lightLayer = modelInstance != null ? modelInstance.layer : 0;

            if (_workingPreset.enableBackLight && styleAcceptsLights)
            {
                if (_backLightGO == null)
                    _backLightGO = StyleApplier.CreateExtraDirectional(
                        "PreviewBackLight",
                        _workingPreset.backLightColor,
                        _workingPreset.backLightIntensity,
                        Quaternion.identity,
                        lightLayer);
                else if (_backLightGO.TryGetComponent<Light>(out var l))
                {
                    l.color = _workingPreset.backLightColor;
                    l.intensity = _workingPreset.backLightIntensity;
                    l.cullingMask = 1 << lightLayer;
                    _backLightGO.layer = lightLayer;
                }
            }
            else if (_backLightGO != null)
            {
                Destroy(_backLightGO);
                _backLightGO = null;
            }

            if (_workingPreset.enableTopAccentLight && styleAcceptsLights)
            {
                if (_topAccentGO == null)
                    _topAccentGO = StyleApplier.CreateExtraDirectional(
                        "PreviewTopAccent",
                        _workingPreset.topAccentColor,
                        _workingPreset.topAccentIntensity,
                        Quaternion.identity,
                        lightLayer);
                else if (_topAccentGO.TryGetComponent<Light>(out var l))
                {
                    l.color = _workingPreset.topAccentColor;
                    l.intensity = _workingPreset.topAccentIntensity;
                    l.cullingMask = 1 << lightLayer;
                    _topAccentGO.layer = lightLayer;
                }
            }
            else if (_topAccentGO != null)
            {
                Destroy(_topAccentGO);
                _topAccentGO = null;
            }
        }

        private void LateUpdate()
        {
            if (!_ready || previewCamera == null) return;

            HandleInput();
            UpdateCameraPosition();
            UpdateScrubFromAnimator();
            UpdateSpritePreview();
        }

        public void Initialize(GameObject instance, Camera cam, Light light)
        {
            modelInstance = instance;
            previewCamera = cam;
            mainLight = light;
            modelAnimator = instance.GetComponent<Animator>();

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                orbitCenter = bounds.center;
                distance = bounds.size.magnitude * 1.5f;
            }

            LoadFrameTimesFromConfig(0);
        }

        // =================================================================
        // INPUT
        // =================================================================

        // Ввод — через Input System, если пакет есть в проекте; иначе старый Input Manager.
        // Наружу одинаковые величины: дельта мыши в пикселях за кадр, колесо ±120 за щелчок.
#if PROTO_HAS_INPUT_SYSTEM
        private static bool InputReady => Mouse.current != null && Keyboard.current != null;
        private static Vector2 MouseDelta() => Mouse.current.delta.ReadValue();
        private static bool RightMouse => Mouse.current.rightButton.isPressed;
        private static bool MiddleMouse => Mouse.current.middleButton.isPressed;
        private static float Scroll => Mouse.current.scroll.ReadValue().y;
        private static bool KeyDown(KeyCode key) => Keyboard.current[ToKey(key)].wasPressedThisFrame;
        private static bool KeyHeld(KeyCode key) => Keyboard.current[ToKey(key)].isPressed;

        private static Key ToKey(KeyCode key) => key switch
        {
            KeyCode.Space => Key.Space,
            KeyCode.H => Key.H,
            KeyCode.Q => Key.Q,
            KeyCode.E => Key.E,
            KeyCode.A => Key.A,
            KeyCode.D => Key.D,
            >= KeyCode.Alpha1 and <= KeyCode.Alpha9 => (Key)((int)Key.Digit1 + (key - KeyCode.Alpha1)),
            _ => Key.None,
        };
#else
        private Vector2 _lastMousePos;
        private static bool InputReady => true;
        private Vector2 MouseDelta()
        {
            Vector2 pos = Input.mousePosition;
            Vector2 delta = pos - _lastMousePos;
            _lastMousePos = pos;
            return delta;
        }
        private static bool RightMouse => Input.GetMouseButton(1);
        private static bool MiddleMouse => Input.GetMouseButton(2);
        private static float Scroll => Input.mouseScrollDelta.y * 120f;
        private static bool KeyDown(KeyCode key) => Input.GetKeyDown(key);
        private static bool KeyHeld(KeyCode key) => Input.GetKey(key);
#endif

        private void HandleInput()
        {
            if (!InputReady) return;

            var mouseDelta = MouseDelta();

            // ПКМ — вращение камеры
            if (RightMouse)
            {
                yaw += mouseDelta.x * orbitSpeed * Time.deltaTime * 0.1f;
                pitch -= mouseDelta.y * orbitSpeed * Time.deltaTime * 0.1f;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
            }

            // СКМ — перемещение
            if (MiddleMouse)
            {
                var right = previewCamera.transform.right;
                var up = previewCamera.transform.up;
                float dx = mouseDelta.x * 0.1f;
                float dy = mouseDelta.y * 0.1f;
                orbitCenter -= (right * dx + up * dy) * panSpeed * Time.deltaTime * distance;
            }

            // Колёсико — зум
            float scroll = Scroll;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance -= scroll * zoomSpeed * 0.01f * distance;
                distance = Mathf.Max(0.1f, distance);
            }

            // Space — пауза
            if (KeyDown(KeyCode.Space))
                TogglePause();

            // H — помощь
            if (KeyDown(KeyCode.H))
                _showHelp = !_showHelp;

            // 1-9 — переключение анимаций
            for (int i = 0; i < 9; i++)
            {
                if (KeyDown(KeyCode.Alpha1 + i))
                    SetAnimation(i);
            }

            // Q/E — переключение ракурса
            if (KeyDown(KeyCode.Q))
                CycleAngle(-1);
            if (KeyDown(KeyCode.E))
                CycleAngle(1);

            // A/D — скраб при паузе
            if (isPaused && modelAnimator != null)
            {
                float scrubDelta = 0f;
                if (KeyHeld(KeyCode.D)) scrubDelta = Time.unscaledDeltaTime * 0.5f;
                if (KeyHeld(KeyCode.A)) scrubDelta = -Time.unscaledDeltaTime * 0.5f;

                if (Mathf.Abs(scrubDelta) > 0f)
                {
                    scrubTime = Mathf.Clamp(scrubTime + scrubDelta, 0f, _currentClipLength);
                    ScrubTo(scrubTime);
                }
            }
        }

        // =================================================================
        // ANIMATION CONTROL
        // =================================================================

        public void TogglePause()
        {
            isPaused = !isPaused;
            if (modelAnimator != null)
                modelAnimator.speed = isPaused ? 0f : animationSpeed;
        }

        public void SetAnimation(int index)
        {
            if (modelAnimator == null) return;

            var clips = modelAnimator.runtimeAnimatorController.animationClips;
            if (index < 0 || index >= clips.Length) return;

            // Сохранить текущий кадр для текущей анимации
            _savedScrubTimes[currentAnimationIndex] = scrubTime;

            currentAnimationIndex = index;
            LoadFrameTimesFromConfig(index);

            // Восстановить сохранённый кадр или начало
            scrubTime = _savedScrubTimes.ContainsKey(index) ? _savedScrubTimes[index] : 0f;
            float normalized = _currentClipLength > 0f ? scrubTime / _currentClipLength : 0f;

            // Параметр для AnyState переходов
            modelAnimator.SetInteger("AnimationIndex", index);

            // Имя стейта — из конфига (совпадает с тем что создал AnimatorControllerBuilder)
            string stateName = (config != null && index < config.animations.Count)
                ? config.animations[index].name
                : clips[index].name;

            // Принудительно переключить стейт (даже на паузе)
            modelAnimator.speed = 1f;
            modelAnimator.Play(stateName, 0, normalized);
            modelAnimator.Update(0f);
            modelAnimator.speed = isPaused ? 0f : animationSpeed;
        }

        public void ScrubTo(float time)
        {
            scrubTime = time;
            if (modelAnimator == null) return;

            // Убедиться что аниматор стоит, но поза обновляется
            modelAnimator.speed = 0f;
            isPaused = true;

            float normalized = _currentClipLength > 0f ? time / _currentClipLength : 0f;
            normalized = Mathf.Clamp01(normalized);

            var stateInfo = modelAnimator.GetCurrentAnimatorStateInfo(0);
            modelAnimator.Play(stateInfo.fullPathHash, 0, normalized);
            modelAnimator.Update(0f);
        }

        private void UpdateScrubFromAnimator()
        {
            if (modelAnimator == null || isPaused) return;

            var stateInfo = modelAnimator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length > 0f)
            {
                _currentClipLength = stateInfo.length;
                scrubTime = (stateInfo.normalizedTime % 1f) * stateInfo.length;
            }
        }

        private float GetClipLength(int animIndex)
        {
            // Из AnimatorController — самый надёжный источник в рантайме
            if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null)
            {
                var clips = modelAnimator.runtimeAnimatorController.animationClips;
                if (animIndex < clips.Length && clips[animIndex] != null)
                    return clips[animIndex].length;
            }

            // Фоллбек на конфиг
            if (config != null && animIndex < config.animations.Count)
            {
                var entry = config.animations[animIndex];
                if (entry.clip != null)
                    return entry.clip.length;
            }

            return 1f;
        }

        private void LoadFrameTimesFromConfig(int animIndex)
        {
            _editingFrameTimes.Clear();
            _currentClipName = "";

            // Длина клипа — всегда из реального источника
            _currentClipLength = GetClipLength(animIndex);

            // Имя
            if (config != null && animIndex < config.animations.Count)
                _currentClipName = config.animations[animIndex].name;
            else if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null)
            {
                var clips = modelAnimator.runtimeAnimatorController.animationClips;
                if (animIndex < clips.Length)
                    _currentClipName = clips[animIndex].name;
            }

            // Точки из конфига
            if (config != null && animIndex < config.animations.Count)
            {
                var entry = config.animations[animIndex];
                if (entry.frameMode == FrameSelectionMode.Custom
                    && entry.customFrameTimes != null
                    && entry.customFrameTimes.Length > 0)
                {
                    _editingFrameTimes.AddRange(entry.customFrameTimes);
                }
                else
                {
                    // Равномерно по реальной длине клипа
                    int count = Mathf.Max(1, entry.frameCount);
                    for (int i = 0; i < count; i++)
                    {
                        float t = count > 1
                            ? i * _currentClipLength / (count - 1)
                            : 0f;
                        _editingFrameTimes.Add(t);
                    }
                }
            }
            else
            {
                // Конфига нет — 8 равномерных точек по длине клипа
                for (int i = 0; i < 8; i++)
                {
                    float t = i * _currentClipLength / 7f;
                    _editingFrameTimes.Add(t);
                }
            }
        }

        private void SaveFrameTimesToConfig()
        {
            if (config == null || currentAnimationIndex >= config.animations.Count) return;

            var entry = config.animations[currentAnimationIndex];
            entry.frameMode = FrameSelectionMode.Custom;
            entry.customFrameTimes = _editingFrameTimes.ToArray();
            entry.frameCount = _editingFrameTimes.Count;

            Debug.Log($"Точки сохранены в конфиг: '{entry.name}' ({_editingFrameTimes.Count} кадров)");
        }

        // =================================================================
        // ANGLE PRESET
        // =================================================================

        private void InitAnglePreset()
        {
            if (config != null)
                _currentAnglePreset = config.anglePreset;
            else
                _currentAnglePreset = CameraAnglePreset.EightDirections;

            _presetAngles = GetAnglesForPreset(_currentAnglePreset);
            _currentAngleIndex = 0;

            if (_currentAnglePreset == CameraAnglePreset.TopDown)
                pitch = 90f;
            else if (_currentAnglePreset == CameraAnglePreset.Side)
                pitch = 0f;
        }

        private void SetAnglePreset(CameraAnglePreset preset)
        {
            _currentAnglePreset = preset;
            _presetAngles = GetAnglesForPreset(preset);
            _currentAngleIndex = 0;

            if (preset == CameraAnglePreset.TopDown)
                pitch = 90f;
            else if (preset == CameraAnglePreset.Side)
                pitch = 0f;
            else if (config != null)
                pitch = config.cameraPitch;

            yaw = _presetAngles[0];
        }

        private void CycleAngle(int direction)
        {
            if (_presetAngles == null || _presetAngles.Length <= 1) return;

            _currentAngleIndex = (_currentAngleIndex + direction + _presetAngles.Length)
                                 % _presetAngles.Length;
            yaw = _presetAngles[_currentAngleIndex];
        }

        private float[] GetAnglesForPreset(CameraAnglePreset preset)
        {
            switch (preset)
            {
                case CameraAnglePreset.TopDown:
                    return new float[] { 0f };
                case CameraAnglePreset.Side:
                    return new float[] { 0f };
                case CameraAnglePreset.FourDirections:
                    return new float[] { 0f, 90f, 180f, 270f };
                case CameraAnglePreset.EightDirections:
                    return new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
                case CameraAnglePreset.SixteenDirections:
                    var angles = new float[16];
                    for (int i = 0; i < 16; i++)
                        angles[i] = i * 22.5f;
                    return angles;
                case CameraAnglePreset.Custom:
                    if (config != null && config.customAngles != null && config.customAngles.Length > 0)
                        return config.customAngles;
                    return new float[] { 0f, 90f, 180f, 270f };
                default:
                    return new float[] { 0f };
            }
        }

        // =================================================================
        // SPRITE PREVIEW CAPTURE
        // =================================================================

        private void CaptureFrames()
        {
            // Cleanup old
            foreach (var tex in _capturedFrames)
                if (tex != null) Destroy(tex);
            _capturedFrames.Clear();

            foreach (var spr in _capturedSprites)
                if (spr != null) Destroy(spr);
            _capturedSprites.Clear();

            if (previewCamera == null || modelAnimator == null) return;

            // Скрыть in-scene спрайт, чтобы он не попал в захват
            if (_spritePreviewGO != null) _spritePreviewGO.SetActive(false);

            float savedScrub = scrubTime;
            bool savedPause = isPaused;
            float savedSpeed = modelAnimator.speed;

            // Принудительный пересчёт мешей при каждом рендере
            var skinnedRenderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
                smr.forceMatrixRecalculationPerRender = true;

            // Имя текущего стейта для Play()
            string stateName = (config != null && currentAnimationIndex < config.animations.Count)
                ? config.animations[currentAnimationIndex].name
                : null;

            int w = config != null ? config.spriteWidth : 128;
            int h = config != null ? config.spriteHeight : 128;

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = previewCamera.targetTexture;
            var prevAspect = previewCamera.aspect;
            previewCamera.targetTexture = rt;
            previewCamera.aspect = (float)w / h;

            modelAnimator.speed = 1f;

            for (int i = 0; i < _editingFrameTimes.Count; i++)
            {
                float time = _editingFrameTimes[i];
                float normalized = _currentClipLength > 0f ? time / _currentClipLength : 0f;
                normalized = Mathf.Clamp01(normalized);

                // Прямая установка позы через Play + Update
                if (!string.IsNullOrEmpty(stateName))
                    modelAnimator.Play(stateName, 0, normalized);
                else
                    modelAnimator.Play(0, 0, normalized);

                // Два вызова Update — гарантирует применение позы к костям
                modelAnimator.Update(0f);
                modelAnimator.Update(0f);

                previewCamera.Render();

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                if (_workingPreset != null && _workingPreset.style == RenderStyle.PixelArt)
                    tex = StyleApplier.ApplyPixelArt(tex, _workingPreset.pixelArtDownscale);

                if (_workingPreset != null && _workingPreset.enableSobelOutlinePost)
                    tex = StyleApplier.ApplySobelOutline(tex, _workingPreset);

                _capturedFrames.Add(tex);

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0f),
                    100f);
                _capturedSprites.Add(sprite);
            }

            previewCamera.targetTexture = prevTarget;
            previewCamera.aspect = prevAspect;
            RenderTexture.ReleaseTemporary(rt);

            // Вернуть настройки
            foreach (var smr in skinnedRenderers)
                smr.forceMatrixRecalculationPerRender = false;

            if (_spritePreviewGO != null) _spritePreviewGO.SetActive(true);

            ScrubTo(savedScrub);
            if (!savedPause)
            {
                isPaused = false;
                modelAnimator.speed = savedSpeed;
            }
            else
            {
                modelAnimator.speed = 0f;
            }

            // Создать / обновить объект-спрайт в сцене
            EnsureSpritePreviewObject(h);

            _spritePreviewFrame = 0;
            _spritePreviewTimer = 0f;
            _spritePreviewPlaying = true;
        }

        private void EnsureSpritePreviewObject(int texHeight)
        {
            if (_spritePreviewGO == null)
            {
                _spritePreviewGO = new GameObject("SpriteAnimPreview");
                _spriteRenderer = _spritePreviewGO.AddComponent<SpriteRenderer>();
                _spriteRenderer.sortingOrder = 100;
            }

            // Базовый масштаб: подогнать высоту спрайта под высоту модели
            if (modelInstance != null)
            {
                var renderers = modelInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    float modelHeight = bounds.size.y;
                    float spriteWorldHeight = texHeight / 100f; // pixelsPerUnit = 100
                    _baseSceneScale = modelHeight / Mathf.Max(0.01f, spriteWorldHeight);
                    _spritePreviewGO.transform.localScale = Vector3.one * _baseSceneScale * _inSceneSpriteScale;

                    // Позиция — справа от модели
                    _spritePreviewGO.transform.position = new Vector3(
                        bounds.max.x + bounds.size.x * 0.8f,
                        bounds.min.y,
                        bounds.center.z);
                }
            }
        }

        private void UpdateSpritePreview()
        {
            if (_capturedSprites.Count == 0) return;

            // Прокрутка кадров при воспроизведении
            if (_spritePreviewPlaying)
            {
                _spritePreviewTimer += Time.unscaledDeltaTime;
                float interval = 1f / Mathf.Max(1f, _spritePreviewFPS);

                if (_spritePreviewTimer >= interval)
                {
                    _spritePreviewTimer -= interval;
                    _spritePreviewFrame = (_spritePreviewFrame + 1) % _capturedSprites.Count;
                }
            }

            // Обновить SpriteRenderer в сцене
            if (_spriteRenderer != null && _spritePreviewFrame < _capturedSprites.Count)
                _spriteRenderer.sprite = _capturedSprites[_spritePreviewFrame];

            // Billboard — спрайт всегда повёрнут к камере
            if (_spritePreviewGO != null && previewCamera != null)
            {
                Vector3 dir = previewCamera.transform.position - _spritePreviewGO.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    _spritePreviewGO.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            }
        }

        private void OnDestroy()
        {
            foreach (var tex in _capturedFrames)
                if (tex != null) Destroy(tex);
            _capturedFrames.Clear();

            foreach (var spr in _capturedSprites)
                if (spr != null) Destroy(spr);
            _capturedSprites.Clear();

            if (_spritePreviewGO != null)
                Destroy(_spritePreviewGO);

            if (modelInstance != null && _originalMaterials != null)
                StyleApplier.RestoreOriginalMaterials(modelInstance, _originalMaterials);

            if (_backLightGO != null) Destroy(_backLightGO);
            if (_topAccentGO != null) Destroy(_topAccentGO);
            DisposeStyleMaterials();
            if (_ownWorkingPreset && _workingPreset != null) Destroy(_workingPreset);
        }

        // =================================================================
        // CAMERA
        // =================================================================

        private void UpdateCameraPosition()
        {
            float pitchRad = pitch * Mathf.Deg2Rad;
            float yawRad = yaw * Mathf.Deg2Rad;

            var offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * distance;

            previewCamera.transform.position = orbitCenter + offset;
            previewCamera.transform.LookAt(orbitCenter);

            if (mainLight != null)
                mainLight.transform.rotation = Quaternion.Euler(50f, yaw + 30f, 0f);

            if (_backLightGO != null && _workingPreset != null)
                _backLightGO.transform.rotation = Quaternion.Euler(
                    _workingPreset.backLightPitch,
                    yaw + 30f + _workingPreset.backLightYawOffset,
                    0f);

            if (_topAccentGO != null)
                _topAccentGO.transform.rotation = Quaternion.Euler(85f, yaw, 0f);
        }

        // =================================================================
        // GUI
        // =================================================================

        private void OnGUI()
        {
            if (!_ready) return;
            InitStyles();

            DrawLeftPanel();
            DrawBottomTimeline();
            DrawSpritePreview();

            if (_showHelp)
                DrawHelpOverlay();
        }

        private void DrawLeftPanel()
        {
            float panelWidth = 280f;
            float panelHeight = Screen.height - 120f;

            GUILayout.BeginArea(new Rect(10, 10, panelWidth, panelHeight));
            GUILayout.BeginVertical(_boxStyle);

            // Заголовок
            GUILayout.Label("Sprite Generator Preview", _headerStyle);
            GUILayout.Space(3);

            // Статус
            string statusIcon = isPaused ? "||" : ">";
            GUILayout.Label($"{statusIcon}  {_currentClipName}   t={scrubTime:F2}s / {_currentClipLength:F2}s");
            GUILayout.Label($"Yaw: {yaw:F0}   Pitch: {pitch:F0}   Dist: {distance:F1}");

            GUILayout.Space(5);

            // Скорость
            GUILayout.BeginHorizontal();
            GUILayout.Label("Скорость", GUILayout.Width(60));
            float newSpeed = GUILayout.HorizontalSlider(animationSpeed, 0f, 2f);
            if (!Mathf.Approximately(newSpeed, animationSpeed))
            {
                animationSpeed = newSpeed;
                if (modelAnimator != null && !isPaused)
                    modelAnimator.speed = animationSpeed;
            }
            GUILayout.Label($"{animationSpeed:F1}x", GUILayout.Width(30));
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Play / Pause
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isPaused ? "Play" : "Pause", GUILayout.Height(25)))
                TogglePause();

            if (GUILayout.Button("В начало", GUILayout.Height(25)))
            {
                scrubTime = 0f;
                ScrubTo(0f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Ракурс камеры
            GUILayout.Label("Ракурс камеры", _headerStyle);

            string[] presetLabels = { "Сверху", "Сбоку", "4 нап.", "8 нап.", "16 нап.", "Свои" };
            int newPreset = GUILayout.SelectionGrid(
                (int)_currentAnglePreset, presetLabels, 3, GUILayout.Height(44));
            if (newPreset != (int)_currentAnglePreset)
                SetAnglePreset((CameraAnglePreset)newPreset);

            if (_presetAngles != null && _presetAngles.Length > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Q <", GUILayout.Width(50)))
                    CycleAngle(-1);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    $"{_presetAngles[_currentAngleIndex]:F0}\u00b0  ({_currentAngleIndex + 1}/{_presetAngles.Length})");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("> E", GUILayout.Width(50)))
                    CycleAngle(1);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(3);

            if (GUILayout.Button("Захватить кадры для превью", GUILayout.Height(25)))
                CaptureFrames();

            GUILayout.Space(8);

            DrawStyleSection();

            // Список анимаций
            GUILayout.Label("Анимации", _headerStyle);

            _animListScroll = GUILayout.BeginScrollView(_animListScroll, GUILayout.MaxHeight(200));

            if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null)
            {
                var clips = modelAnimator.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    bool isCurrent = i == currentAnimationIndex;
                    string label = $"[{i + 1}] {clips[i].name} ({clips[i].length:F2}s)";

                    if (isCurrent)
                    {
                        if (GUILayout.Button(label, _selectedButtonStyle))
                            SetAnimation(i);
                    }
                    else
                    {
                        if (GUILayout.Button(label))
                            SetAnimation(i);
                    }
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Точки генерации
            GUILayout.Label("Точки захвата кадров", _headerStyle);

            // Количество кадров + применить
            GUILayout.BeginHorizontal();
            GUILayout.Label("Кадров:", GUILayout.Width(50));
            string countStr = GUILayout.TextField(_editingFrameTimes.Count.ToString(), GUILayout.Width(36));
            int.TryParse(countStr, out int parsedCount);
            parsedCount = Mathf.Clamp(parsedCount, 1, 120);
            if (GUILayout.Button("Равном.", GUILayout.Height(20)))
            {
                _editingFrameTimes.Clear();
                for (int i = 0; i < parsedCount; i++)
                {
                    float t = parsedCount > 1
                        ? i * _currentClipLength / (parsedCount - 1)
                        : 0f;
                    _editingFrameTimes.Add(t);
                }
            }
            if (GUILayout.Button("+ Кадр", GUILayout.Height(20)))
            {
                _editingFrameTimes.Add(scrubTime);
                _editingFrameTimes.Sort();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Очистить", GUILayout.Height(22)))
                _editingFrameTimes.Clear();

            GUI.enabled = config != null;
            if (GUILayout.Button("Сохранить в конфиг", GUILayout.Height(22)))
                SaveFrameTimesToConfig();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Список точек
            int removeIdx = -1;
            for (int i = 0; i < _editingFrameTimes.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {i}: {_editingFrameTimes[i]:F3}s", GUILayout.Width(120));

                if (GUILayout.Button("Go", GUILayout.Width(30)))
                {
                    scrubTime = _editingFrameTimes[i];
                    ScrubTo(scrubTime);
                    if (!isPaused) TogglePause();
                }

                if (GUILayout.Button("X", GUILayout.Width(22)))
                    removeIdx = i;

                GUILayout.EndHorizontal();
            }

            if (removeIdx >= 0)
                _editingFrameTimes.RemoveAt(removeIdx);

            GUILayout.Space(5);
            if (GUILayout.Button("H — помощь"))
                _showHelp = !_showHelp;

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawBottomTimeline()
        {
            float margin = 300f;
            float timelineHeight = 50f;
            float timelineWidth = Screen.width - margin - 20f;
            float x0 = margin;
            float y0 = Screen.height - timelineHeight - 10f;

            if (timelineWidth < 100f) return;

            GUI.Box(new Rect(x0, y0, timelineWidth, timelineHeight), "");

            // Полоса таймлайна
            float barX = x0 + 5f;
            float barW = timelineWidth - 10f;
            float barY = y0 + 25f;
            float barH = 8f;

            // Фон полосы
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.grayTexture);

            // Текущая позиция (красная)
            if (_currentClipLength > 0f)
            {
                float normTime = scrubTime / _currentClipLength;
                float cursorX = barX + normTime * barW;
                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(cursorX - 1, barY - 3, 3, barH + 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Маркеры точек (зелёные)
            for (int i = 0; i < _editingFrameTimes.Count; i++)
            {
                float norm = _currentClipLength > 0f ? _editingFrameTimes[i] / _currentClipLength : 0f;
                float mx = barX + norm * barW;

                GUI.color = Color.green;
                GUI.DrawTexture(new Rect(mx - 1, barY - 5, 3, barH + 10), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Время слева/справа
            GUI.Label(new Rect(barX, y0 + 3, 80, 20), $"0.00s");
            GUI.Label(new Rect(barX + barW - 50, y0 + 3, 60, 20), $"{_currentClipLength:F2}s");

            // Текущее время по центру
            float centerLabelX = barX + barW * 0.5f - 30;
            GUI.Label(new Rect(centerLabelX, y0 + 3, 80, 20), $"t={scrubTime:F3}s");

            // Клик/зажатие по таймлайну — скраб
            var timelineRect = new Rect(barX, barY - 10, barW, barH + 20);
            var evt = Event.current;
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag)
                && timelineRect.Contains(evt.mousePosition))
            {
                float clickNorm = (evt.mousePosition.x - barX) / barW;
                clickNorm = Mathf.Clamp01(clickNorm);
                scrubTime = clickNorm * _currentClipLength;
                ScrubTo(scrubTime);
                if (!isPaused) TogglePause();
                evt.Use();
            }
        }

        private void DrawSpritePreview()
        {
            if (_capturedFrames.Count == 0) return;

            float texSize = _spritePreviewTexSize;
            float panelW = texSize + 24f;
            float panelH = texSize + 86f;

            // Ограничить размер экраном
            panelW = Mathf.Min(panelW, Screen.width - 310f);
            panelH = Mathf.Min(panelH, Screen.height - 80f);

            float x = Screen.width - panelW - 10f;
            float y = _showHelp ? 200f : 10f;

            var panelRect = new Rect(x, y, panelW, panelH);
            GUI.Box(panelRect, "");
            GUILayout.BeginArea(new Rect(x + 6, y + 4, panelW - 12, panelH - 8));

            // Текущий кадр
            if (_spritePreviewFrame >= 0 && _spritePreviewFrame < _capturedFrames.Count)
            {
                var tex = _capturedFrames[_spritePreviewFrame];
                if (tex != null)
                {
                    float maxW = panelW - 20f;
                    float maxH = panelH - 86f;
                    float aspect = (float)tex.width / tex.height;
                    float dw, dh;
                    if (aspect >= 1f)
                    {
                        dw = Mathf.Min(maxW, maxH * aspect);
                        dh = dw / aspect;
                    }
                    else
                    {
                        dh = Mathf.Min(maxH, maxW / aspect);
                        dw = dh * aspect;
                    }

                    var texRect = GUILayoutUtility.GetRect(dw, dh);
                    GUI.DrawTexture(texRect, tex, ScaleMode.ScaleToFit);
                }
            }

            // Контролы в одну строку
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_spritePreviewPlaying ? "||" : ">", GUILayout.Width(28), GUILayout.Height(22)))
                _spritePreviewPlaying = !_spritePreviewPlaying;
            if (GUILayout.Button("<", GUILayout.Width(24), GUILayout.Height(22)))
            {
                _spritePreviewPlaying = false;
                _spritePreviewFrame = (_spritePreviewFrame - 1 + _capturedFrames.Count)
                                      % _capturedFrames.Count;
            }
            if (GUILayout.Button(">", GUILayout.Width(24), GUILayout.Height(22)))
            {
                _spritePreviewPlaying = false;
                _spritePreviewFrame = (_spritePreviewFrame + 1) % _capturedFrames.Count;
            }
            GUILayout.Label($"{_spritePreviewFrame + 1}/{_capturedFrames.Count}", GUILayout.Width(42));
            _spritePreviewFPS = GUILayout.HorizontalSlider(_spritePreviewFPS, 1f, 30f);
            GUILayout.Label($"{_spritePreviewFPS:F0}", GUILayout.Width(20));
            if (GUILayout.Button("Upd", GUILayout.Width(34), GUILayout.Height(22)))
                CaptureFrames();
            GUILayout.EndHorizontal();

            // Масштаб in-scene спрайта
            if (_spritePreviewGO != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("3D:", GUILayout.Width(22));
                float newScale = GUILayout.HorizontalSlider(_inSceneSpriteScale, 0.2f, 5f);
                GUILayout.Label($"x{newScale:F1}", GUILayout.Width(28));
                if (!Mathf.Approximately(newScale, _inSceneSpriteScale))
                {
                    _inSceneSpriteScale = newScale;
                    _spritePreviewGO.transform.localScale = Vector3.one * _baseSceneScale * _inSceneSpriteScale;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();

            // Ручка ресайза — нижний левый угол (тянуть влево/вниз = увеличить)
            float hs = 14f;
            var handleRect = new Rect(x - 2f, y + panelH - hs + 2f, hs, hs);
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Box(handleRect, "\u2922");
            GUI.color = Color.white;

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition))
            {
                _isDraggingPreviewResize = true;
                evt.Use();
            }
            if (_isDraggingPreviewResize)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    _spritePreviewTexSize = Mathf.Clamp(
                        _spritePreviewTexSize - evt.delta.x + evt.delta.y,
                        80f, 800f);
                    evt.Use();
                }
                if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    _isDraggingPreviewResize = false;
                    evt.Use();
                }
            }
        }

        private void DrawStyleSection()
        {
            if (_workingPreset == null) EnsureWorkingPreset();
            if (_workingPreset == null) return;

            string headerLabel = _showStyleSection
                ? "Стиль рендера ▾"
                : $"Стиль рендера ▸  ({_workingPreset.style})";
            if (GUILayout.Button(headerLabel, _headerStyle, GUILayout.Height(22)))
                _showStyleSection = !_showStyleSection;

            if (!_showStyleSection) return;

            GUI.changed = false;

            string[] styleNames = { "Real", "Toon", "Pixel", "Silh", "Custom", "TopDown" };
            int newStyle = GUILayout.SelectionGrid((int)_workingPreset.style, styleNames, 3, GUILayout.Height(40));
            bool styleChanged = newStyle != (int)_workingPreset.style;
            _workingPreset.style = (RenderStyle)newStyle;

            GUILayout.Space(3);

            // Параметры стиля
            if (GUILayout.Button(_showStyleParams ? "Параметры ▾" : "Параметры ▸", GUILayout.Height(20)))
                _showStyleParams = !_showStyleParams;

            if (_showStyleParams)
            {
                switch (_workingPreset.style)
                {
                    case RenderStyle.TopDownLit:
                        _workingPreset.topDownRimIntensity = LabeledSlider("Rim Int.", _workingPreset.topDownRimIntensity, 0f, 3f);
                        _workingPreset.topDownRimPower = LabeledSlider("Rim Pwr", _workingPreset.topDownRimPower, 0.5f, 8f);
                        _workingPreset.topDownWrapAmount = LabeledSlider("Wrap", _workingPreset.topDownWrapAmount, 0f, 1f);
                        _workingPreset.topDownTopAccent = LabeledSlider("Top Acc.", _workingPreset.topDownTopAccent, 0f, 1f);
                        _workingPreset.topDownOutlineEnabled = GUILayout.Toggle(_workingPreset.topDownOutlineEnabled, " Outline");
                        if (_workingPreset.topDownOutlineEnabled)
                            _workingPreset.topDownOutlineWidth = LabeledSlider("Width", _workingPreset.topDownOutlineWidth, 0f, 0.1f);
                        break;
                    case RenderStyle.Toon:
                        _workingPreset.outlineWidth = LabeledSlider("Outline W", _workingPreset.outlineWidth, 0f, 0.1f);
                        break;
                    case RenderStyle.PixelArt:
                        int newDownscale = Mathf.RoundToInt(LabeledSlider("Downscale", _workingPreset.pixelArtDownscale, 1f, 8f));
                        _workingPreset.pixelArtDownscale = newDownscale;
                        break;
                    case RenderStyle.Silhouette:
                        GUILayout.Label("Цвет — в инспекторе пресета");
                        break;
                    case RenderStyle.Realistic:
                    case RenderStyle.CustomMaterial:
                        GUILayout.Label("Нет числовых параметров");
                        break;
                }
            }

            GUILayout.Space(3);

            // Освещение
            if (GUILayout.Button(_showStyleLights ? "Освещение ▾" : "Освещение ▸", GUILayout.Height(20)))
                _showStyleLights = !_showStyleLights;

            if (_showStyleLights)
            {
                _workingPreset.enableBackLight = GUILayout.Toggle(_workingPreset.enableBackLight, " Back Light");
                if (_workingPreset.enableBackLight)
                {
                    _workingPreset.backLightIntensity = LabeledSlider("Int.", _workingPreset.backLightIntensity, 0f, 2f);
                    _workingPreset.backLightYawOffset = LabeledSlider("Yaw", _workingPreset.backLightYawOffset, 0f, 360f);
                    _workingPreset.backLightPitch = LabeledSlider("Pitch", _workingPreset.backLightPitch, 0f, 80f);
                }

                _workingPreset.enableTopAccentLight = GUILayout.Toggle(_workingPreset.enableTopAccentLight, " Top Accent");
                if (_workingPreset.enableTopAccentLight)
                    _workingPreset.topAccentIntensity = LabeledSlider("Int.", _workingPreset.topAccentIntensity, 0f, 1.5f);
            }

            GUILayout.Space(3);

            // Sobel
            if (GUILayout.Button(_showStyleSobel ? "Sobel Outline ▾" : "Sobel Outline ▸", GUILayout.Height(20)))
                _showStyleSobel = !_showStyleSobel;

            if (_showStyleSobel)
            {
                _workingPreset.enableSobelOutlinePost = GUILayout.Toggle(_workingPreset.enableSobelOutlinePost, " Enable Sobel");
                if (_workingPreset.enableSobelOutlinePost)
                {
                    _workingPreset.sobelThreshold = LabeledSlider("Thresh", _workingPreset.sobelThreshold, 0f, 1f);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Source", GUILayout.Width(60));
                    if (GUILayout.Toggle(_workingPreset.sobelSource == SobelSource.Alpha, "Alpha", "Button"))
                        _workingPreset.sobelSource = SobelSource.Alpha;
                    if (GUILayout.Toggle(_workingPreset.sobelSource == SobelSource.Luminance, "Luminance", "Button"))
                        _workingPreset.sobelSource = SobelSource.Luminance;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(2);
            GUILayout.Label($"<color=#888>Sobel виден только в превью кадров. Кнопка \"Upd\" перезахватит.</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, wordWrap = true });

            if (GUI.changed || styleChanged)
                ReapplyStyleFromPreset();
        }

        private float LabeledSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(v.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return v;
        }

        private void DrawHelpOverlay()
        {
            float w = 260;
            float h = 180;
            float x = Screen.width - w - 10;
            float y = 10;

            GUI.Box(new Rect(x, y, w, h), "");
            GUILayout.BeginArea(new Rect(x + 8, y + 5, w - 16, h - 10));

            GUILayout.Label("Управление", _headerStyle);
            GUILayout.Label("ПКМ — вращение камеры");
            GUILayout.Label("СКМ — перемещение камеры");
            GUILayout.Label("Колёсико — зум");
            GUILayout.Label("Space — пауза / воспроизведение");
            GUILayout.Label("A / D — скраб по кадрам (на паузе)");
            GUILayout.Label("Q / E — ракурс влево/вправо");
            GUILayout.Label("1-9 — переключить анимацию");
            GUILayout.Label("H — скрыть подсказки");

            GUILayout.EndArea();
        }
    }
}
