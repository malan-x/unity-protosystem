using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Рецепт варианта — зеркало AudioGenResult в сериализуемом виде. По нему вариант
    /// перегенерируется точь-в-точь, даже если WAV удалён при чистке.
    /// </summary>
    [Serializable]
    public class AudioRecipe
    {
        public string subject;
        public string positive;
        public string negative;
        public string lyrics;
        public float lyricsStrength;
        public int engine;
        public float seconds;
        public int seed;
        public int steps;
        public float cfg;
        public string sampler;
        public string scheduler;
        public string checkpoint;
        public string styleName;
        public bool trimSilence;
        public string runId;
        public string generatedAtUtc;

        // TTS: голос и параметры подачи (аддитивно — у старых рецептов пусто)
        public string voiceId;
        public string ttsModelId;
        public float ttsStability;
        public float ttsSimilarity;
        public string ttsLanguage;

        public string postFilter;
        public float targetLufs;   // 0 — без нормализации (аддитивно — у старых рецептов ноль)
    }

    /// <summary>Один сгенерированный вариант звука сущности.</summary>
    [Serializable]
    public class AudioVariant
    {
        public string id;               // guid8
        public AudioRecipe recipe;
        public AudioClip clip;          // null — файл удалён при чистке (рецепт живёт)
        public bool fileDeleted;
        public string wavAssetPath;
        public bool bad;
    }

    /// <summary>История вариантов одной звуковой сущности.</summary>
    [Serializable]
    public class EntityAudioHistory
    {
        public string entityId;
        public List<AudioVariant> variants = new();
        public string activeVariantId;

        public AudioVariant Active
        {
            get
            {
                if (string.IsNullOrEmpty(activeVariantId)) return null;
                return variants.Find(v => v.id == activeVariantId);
            }
        }

        public AudioVariant Find(string variantId) => variants.Find(v => v.id == variantId);
    }

    /// <summary>
    /// История вариантов всех звуковых сущностей проекта — один ассет. Editor-only:
    /// игра ссылается на клипы через свои конфиги (SetClip провайдера), библиотека
    /// вариантов в билд не попадает.
    /// </summary>
    public class AudioVariantLibrary : ScriptableObject
    {
        public List<EntityAudioHistory> entities = new();

        public EntityAudioHistory Find(string entityId)
            => entities.Find(e => e.entityId == entityId);

        public EntityAudioHistory GetOrCreate(string entityId)
        {
            var e = Find(entityId);
            if (e == null)
            {
                e = new EntityAudioHistory { entityId = entityId };
                entities.Add(e);
            }
            return e;
        }
    }
}
