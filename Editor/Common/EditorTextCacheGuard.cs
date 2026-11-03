// Packages/com.protosystem.core/Editor/Common/EditorTextCacheGuard.cs
#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Обход бага IMGUI-текста Unity 6 (Advanced Text Generator). Подпись с эмодзи, сгенерированная,
    /// пока глифа ещё нет в динамическом атласе шрифта редактора, кэшируется с неверной разметкой:
    /// эмодзи рисуются в размер атласа (~100 px) поверх инспектора или пропадают совсем. Нативный кэш
    /// переживает перезагрузку домена и не сбрасывается, пока окно продолжает рисовать эту подпись.
    /// Сбрасываем кэш текста после загрузки домена и каждый раз, когда в шрифты редактора дописываются глифы.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorTextCacheGuard
    {
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const double CheckInterval = 0.25;

        private static readonly MethodInfo EmptyCacheMethod;
        private static readonly FieldInfo EditorTextSettingsField;
        private static readonly PropertyInfo OsFallbacksProperty;

        private static int _glyphCount = -1;
        private static double _nextCheck;

        static EditorTextCacheGuard()
        {
            var textHandle = typeof(GUIStyle).Assembly.GetType("UnityEngine.IMGUITextHandle");
            EmptyCacheMethod = textHandle?.GetMethod("EmptyCache", StaticFlags, null, Type.EmptyTypes, null);
            EditorTextSettingsField = textHandle?.GetField("s_EditorTextSettings", StaticFlags);
            OsFallbacksProperty = typeof(TextSettings).GetProperty("fallbackOSFontAssets",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // В другой версии Unity внутренности могут называться иначе — тогда обход просто не включается
            if (EmptyCacheMethod == null || EditorTextSettingsField == null)
                return;

            EditorApplication.delayCall += Flush;
            EditorApplication.update += WatchGlyphs;
        }

        [MenuItem("ProtoSystem/Обновить текст редактора")]
        private static void Flush()
        {
            try
            {
                EmptyCacheMethod?.Invoke(null, null);
                InternalEditorUtility.RepaintAllViews();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProtoSystem] Не удалось сбросить кэш текста редактора: {e.Message}");
            }
        }

        private static void WatchGlyphs()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck)
                return;
            _nextCheck = EditorApplication.timeSinceStartup + CheckInterval;

            int count = CountGlyphs();
            if (count == _glyphCount)
                return;

            bool firstCheck = _glyphCount < 0;
            _glyphCount = count;
            if (!firstCheck)
                Flush();
        }

        /// <summary>Глифы во всех fallback-шрифтах редактора, куда попадают эмодзи и символы</summary>
        private static int CountGlyphs()
        {
            if (EditorTextSettingsField.GetValue(null) is not TextSettings settings)
                return 0;

            int count = 0;
            if (settings.emojiFallbackTextAssets != null)
                foreach (var asset in settings.emojiFallbackTextAssets)
                    if (asset is FontAsset font && font != null)
                        count += font.glyphTable.Count;

            if (settings.fallbackFontAssets != null)
                foreach (var font in settings.fallbackFontAssets)
                    if (font != null)
                        count += font.glyphTable.Count;

            if (OsFallbacksProperty?.GetValue(settings) is IEnumerable osFonts)
                foreach (var item in osFonts)
                    if (item is FontAsset font && font != null)
                        count += font.glyphTable.Count;

            return count;
        }
    }
}
#endif
