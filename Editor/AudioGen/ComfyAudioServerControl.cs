using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Статус и запуск сервера ComfyUI для аудио-студии. Дубль ComfyServerControl из IconGen
    /// (другая сборка, тащить зависимость ради 80 строк не стали) — настройки и сервер общие.
    /// </summary>
    public static class ComfyAudioServerControl
    {
        public static bool IsOnline { get; private set; }
        public static bool Checking { get; private set; }
        public static bool Launching { get; private set; }

        public static event Action Changed;

        private static double _lastCheck = -999;

        /// <summary>Проверить статус не чаще, чем раз в intervalSec (дёргать из UI можно свободно).</summary>
        public static void Poll(double intervalSec = 5.0)
        {
            if (EditorApplication.timeSinceStartup - _lastCheck < intervalSec) return;
            CheckNow();
        }

        public static async void CheckNow()
        {
            if (Checking) return;
            Checking = true;
            _lastCheck = EditorApplication.timeSinceStartup;
            try
            {
                bool online = await ComfyAudioClient.IsOnlineAsync();
                if (online != IsOnline || Launching && online)
                {
                    IsOnline = online;
                    if (online) Launching = false;
                    Changed?.Invoke();
                }
                else
                {
                    IsOnline = online;
                }
            }
            finally { Checking = false; }
        }

        public static bool CanLaunch
            => !string.IsNullOrEmpty(AudioAiSettings.ComfyLaunch) && File.Exists(AudioAiSettings.ComfyLaunch);

        /// <summary>Запустить сервер файлом из настроек (.bat/.exe). UseShellExecute обязателен для .bat.</summary>
        public static bool Launch()
        {
            if (!CanLaunch || Launching || IsOnline) return false;
            try
            {
                string path = AudioAiSettings.ComfyLaunch;
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? "",
                    UseShellExecute = true,
                });
                Launching = true;
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[AudioStudio] Не удалось запустить ComfyUI: {e.Message}");
                return false;
            }
        }

        public static void OpenInBrowser()
            => UnityEngine.Application.OpenURL(AudioAiSettings.ComfyServer);
    }
}
