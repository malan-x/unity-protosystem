// Packages/com.protosystem.core/Editor/EventBus/EventIdDrawer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Editor.EventBus
{
    /// <summary>
    /// Drawer для [EventId] — выпадающий список всех событий шины.
    ///
    /// Список строится рефлексией: ищем статические классы с именем Evt в любой
    /// загруженной сборке и берём их вложенные категории с public const int.
    /// Так в один список попадают события пакета (ProtoSystem.Evt) и проекта
    /// (LastConvoy.Evt и любые другие) — редактор пакета не знает о проектах,
    /// а список всё равно полный.
    ///
    /// Ассеты-конфиги при этом живут в проекте: пакет даёт только тип и drawer.
    /// </summary>
    [CustomPropertyDrawer(typeof(EventIdAttribute))]
    public class EventIdDrawer : PropertyDrawer
    {
        private static string[] _paths;   // "LastConvoy/Сектор/Завершён"
        private static int[]    _ids;
        private static double   _builtAt;
        private const double CacheSeconds = 10.0;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Build();

            if (_ids.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // Событие могло исчезнуть из кода — не затираем значение молча,
            // а показываем его числом рядом со списком
            int index = Array.IndexOf(_ids, property.intValue);

            var dropdownRect = new Rect(position.x, position.y, position.width - 62f, position.height);
            var numberRect   = new Rect(position.xMax - 58f, position.y, 58f, position.height);

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(dropdownRect, label.text,
                index < 0 ? 0 : index, _paths.Select(p => p.Replace('/', '\u2215')).ToArray());
            if (EditorGUI.EndChangeCheck())
                property.intValue = _ids[picked];

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.IntField(numberRect, property.intValue);

            if (index < 0 && property.intValue != 0)
            {
                // Значение не найдено ни в одной шине — вероятно, событие переименовали
                var warnRect = new Rect(position.x, position.y, position.width, position.height);
                EditorGUI.LabelField(warnRect, GUIContent.none,
                    new GUIContent("", $"ID {property.intValue} не найден ни в одном классе Evt"));
            }
        }

        private static void Build()
        {
            if (_paths != null && EditorApplication.timeSinceStartup - _builtAt < CacheSeconds) return;

            var paths = new List<string>();
            var ids   = new List<int>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (var evt in types)
                {
                    if (evt.Name != "Evt" || !evt.IsAbstract || !evt.IsSealed) continue; // static class
                    var owner = string.IsNullOrEmpty(evt.Namespace) ? "Global" : evt.Namespace;

                    foreach (var category in evt.GetNestedTypes(BindingFlags.Public))
                    {
                        if (!category.IsClass) continue;   // вложенный enum EventType пропускаем
                        foreach (var f in category.GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (!f.IsLiteral || f.FieldType != typeof(int)) continue;
                            int value = (int)f.GetRawConstantValue();
                            if (ids.Contains(value)) continue;   // алиасы одного id
                            ids.Add(value);
                            paths.Add($"{owner}/{category.Name}/{f.Name}");
                        }
                    }
                }
            }

            var order = Enumerable.Range(0, paths.Count).OrderBy(i => paths[i], StringComparer.OrdinalIgnoreCase).ToArray();
            _paths   = order.Select(i => paths[i]).ToArray();
            _ids     = order.Select(i => ids[i]).ToArray();
            _builtAt = EditorApplication.timeSinceStartup;
        }
    }
}
