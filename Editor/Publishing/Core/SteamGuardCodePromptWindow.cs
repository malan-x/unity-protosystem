// Packages/com.protosystem.core/Editor/Publishing/Core/SteamGuardCodePromptWindow.cs
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Окно для ввода кода Steam Guard.
    /// Использует polling вместо модального окна для совместимости с async/await.
    /// </summary>
    internal sealed class SteamGuardCodePromptWindow : EditorWindow
    {
        private const float DefaultWidth = 400f;
        private const float DefaultHeight = 140f;

        private string _message;
        private string _code = "";
        private TaskCompletionSource<string> _tcs;
        private bool _submitted;
        private bool _cancelled;
        private bool _focusField = true;

        // Статическое поле для отслеживания активного окна
        private static SteamGuardCodePromptWindow _activeWindow;

        public static Task<string> PromptAsync(string title, string message)
        {
            // Закрываем предыдущее окно если есть
            if (_activeWindow != null)
            {
                try { _activeWindow.Close(); } catch { }
                _activeWindow = null;
            }

            var tcs = new TaskCompletionSource<string>();

            // Создаём окно в главном потоке
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var window = CreateInstance<SteamGuardCodePromptWindow>();
                    window.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? "Steam Guard" : title);
                    window._message = message;
                    window._code = "";
                    window._tcs = tcs;
                    window._submitted = false;
                    window._cancelled = false;
                    window._focusField = true;

                    // Центрируем окно
                    var mainWindow = EditorGUIUtility.GetMainWindowPosition();
                    var x = mainWindow.x + (mainWindow.width - DefaultWidth) / 2;
                    var y = mainWindow.y + (mainWindow.height - DefaultHeight) / 2;
                    window.position = new Rect(x, y, DefaultWidth, DefaultHeight);

                    window.minSize = new Vector2(DefaultWidth, DefaultHeight);
                    window.maxSize = new Vector2(DefaultWidth + 100, DefaultHeight + 50);

                    _activeWindow = window;
                    
                    // ShowUtility - окно поверх других, но не блокирует
                    window.ShowUtility();
                    window.Focus();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamGuard] Failed to show window: {ex.Message}");
                    tcs.TrySetResult(null);
                }
            };

            return tcs.Task;
        }

        private void OnGUI()
        {
            // Фон
            EditorGUILayout.Space(10);

            // Сообщение
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);
            }

            // Поле ввода кода
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Code:", GUILayout.Width(45));
            
            GUI.SetNextControlName("SteamGuardCodeField");
            
            // Стиль для большого поля
            var textStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 30
            };
            
            _code = EditorGUILayout.TextField(_code, textStyle, GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();

            // Фокус на поле
            if (_focusField)
            {
                EditorGUI.FocusTextInControl("SteamGuardCodeField");
                _focusField = false;
            }

            EditorGUILayout.Space(15);

            // Кнопки
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrWhiteSpace(_code);
            if (GUILayout.Button("Submit", GUILayout.Width(100), GUILayout.Height(28)))
            {
                Submit();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(28)))
            {
                Cancel();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Обработка клавиш
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (!string.IsNullOrWhiteSpace(_code))
                    {
                        Submit();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Cancel();
                    e.Use();
                }
            }

            // Постоянная перерисовка для responsiveness
            if (focusedWindow == this)
            {
                Repaint();
            }
        }

        private void Submit()
        {
            if (_submitted || _cancelled) return;
            _submitted = true;

            var code = _code?.Trim() ?? "";
            Debug.Log($"[SteamGuard] Code submitted: {new string('*', code.Length)}");
            
            _tcs?.TrySetResult(code);
            _activeWindow = null;
            Close();
        }

        private void Cancel()
        {
            if (_submitted || _cancelled) return;
            _cancelled = true;

            Debug.Log("[SteamGuard] Cancelled by user");
            
            _tcs?.TrySetResult(null);
            _activeWindow = null;
            Close();
        }

        private void OnLostFocus()
        {
            // Возвращаем фокус если окно потеряло его
            if (!_submitted && !_cancelled)
            {
                Focus();
            }
        }

        private void OnDestroy()
        {
            if (!_submitted && !_cancelled)
            {
                _tcs?.TrySetResult(null);
            }
            
            if (_activeWindow == this)
            {
                _activeWindow = null;
            }
        }
    }
}
