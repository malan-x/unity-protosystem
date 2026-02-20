// Packages/com.protosystem.core/Editor/Capture/Recorder/RecorderBridge.cs
// Этот файл компилируется ТОЛЬКО при наличии com.unity.recorder
// (defineConstraints: PROTO_HAS_RECORDER в ProtoSystem.Editor.Recorder.asmdef)
using System;
using System.IO;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Обёртка Unity Recorder API для ручной видеозаписи.
    /// </summary>
    public class RecorderBridge : CaptureSystem.IRecorderBridge
    {
        private RecorderController _controller;
        private RecorderControllerSettings _settings;
        private MovieRecorderSettings _movieSettings;

        public bool IsRecording => _controller?.IsRecording() ?? false;

        public void StartRecording(string outputDir, string filename, int fps, float resScale)
        {
            if (_controller != null && _controller.IsRecording())
            {
                Debug.LogWarning("[Capture] Запись уже идёт");
                return;
            }

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _settings.SetRecordModeToManual();
            _settings.FrameRate = fps;

            _movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _movieSettings.Enabled = true;
            _movieSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;

            var inputSettings = new GameViewInputSettings();
            if (resScale < 1f)
            {
                inputSettings.OutputWidth = (int)(Screen.width * resScale);
                inputSettings.OutputHeight = (int)(Screen.height * resScale);
            }
            _movieSettings.ImageInputSettings = inputSettings;

            _movieSettings.OutputFile = Path.Combine(outputDir, filename);

            _settings.AddRecorderSettings(_movieSettings);

            _controller = new RecorderController(_settings);
            _controller.PrepareRecording();
            _controller.StartRecording();

            Debug.Log($"[Capture] Запись начата: {filename}.mp4");
        }

        public void StopRecording()
        {
            if (_controller == null || !_controller.IsRecording())
            {
                Debug.LogWarning("[Capture] Нет активной записи");
                return;
            }

            _controller.StopRecording();
            Debug.Log("[Capture] Запись остановлена");

            Cleanup();
        }

        private void Cleanup()
        {
            if (_movieSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(_movieSettings);
                _movieSettings = null;
            }
            if (_settings != null)
            {
                UnityEngine.Object.DestroyImmediate(_settings);
                _settings = null;
            }
            _controller = null;
        }

        public void Dispose()
        {
            if (_controller != null && _controller.IsRecording())
                _controller.StopRecording();
            Cleanup();
        }
    }
}
