using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Группа наборов — одна вкладка студии из нескольких наборов секциями
    /// («Здания» = иконки + превью).
    /// Живёт в своём файле с тем же именем: пока класс лежал в IconStudioConfig.cs, у Unity не было
    /// MonoScript для него, ассеты групп сохранялись с пустым m_Script и после перезапуска
    /// редактора не загружались — вкладки-группы пропадали из студии.
    /// </summary>
    [CreateAssetMenu(fileName = "IconCollectionGroup", menuName = "ProtoSystem/Icon Studio/Group")]
    public class IconCollectionGroup : ScriptableObject
    {
        public string title;
        public List<IconCollectionAsset> collections = new();

        public string ResolveTitle() => string.IsNullOrEmpty(title) ? name : title;
    }
}
