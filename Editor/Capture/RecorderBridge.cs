// Packages/com.protosystem.core/Editor/Capture/RecorderBridge.cs
using System;
using System.IO;
using UnityEngine;
#if PROTO_HAS_RECORDER
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Обёртка Unity Recorder API для ручной видеозаписи.
    /// Изолирует зависимость от com.unity.recorder.
    /// </summary>
    public class RecorderBridge : CaptureSystem.IRecorderBridge
    {
#if PROTO_HAS_RECORDER
        private RecorderController _controller;
        private RecorderControllerSettings _settings;
        private MovieRecorderSettings _movieSettings;
#endif

        public bool IsRecording
        {
            get
            {
#if PROTO_HAS_RECORDER
                return _controller?.IsRecording() ?? false;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Начать запись видео.
        /// </summary>
        /// <param name="outputDir">Директория для сохранения</param>
        /// <param name="filename">Имя файла без расширения</param>
        /// <param name="fps">Частота кадров</param>
        /// <param name="resScale">Масштаб разрешения (0.25 - 1.0)</param>
        public void StartRecording(string outputDir, string filename, int fps, float resScale)
        {
#if PROTO_HAS_RECORDER
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
#else
            Debug.LogWarning("[Capture] Unity Recorder не установлен. Добавьте com.unity.recorder в manifest.json");
#endif
        }

        /// <summary>
        /// Остановить запись.
        /// </summary>
        public void StopRecording()
        {
#if PROTO_HAS_RECORDER
            if (_controller == null || !_controller.IsRecording())
            {
                Debug.LogWarning("[Capture] Нет активной записи");
                return;
            }

            _controller.StopRecording();
            Debug.Log("[Capture] Запись остановлена");

            Cleanup();
#else
            Debug.LogWarning("[Capture] Unity Recorder не установлен");
#endif
        }

        private void Cleanup()
        {
#if PROTO_HAS_RECORDER
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
#endif
        }

        public void Dispose()
        {
#if PROTO_HAS_RECORDER
            if (_controller != null && _controller.IsRecording())
                _controller.StopRecording();
            Cleanup();
#endif
        }
    }
}
