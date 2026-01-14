// Packages/com.protosystem.core/Editor/Publishing/Core/SteamGuardCodePromptWindow.cs
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Publishing.Editor
{
    internal sealed class SteamGuardCodePromptWindow : EditorWindow
    {
        private const float DefaultWidth = 520f;
        private const float DefaultHeight = 160f;

        private string _message;
        private string _code;
        private TaskCompletionSource<string> _tcs;
        private bool _focusRequested;

        public static Task<string> PromptAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<string>();

            EditorApplication.delayCall += () =>
            {
                var window = CreateInstance<SteamGuardCodePromptWindow>();
                window.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? "Steam Guard" : title);
                window._message = message;
                window._code = string.Empty;
                window._tcs = tcs;
                window._focusRequested = true;

                var center = new Vector2(Screen.currentResolution.width * 0.5f, Screen.currentResolution.height * 0.5f);
                window.position = new Rect(center.x - DefaultWidth * 0.5f, center.y - DefaultHeight * 0.5f, DefaultWidth, DefaultHeight);

                window.ShowModalUtility();
            };

            return tcs.Task;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope())
            {
                if (!string.IsNullOrEmpty(_message))
                {
                    EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space(8);
                }

                GUI.SetNextControlName("steam_guard_code_field");
                _code = EditorGUILayout.TextField(new GUIContent("Code"), _code);

                if (_focusRequested)
                {
                    EditorGUI.FocusTextInControl("steam_guard_code_field");
                    _focusRequested = false;
                    Repaint();
                }

                EditorGUILayout.Space(10);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    var submit = GUILayout.Button("Submit", GUILayout.Width(90));
                    var cancel = GUILayout.Button("Cancel", GUILayout.Width(90));

                    if (submit)
                    {
                        Submit();
                        return;
                    }

                    if (cancel)
                    {
                        Cancel();
                        return;
                    }
                }
            }

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    Cancel();
                    Event.current.Use();
                }
            }

        }

        private void Submit()
        {
            var code = string.IsNullOrWhiteSpace(_code) ? string.Empty : _code.Trim();
            _tcs?.TrySetResult(code);
            Close();
        }

        private void Cancel()
        {
            _tcs?.TrySetResult(null);
            Close();
        }

        private void OnDestroy()
        {
            _tcs?.TrySetResult(null);
        }
    }
}
