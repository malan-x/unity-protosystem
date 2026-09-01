// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/WishlistPromptState.cs
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Память панели вишлиста: показывалась ли она и принял ли игрок решение.
    ///
    /// Вынесено из системы отдельно, потому что читают и пишут состояние трое:
    /// сама система в рантайме, инспектор конфига (кнопка сброса) и, при желании,
    /// отладочные инструменты проекта. Ключи PlayerPrefs общие для всех проектов —
    /// панель в игре одна.
    ///
    /// Важно про PlayerPrefs: они общие у редактора и билда на одной машине, но
    /// свои на каждой машине. Поэтому проверять панель на другом компьютере
    /// приходится заново, а сбрасывать — руками.
    /// </summary>
    public static class WishlistPromptState
    {
        public const string PrefDecided = "wishlist_prompt_decided";
        public const string PrefShows   = "wishlist_prompt_shows";

        /// <summary>Игрок нажал «Добавить» или «Уже добавил» — панель молчит навсегда.</summary>
        public static bool Decided => PlayerPrefs.GetInt(PrefDecided, 0) == 1;

        /// <summary>Сколько раз панель показывалась.</summary>
        public static int Shows => PlayerPrefs.GetInt(PrefShows, 0);

        public static void RegisterShow()
        {
            PlayerPrefs.SetInt(PrefShows, Shows + 1);
            PlayerPrefs.Save();
        }

        public static void MarkDecided()
        {
            PlayerPrefs.SetInt(PrefDecided, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Забыть и решение, и счётчик показов — панель снова заработает.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PrefDecided);
            PlayerPrefs.DeleteKey(PrefShows);
            PlayerPrefs.Save();
        }
    }
}
