using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Группа наборов — одна вкладка студии из нескольких секций.
    /// ОТДЕЛЬНЫЙ файл обязателен: класс ScriptableObject, объявленный вторым в чужом
    /// файле, теряет привязку скрипта у ассетов после перезапуска редактора
    /// (m_Script: 0 → ассет грузится голым Object).
    /// </summary>
    [CreateAssetMenu(menuName = "ProtoSystem/Audio Studio/Group", fileName = "AudioGroup_")]
    public class AudioCollectionGroup : ScriptableObject
    {
        public string title;
        public List<AudioCollectionAsset> collections = new();

        public string ResolveTitle() => string.IsNullOrEmpty(title) ? name : title;
    }
}
