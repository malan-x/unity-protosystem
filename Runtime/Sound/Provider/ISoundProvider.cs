using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.Sound
{
    /// <summary>
    /// Интерфейс аудио-провайдера (Unity, FMOD, Wwise)
    /// </summary>
    public interface ISoundProvider : IDisposable
    {
        /// <summary>
        /// Инициализация провайдера
        /// </summary>
        void Initialize(SoundManagerConfig config, SoundLibrary library);
        
        /// <summary>
        /// Обновление (вызывается каждый кадр)
        /// </summary>
        void Update();
        
        // === Воспроизведение ===
        
        /// <summary>
        /// Воспроизвести звук
        /// </summary>
        /// <param name="id">ID звука из библиотеки</param>
        /// <param name="position">Позиция в мире (null = 2D звук)</param>
        /// <param name="volumeMultiplier">Множитель громкости</param>
        /// <returns>Хэндл для управления звуком</returns>
        SoundHandle Play(string id, Vector3? position = null, float volumeMultiplier = 1f);
        
        /// <summary>
        /// Остановить звук по хэндлу
        /// </summary>
        void Stop(SoundHandle handle);
        
        /// <summary>
        /// Остановить все звуки категории
        /// </summary>
        void StopAll(SoundCategory category);
        
        /// <summary>
        /// Остановить все звуки
        /// </summary>
        void StopAll();
        
        /// <summary>
        /// Проверить играет ли звук
        /// </summary>
        bool IsPlaying(SoundHandle handle);
        
        // === Музыка ===
        
        /// <summary>
        /// Воспроизвести музыку (с кроссфейдом)
        /// </summary>
        /// <param name="id">ID музыкального трека</param>
        /// <param name="fadeInTime">Время появления (0 = мгновенно)</param>
        void PlayMusic(string id, float fadeInTime = 0f);
        
        /// <summary>
        /// Остановить музыку
        /// </summary>
        /// <param name="fadeOutTime">Время затухания (0 = мгновенно)</param>
        void StopMusic(float fadeOutTime = 0f);
        
        /// <summary>
        /// Кроссфейд к новому треку
        /// </summary>
        void CrossfadeMusic(string id, float crossfadeTime);
        
        /// <summary>
        /// Установить параметр музыки (для вертикальных слоёв)
        /// </summary>
        void SetMusicParameter(string parameter, float value);
        
        /// <summary>
        /// Получить значение параметра музыки
        /// </summary>
        float GetMusicParameter(string parameter);
        
        // === Громкость ===
        
        /// <summary>
        /// Установить громкость категории
        /// </summary>
        void SetVolume(SoundCategory category, float volume);
        
        /// <summary>
        /// Получить громкость категории
        /// </summary>
        float GetVolume(SoundCategory category);
        
        /// <summary>
        /// Установить общий mute
        /// </summary>
        void SetMute(bool muted);
        
        /// <summary>
        /// Получить состояние mute
        /// </summary>
        bool IsMuted();
        
        // === Пауза ===
        
        /// <summary>
        /// Поставить на паузу категорию
        /// </summary>
        void Pause(SoundCategory category);
        
        /// <summary>
        /// Снять с паузы категорию
        /// </summary>
        void Resume(SoundCategory category);
        
        /// <summary>
        /// Поставить на паузу всё
        /// </summary>
        void PauseAll();
        
        /// <summary>
        /// Снять с паузы всё
        /// </summary>
        void ResumeAll();
        
        // === Snapshots ===
        
        /// <summary>
        /// Активировать snapshot
        /// </summary>
        /// <param name="snapshot">Snapshot для активации</param>
        /// <param name="transitionTime">Время перехода</param>
        void SetSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f);
        
        /// <summary>
        /// Деактивировать snapshot
        /// </summary>
        void ClearSnapshot(SoundSnapshotId snapshot, float transitionTime = 0.5f);
        
        /// <summary>
        /// Очистить все snapshots
        /// </summary>
        void ClearAllSnapshots(float transitionTime = 0.5f);
        
        // === Банки ===
        
        /// <summary>
        /// Загрузить банк звуков
        /// </summary>
        Task<bool> LoadBankAsync(string bankId);
        
        /// <summary>
        /// Выгрузить банк звуков
        /// </summary>
        void UnloadBank(string bankId);
        
        /// <summary>
        /// Проверить загружен ли банк
        /// </summary>
        bool IsBankLoaded(string bankId);
        
        // === Расширение ===
        
        /// <summary>
        /// Установить процессор для кастомной обработки звуков (occlusion и т.д.)
        /// </summary>
        void SetSoundProcessor(ISoundProcessor processor);
        
        // === Статистика ===
        
        /// <summary>
        /// Количество активных звуков
        /// </summary>
        int ActiveSoundCount { get; }
        
        /// <summary>
        /// Максимум одновременных звуков
        /// </summary>
        int MaxSimultaneousSounds { get; }
    }
}
