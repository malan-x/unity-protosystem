using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen
{
    /// <summary>
    /// Провайдер аудио-контента: ассет, хранящий звуковые сущности (библиотека звуков,
    /// профили миссий…), сам объявляет, каким его сущностям какой звук нужен. UI-клики,
    /// SFX боя, эмбиенты, музыка — каждый вид звука — отдельный сет.
    ///
    /// Runtime-интерфейс без editor-зависимостей: реализуется игровыми SO. Аудио-студия
    /// (editor) подключает провайдера через ассет набора (AudioCollectionAsset) по Id сета.
    /// Зеркало IArtContentProvider из ProtoSystem.IconGen.
    /// </summary>
    public interface IAudioContentProvider
    {
        IEnumerable<AudioContentSet> GetAudioSets();
    }

    /// <summary>Один вид звука у провайдера: «звуки UI», «SFX мобов», «эмбиенты биомов»…</summary>
    public class AudioContentSet
    {
        /// <summary>Стабильный ключ сета ("ui_sounds") — по нему ссылается ассет набора.</summary>
        public string Id;

        /// <summary>Имя по умолчанию (ассет набора может переопределить).</summary>
        public string Title;

        /// <summary>
        /// Куда идёт звук сета («кнопки всех окон», «бой: оружие вагонов») —
        /// студия показывает это в шапке секции, чтобы было видно назначение.
        /// </summary>
        public string UsageNote;

        /// <summary>
        /// Контракт длительности: сколько секунд генерировать по умолчанию для сущностей
        /// сета (UI-клик ~1 с, эмбиент ~40 с). 0 — не задано, решает стиль генерации.
        /// Сущность может переопределить (AudioContentItem.Seconds).
        /// </summary>
        public float DefaultSeconds;

        public List<AudioContentItem> Items = new();
    }

    /// <summary>
    /// Элемент сета: звуковая сущность + доступ к её клипу/промпту. Делегаты создаются
    /// при каждом вызове GetAudioSets() и не сериализуются.
    /// </summary>
    public class AudioContentItem
    {
        /// <summary>Ключ истории вариантов ("ui_click", "enemy_spitter_fire") — глобально уникален и стабилен.</summary>
        public string EntityId;

        public string DisplayName;
        public Color Accent = new(0.45f, 0.45f, 0.45f);

        /// <summary>
        /// Где используется ИМЕННО эта сущность — человекочитаемое описание («сирена волны:
        /// событие swarm», «двигатель в движении, pitch от скорости»). Пусто — студия
        /// покажет сетовое значение.
        /// </summary>
        public string UsageNote;

        /// <summary>Задать описание (write-through в провайдера). null-колбэк — не редактируется.</summary>
        public Action<string> SetUsageNote;

        /// <summary>Длительность генерации, сек. 0 — взять DefaultSeconds сета (или стиля).</summary>
        public float Seconds;

        /// <summary>
        /// true — звук зациклен в игре (эмбиент, гул мотора). Студия проверяет луп
        /// в превью и подсказывает это модели в промпте.
        /// </summary>
        public bool Loop;

        public Func<AudioClip> GetClip;
        public Action<AudioClip> SetClip;

        // ── Громкость в игре (микширование записи) — опционально ──
        // Студия показывает слайдер и пишет write-through: слушая варианты, сразу
        // подгоняешь громкость записи, не бегая в инспектор библиотеки звуков.

        /// <summary>Громкость записи в игре 0..1. null-колбэк — слайдер не показывается.</summary>
        public Func<float> GetVolume;

        /// <summary>Задать громкость записи (write-through в провайдера).</summary>
        public Action<float> SetVolume;
        public Func<string> GetPrompt;
        public Action<string> SetPrompt;

        // ── Рандом-пул вариантов — опционально ──
        // Игра умеет играть случайный клип из пула (анти-повтор для частых SFX: попадания,
        // выстрелы). Студия может публиковать в пул несколько выбранных вариантов разом.
        // null-колбэки — сущность без пула.

        /// <summary>Текущий рандом-пул (без активного клипа). null-колбэк — не поддерживается.</summary>
        public Func<AudioClip[]> GetClipVariants;

        /// <summary>Назначить рандом-пул вариантов этой сущности.</summary>
        public Action<AudioClip[]> SetClipVariants;

        // Постобработка (трим/фейды/нормализация) хранится ПОФАЙЛОВО у каждого варианта —
        // это editor-концепция истории вариантов, в контракт провайдера не входит.

        /// <summary>Объект для Undo/SetDirty при назначении клипа/промпта (обычно сам конфиг).</summary>
        public UnityEngine.Object UndoTarget;
    }
}
