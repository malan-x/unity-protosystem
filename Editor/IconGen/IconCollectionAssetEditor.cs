using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProtoSystem.IconGen;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Инспектор ассета набора: setId выбирается попапом из сетов назначенного провайдера
    /// (id руками не вспоминать), провайдер валидируется на IArtContentProvider,
    /// внизу — живая сводка (сколько сущностей разрешилось).
    /// </summary>
    [CustomEditor(typeof(IconCollectionAsset))]
    public class IconCollectionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var providerProp = serializedObject.FindProperty("provider");
            EditorGUILayout.PropertyField(providerProp);

            var setIdProp = serializedObject.FindProperty("setId");
            var providerObj = providerProp.objectReferenceValue;
            var provider = providerObj as IArtContentProvider;

            if (providerObj == null)
            {
                EditorGUILayout.HelpBox(
                    "Назначьте провайдера — ассет, реализующий IArtContentProvider " +
                    "(например, CampaignConfig).", MessageType.Info);
                EditorGUILayout.PropertyField(setIdProp);
            }
            else if (provider == null)
            {
                EditorGUILayout.HelpBox(
                    $"«{providerObj.name}» не реализует IArtContentProvider — сущности " +
                    "взять неоткуда.", MessageType.Error);
                EditorGUILayout.PropertyField(setIdProp);
            }
            else
            {
                // Попап сетов провайдера; чужое/устаревшее значение не затираем молча
                var ids = new List<string>();
                var labels = new List<string>();
                foreach (var s in provider.GetArtSets())
                {
                    if (s == null || string.IsNullOrEmpty(s.Id)) continue;
                    ids.Add(s.Id);
                    labels.Add($"{s.Title} ({s.Id})");
                }

                if (ids.Count == 0)
                {
                    EditorGUILayout.HelpBox("Провайдер не вернул ни одного сета.", MessageType.Warning);
                    EditorGUILayout.PropertyField(setIdProp);
                }
                else
                {
                    int cur = ids.IndexOf(setIdProp.stringValue);
                    if (cur < 0)
                    {
                        ids.Insert(0, setIdProp.stringValue);
                        labels.Insert(0, string.IsNullOrEmpty(setIdProp.stringValue)
                            ? "(не выбран)" : $"(нет у провайдера: {setIdProp.stringValue})");
                        cur = 0;
                    }
                    int next = EditorGUILayout.Popup("Сет провайдера", cur, labels.ToArray());
                    if (next != cur) setIdProp.stringValue = ids[next];
                }
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "provider", "setId");
            serializedObject.ApplyModifiedProperties();

            // Живая сводка — сразу видно, что связка провайдер+сет рабочая
            var asset = (IconCollectionAsset)target;
            var set = asset.ResolveSet();
            EditorGUILayout.Space(4);
            if (set != null)
            {
                var storage = asset.ResolveStorageSize();
                bool overridden = asset.storageSize.x > 0 && asset.storageSize.y > 0;
                string storageNote = overridden
                    ? (set.PreferredStorageSize.x > 0 && set.PreferredStorageSize != asset.storageSize
                        ? $"{storage.x}×{storage.y} (override; контракт провайдера " +
                          $"{set.PreferredStorageSize.x}×{set.PreferredStorageSize.y})"
                        : $"{storage.x}×{storage.y}")
                    : $"{storage.x}×{storage.y} (из контракта провайдера)";

                EditorGUILayout.HelpBox(
                    $"«{asset.ResolveTitle()}»: {set.Items.Count} сущностей, хранение " +
                    $"{storageNote}, рендер {asset.renderSize.x}×{asset.renderSize.y}." +
                    (string.IsNullOrEmpty(set.UsageNote) ? "" : $"\nИспользуется: {set.UsageNote}."),
                    MessageType.None);

                // Хранение меньше показа — арт будет мылиться при выводе
                foreach (var it in set.Items)
                {
                    if (it == null || it.DisplaySize.x <= 0) continue;
                    if (it.DisplaySize.x > storage.x || it.DisplaySize.y > storage.y)
                    {
                        EditorGUILayout.HelpBox(
                            $"«{it.DisplayName}» показывается {it.DisplaySize.x}×{it.DisplaySize.y}" +
                            $" — больше размера хранения {storage.x}×{storage.y}: будет мыло.",
                            MessageType.Warning);
                        break;   // одного предупреждения достаточно
                    }
                }
            }
            else
                EditorGUILayout.HelpBox("Сет не разрешается — проверьте провайдера и setId.",
                    MessageType.Warning);
        }
    }
}
