// Packages/com.protosystem.core/Runtime/UI/UITwoColorImage.cs
using UnityEngine;
using UnityEngine.UI;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Компонент для Image с двумя цветами (заливка и рамка)
    /// Использует специальный шейдер UI/TwoColor
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class UITwoColorImage : MonoBehaviour
    {
        [Header("Цвета")]
        [SerializeField] private Color _fillColor = Color.white;
        [SerializeField] private Color _borderColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        private Image _image;
        private Material _material;
        private static Shader _twoColorShader;
        
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                UpdateColors();
            }
        }
        
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                UpdateColors();
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            UpdateColors();
        }

        private void Initialize()
        {
            _image = GetComponent<Image>();
            
            // Ищем шейдер
            if (_twoColorShader == null)
            {
                _twoColorShader = Shader.Find("UI/TwoColor");
            }
            
            if (_twoColorShader != null && _image != null)
            {
                // Создаём инстанс материала
                if (_material == null || _material.shader != _twoColorShader)
                {
                    _material = new Material(_twoColorShader);
                    _material.name = "UITwoColor (Instance)";
                    _image.material = _material;
                }
            }
            else
            {
                Debug.LogWarning($"[UITwoColorImage] Shader 'UI/TwoColor' not found on {gameObject.name}");
            }
        }

        private void UpdateColors()
        {
            if (_material != null)
            {
                _material.SetColor("_Color", _fillColor);
                _material.SetColor("_BorderColor", _borderColor);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Обновляем цвета в редакторе
            if (_material != null)
            {
                UpdateColors();
            }
        }
#endif

        private void OnDestroy()
        {
            // Уничтожаем инстанс материала
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
            }
        }
    }
}
