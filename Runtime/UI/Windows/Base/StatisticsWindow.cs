// Packages/com.protosystem.core/Runtime/UI/Windows/Base/StatisticsWindow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Окно статистики (после игры или в паузе)
    /// </summary>
    [UIWindow("Statistics", WindowType.Normal, WindowLayer.Windows, Level = 1, PauseGame = true, CursorMode = WindowCursorMode.Visible)]
    public class StatisticsWindow : UIWindowBase
    {
        [Header("Content")]
        [SerializeField] protected TMP_Text titleText;
        [SerializeField] protected Transform statsContainer;
        [SerializeField] protected GameObject statRowPrefab;

        [Header("Buttons")]
        [SerializeField] protected Button continueButton;
        [SerializeField] protected Button backButton;

        private readonly List<GameObject> _statRows = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            
            continueButton?.onClick.AddListener(OnContinueClicked);
            backButton?.onClick.AddListener(OnBackClicked);
        }

        public override void Show(System.Action onComplete = null)
        {
            base.Show(onComplete);
        }

        /// <summary>
        /// Установить заголовок
        /// </summary>
        public void SetTitle(string title)
        {
            if (titleText != null)
                titleText.text = title;
        }

        /// <summary>
        /// Очистить все строки статистики
        /// </summary>
        public void ClearStats()
        {
            foreach (var row in _statRows)
            {
                if (row != null)
                    Destroy(row);
            }
            _statRows.Clear();
        }

        /// <summary>
        /// Добавить строку статистики
        /// </summary>
        public void AddStat(string label, string value)
        {
            if (statsContainer == null) return;

            GameObject row;
            if (statRowPrefab != null)
            {
                row = Instantiate(statRowPrefab, statsContainer);
            }
            else
            {
                // Создаём простую строку если нет префаба
                row = CreateDefaultStatRow(label, value);
            }

            // Пытаемся найти тексты в строке
            var texts = row.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = label;
                texts[1].text = value;
            }
            else if (texts.Length == 1)
            {
                texts[0].text = $"{label}: {value}";
            }

            _statRows.Add(row);
        }

        /// <summary>
        /// Добавить несколько строк статистики
        /// </summary>
        public void AddStats(Dictionary<string, string> stats)
        {
            foreach (var kvp in stats)
            {
                AddStat(kvp.Key, kvp.Value);
            }
        }

        private GameObject CreateDefaultStatRow(string label, string value)
        {
            var row = new GameObject("StatRow");
            row.transform.SetParent(statsContainer, false);
            
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);
            
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = 10;
            
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 30;
            le.flexibleWidth = 1;

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 16;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            // Value
            var valueGO = new GameObject("Value");
            valueGO.transform.SetParent(row.transform, false);
            var valueText = valueGO.AddComponent<TextMeshProUGUI>();
            valueText.text = value;
            valueText.fontSize = 16;
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.fontStyle = FontStyles.Bold;
            var valueLE = valueGO.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 100;

            return row;
        }

        protected virtual void OnContinueClicked()
        {
            UISystem.Back();
        }

        protected virtual void OnBackClicked()
        {
            UISystem.Back();
        }
    }
}
