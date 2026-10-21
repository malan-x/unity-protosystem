using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Статус локального ComfyUI и его запуск из студии: опрос сервера (ComfyUIClient.IsOnlineAsync)
    /// с кэшем + старт процесса из AiToolsSettings.ComfyLaunch. Студия рисует индикатор
    /// (зелёный/красный) и кнопку запуска, подписавшись на Changed.
    /// </summary>
    public static class ComfyServerControl
    {
        /// <summary>Последний известный статус сервера.</summary>
        public static bool IsOnline { get; private set; }

        /// <summary>Идёт проверка (индикатор можно показать «жёлтым»).</summary>
        public static bool Checking { get; private set; }

        /// <summary>Статус изменился/проверка началась-закончилась — перерисовать UI.</summary>
        public static event Action Changed;

        private static double _nextCheck;
        private static bool _launching;

        /// <summary>Проверить не чаще, чем раз в intervalSec (дёргается по таймеру студии).</summary>
        public static void Poll(double intervalSec = 5.0)
        {
            if (Checking || EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + intervalSec;
            CheckNow();
        }

        /// <summary>Проверить статус сейчас (не блокирует редактор).</summary>
        public static async void CheckNow()
        {
            if (Checking) return;
            Checking = true;
            Changed?.Invoke();

            bool online;
            try { online = await ComfyUIClient.IsOnlineAsync(); }
            catch { online = false; }

            Checking = false;
            IsOnline = online;
            if (online) _launching = false;   // дождались подъёма
            Changed?.Invoke();
        }

        /// <summary>Путь запуска задан и файл на месте.</summary>
        public static bool CanLaunch
        {
            get
            {
                string p = AiToolsSettings.ComfyLaunch;
                return !string.IsNullOrWhiteSpace(p) && File.Exists(p);
            }
        }

        /// <summary>Идёт старт: процесс запущен, но сервер ещё не ответил.</summary>
        public static bool Launching => _launching && !IsOnline;

        /// <summary>Запустить ComfyUI (.bat/.exe из настроек). false — путь не задан/ошибка.</summary>
        public static bool Launch()
        {
            if (IsOnline) return true;
            if (!CanLaunch)
            {
                Debug.LogWarning("[ComfyUI] Не задан файл запуска: Project Settings ▸ AI Icon Tools ▸ «Запуск (.bat/.exe)».");
                return false;
            }
            try
            {
                string path = AiToolsSettings.ComfyLaunch;
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? "",
                    UseShellExecute = true,   // .bat поднимается только через оболочку
                };
                Process.Start(psi);
                _launching = true;
                _nextCheck = 0;               // опрашивать сразу, пока поднимается
                Changed?.Invoke();
                Debug.Log($"[ComfyUI] Запуск: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ComfyUI] Не удалось запустить: {e.Message}");
                _launching = false;
                return false;
            }
        }

        /// <summary>Открыть веб-интерфейс сервера в браузере.</summary>
        public static void OpenInBrowser()
            => UnityEngine.Application.OpenURL(AiToolsSettings.ComfyServer);
    }
}
