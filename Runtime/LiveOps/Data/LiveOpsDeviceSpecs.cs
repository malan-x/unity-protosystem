// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsDeviceSpecs.cs
using System;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Конфигурация машины игрока. Собирается один раз и уходит с первым
    /// батчем сессии — данные статичные, слать их чаще незачем.
    ///
    /// Нужна, чтобы понимать аудиторию (какое железо реально в ходу) и
    /// связывать оценки и жалобы на производительность с конкретными
    /// конфигурациями: сервер кладёт это в player_specs, одна запись
    /// на игрока, джойн с оценками по player_id.
    ///
    /// Имена полей — snake_case: так их ждёт серверный хук, и так же они
    /// уходят через JsonUtility в кастомном провайдере.
    /// </summary>
    [Serializable]
    public class LiveOpsDeviceSpecs
    {
        public string os;
        public string cpu;
        public int    cpu_cores;
        public int    ram_mb;
        public string gpu;
        public string gpu_vendor;
        public string gpu_api;
        public int    vram_mb;
        public string resolution;
        public int    refresh_hz;

        /// <summary>Снять конфигурацию текущей машины.</summary>
        public static LiveOpsDeviceSpecs Collect()
        {
            var res = Screen.currentResolution;
            return new LiveOpsDeviceSpecs
            {
                os         = SystemInfo.operatingSystem,
                cpu        = SystemInfo.processorType,
                cpu_cores  = SystemInfo.processorCount,
                ram_mb     = SystemInfo.systemMemorySize,
                gpu        = SystemInfo.graphicsDeviceName,
                gpu_vendor = SystemInfo.graphicsDeviceVendor,
                gpu_api    = SystemInfo.graphicsDeviceType.ToString(),
                vram_mb    = SystemInfo.graphicsMemorySize,
                resolution = $"{res.width}x{res.height}",
                // В новых версиях Unity refreshRate устарел в пользу
                // refreshRateRatio, но старое поле ещё работает и не требует
                // ветвления по версиям — точность в герцах здесь не нужна.
                refresh_hz = Mathf.RoundToInt((float)res.refreshRateRatio.value),
            };
        }

        /// <summary>
        /// Тег устройства по платформе: windows / linux / mac / ...
        ///
        /// ВАЖНО: Steam Deck сюда не попадает. Игра на нём почти всегда идёт
        /// через Proton и видит себя как WindowsPlayer, а нативный Linux-билд
        /// неотличим от обычного линукса. Deck определяется только через
        /// Steamworks (SteamUtils.IsSteamRunningOnSteamDeck) — проект должен
        /// сообщить это сам: LiveOpsSystem.SetDeviceTag("steamdeck").
        /// </summary>
        public static string PlatformTag()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "windows";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "mac";
                case RuntimePlatform.Android:
                    return "android";
                case RuntimePlatform.IPhonePlayer:
                    return "ios";
                default:
                    return Application.platform.ToString().ToLowerInvariant();
            }
        }
    }
}
