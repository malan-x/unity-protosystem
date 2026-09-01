// Packages/com.protosystem.core/Runtime/LiveOps/WishlistPrompt/StoreLink.cs
using UnityEngine;
using ProtoSystem.UI;

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Открытие страницы игры в магазине — одним способом для всех, кто просит
    /// вишлист: всплывающей панели, кнопки в главном меню, экрана финала.
    ///
    /// Добавить игру в желаемое программно нельзя: у Steamworks нет такого API
    /// (EOverlayToStoreFlag умеет только корзину), это сознательное ограничение
    /// Valve. Максимум доступного — довести человека до кнопки в один клик,
    /// не выкидывая его из игры.
    /// </summary>
    public static class StoreLink
    {
        /// <summary>
        /// Открывает страницу игры: оверлеем Steam, если он есть, иначе браузером.
        /// Возвращает false, только если открывать нечего (ни AppID, ни ссылки).
        /// </summary>
        public static bool OpenStorePage(uint steamAppId, string fallbackUrl)
        {
#if STEAMWORKS_NET
            if (steamAppId > 0 && SteamInitProvider.IsInitialized && SteamUtils.IsOverlayEnabled())
            {
                SteamFriends.ActivateGameOverlayToStore(
                    new AppId_t(steamAppId),
                    EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
                return true;
            }
#endif
            if (string.IsNullOrWhiteSpace(fallbackUrl)) return false;

            Application.OpenURL(fallbackUrl);
            return true;
        }

        /// <summary>То же, но параметры берутся из конфига панели вишлиста.</summary>
        public static bool OpenStorePage(WishlistPromptConfig config)
        {
            if (config == null) return false;
            return OpenStorePage(config.steamAppId, config.ResolveStoreUrl());
        }
    }
}
