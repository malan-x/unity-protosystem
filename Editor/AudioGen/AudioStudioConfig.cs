using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Конфиг аудио-студии: упорядоченные вкладки (набор или группа).
    /// Несколько конфигов в проекте → дропдаун в тулбаре окна.
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Studio/Studio Config", fileName = "AudioStudioConfig")]
    public class AudioStudioConfig : ScriptableObject
    {
        [Tooltip("Вкладки по порядку: AudioCollectionAsset или AudioCollectionGroup.")]
        public List<Object> tabs = new();

        public List<AudioCollectionAsset> CollectionsOfTab(Object tab)
        {
            var result = new List<AudioCollectionAsset>();
            switch (tab)
            {
                case AudioCollectionAsset col when col != null: result.Add(col); break;
                case AudioCollectionGroup group when group != null:
                    foreach (var c in group.collections)
                        if (c != null) result.Add(c);
                    break;
            }
            return result;
        }

        public string TabTitle(Object tab) => tab switch
        {
            AudioCollectionAsset col => col.ResolveTitle(),
            AudioCollectionGroup group => group.ResolveTitle(),
            _ => "?",
        };
    }
}
