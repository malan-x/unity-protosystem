using System.Threading;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Страховка от «окно закрылось, а процесс жив».
    ///
    /// Симптом (жалоба игрока 22.08): по Alt+F4 игра исчезает с экрана, но процесс
    /// остаётся в памяти, и Steam продолжает показывать «Запущено» — игроку приходится
    /// снимать задачу руками. Хуже косметики: пока процесс числится живым, Steam не
    /// начинает синхронизацию облачных сохранений и накручивает время в профиле.
    ///
    /// Держать процесс может что угодно нативное, до чего C# не дотягивается:
    /// нативные потоки опроса устройств (Rewired при Raw Input + XInput), клиент Steam,
    /// незакрытый сокет внутри UnityWebRequest. Ловить каждую причину по отдельности
    /// бессмысленно — они разные у разных игроков и зависят от периферии и сети.
    /// Поэтому: даём движку штатно завершиться, а если через graceSeconds процесс всё
    /// ещё жив — снимаем его сами.
    ///
    /// Поток фоновый (IsBackground), то есть сам процесс никогда не удерживает.
    /// Запас по времени берётся с расчётом, что сохранения и Steam-shutdown уже
    /// отработали в OnApplicationQuit — они синхронные и укладываются в доли секунды.
    /// </summary>
    public static class QuitGuard
    {
        /// <summary>Сколько ждать штатного завершения, прежде чем убивать процесс.</summary>
        public static float GraceSeconds { get; set; } = 5f;

        /// <summary>Выключатель на случай, если игре нужна долгая работа после quit.</summary>
        public static bool Enabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // В редакторе Play Mode останавливается без завершения процесса —
            // сторож там не нужен и был бы опасен
            if (Application.isEditor) return;

            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            if (!Enabled) return;

            var watchdog = new Thread(WatchdogLoop)
            {
                IsBackground = true,       // сам процесс не держит
                Name = "ProtoSystem.QuitGuard",
            };
            watchdog.Start();
        }

        private static void WatchdogLoop()
        {
            Thread.Sleep(Mathf.Max(1, Mathf.CeilToInt(GraceSeconds * 1000f)));

            // Досюда доходим, только если процесс НЕ завершился штатно:
            // при нормальном выходе фоновый поток умирает вместе с процессом
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch
            {
                // Последний рубеж: если Kill недоступен (права, платформа)
                System.Environment.Exit(0);
            }
        }
    }
}
