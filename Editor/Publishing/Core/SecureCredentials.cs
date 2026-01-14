// Packages/com.protosystem.core/Editor/Publishing/Core/SecureCredentials.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Хранение учётных данных.
    /// ВНИМАНИЕ: EditorPrefs НЕ является безопасным хранилищем!
    /// Данные хранятся в открытом виде в реестре Windows / plist macOS.
    /// </summary>
    public static class SecureCredentials
    {
        private const string PREFIX = "ProtoSystem.Publishing.Credentials.";
        private const string WARNING_KEY = PREFIX + "WarningShown";

        /// <summary>
        /// Показать предупреждение о безопасности (один раз)
        /// </summary>
        public static void ShowSecurityWarning()
        {
            if (EditorPrefs.GetBool(WARNING_KEY, false)) return;
            
            var result = EditorUtility.DisplayDialog(
                "⚠️ Предупреждение о безопасности",
                "Пароли и API ключи хранятся в EditorPrefs, который НЕ является безопасным хранилищем.\n\n" +
                "Данные сохраняются в открытом виде:\n" +
                "• Windows: реестр HKEY_CURRENT_USER\n" +
                "• macOS: ~/Library/Preferences/com.unity3d.UnityEditor.plist\n\n" +
                "Рекомендации:\n" +
                "• Используйте отдельный Steam аккаунт для загрузки\n" +
                "• Не сохраняйте пароли на общих компьютерах\n" +
                "• Рассмотрите использование Steam Guard через файл\n\n" +
                "Продолжить с сохранением?",
                "Понятно, сохранить",
                "Отмена"
            );
            
            if (result)
            {
                EditorPrefs.SetBool(WARNING_KEY, true);
            }
        }

        /// <summary>
        /// Сбросить флаг показа предупреждения
        /// </summary>
        public static void ResetWarning()
        {
            EditorPrefs.DeleteKey(WARNING_KEY);
        }

        /// <summary>
        /// Сохранить пароль для платформы
        /// </summary>
        public static void SetPassword(string platformId, string username, string password)
        {
            ShowSecurityWarning();
            
            var key = GetKey(platformId, username, "password");
            
            if (string.IsNullOrEmpty(password))
            {
                EditorPrefs.DeleteKey(key);
            }
            else
            {
                // Простое XOR шифрование (не безопасно, но лучше чем plaintext)
                var encoded = SimpleEncode(password);
                EditorPrefs.SetString(key, encoded);
            }
        }

        /// <summary>
        /// Получить пароль для платформы
        /// </summary>
        public static string GetPassword(string platformId, string username)
        {
            var key = GetKey(platformId, username, "password");
            var encoded = EditorPrefs.GetString(key, "");
            
            if (string.IsNullOrEmpty(encoded)) return "";
            
            return SimpleDecode(encoded);
        }

        /// <summary>
        /// Проверить наличие сохранённого пароля
        /// </summary>
        public static bool HasPassword(string platformId, string username)
        {
            var key = GetKey(platformId, username, "password");
            return EditorPrefs.HasKey(key);
        }

        /// <summary>
        /// Удалить пароль
        /// </summary>
        public static void DeletePassword(string platformId, string username)
        {
            var key = GetKey(platformId, username, "password");
            EditorPrefs.DeleteKey(key);
        }

        /// <summary>
        /// Сохранить API ключ
        /// </summary>
        public static void SetApiKey(string platformId, string keyName, string apiKey)
        {
            ShowSecurityWarning();
            
            var key = GetKey(platformId, keyName, "apikey");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                EditorPrefs.DeleteKey(key);
            }
            else
            {
                var encoded = SimpleEncode(apiKey);
                EditorPrefs.SetString(key, encoded);
            }
        }

        /// <summary>
        /// Получить API ключ
        /// </summary>
        public static string GetApiKey(string platformId, string keyName)
        {
            var key = GetKey(platformId, keyName, "apikey");
            var encoded = EditorPrefs.GetString(key, "");
            
            if (string.IsNullOrEmpty(encoded)) return "";
            
            return SimpleDecode(encoded);
        }

        /// <summary>
        /// Удалить все сохранённые данные
        /// </summary>
        public static void ClearAll()
        {
            // К сожалению, EditorPrefs не позволяет получить список ключей
            // Удаляем известные ключи
            var platforms = new[] { "steam", "itch", "epic", "gog" };
            
            foreach (var platform in platforms)
            {
                // Пытаемся удалить типичные ключи
                for (int i = 0; i < 10; i++)
                {
                    EditorPrefs.DeleteKey($"{PREFIX}{platform}.user{i}.password");
                    EditorPrefs.DeleteKey($"{PREFIX}{platform}.key{i}.apikey");
                }
            }
            
            EditorPrefs.DeleteKey(WARNING_KEY);
        }

        private static string GetKey(string platformId, string identifier, string type)
        {
            return $"{PREFIX}{platformId}.{identifier}.{type}";
        }

        // Простое XOR кодирование (НЕ БЕЗОПАСНО, только обфускация)
        private static string SimpleEncode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            var key = SystemInfo.deviceUniqueIdentifier;
            var result = new char[input.Length];
            
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = (char)(input[i] ^ key[i % key.Length]);
            }
            
            return System.Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(new string(result)));
        }

        private static string SimpleDecode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            try
            {
                var bytes = System.Convert.FromBase64String(input);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                var key = SystemInfo.deviceUniqueIdentifier;
                var result = new char[decoded.Length];
                
                for (int i = 0; i < decoded.Length; i++)
                {
                    result[i] = (char)(decoded[i] ^ key[i % key.Length]);
                }
                
                return new string(result);
            }
            catch
            {
                return "";
            }
        }
    }
}
