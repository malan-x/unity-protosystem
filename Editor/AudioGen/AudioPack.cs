using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Пак звуков — снапшот выбранных вариантов по сущностям (зеркало IconPack арт-студии).
    /// Применение пака публикует записанные варианты одним кликом — быстрое переключение
    /// «звукового облика» игры между наборами.
    /// ОТДЕЛЬНЫЙ файл обязателен (SO-класс в чужом файле теряет привязку после рестарта).
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Studio/Audio Pack", fileName = "AudioPack_")]
    public class AudioPack : ScriptableObject
    {
        [Serializable]
        public struct Selection
        {
            public string entityId;
            public string variantId;
        }

        public string packName;
        public List<Selection> selections = new();

        public string ResolveName() => string.IsNullOrEmpty(packName) ? name : packName;

        public string VariantFor(string entityId)
        {
            foreach (var s in selections)
                if (s.entityId == entityId)
                    return s.variantId;
            return null;
        }
    }
}
