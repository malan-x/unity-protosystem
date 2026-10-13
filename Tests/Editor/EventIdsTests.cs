using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ProtoSystem.Tests
{
    /// <summary>
    /// Номера событий пакета: один id — одно событие.
    /// Модули объявляют события в EventBus.&lt;Модуль&gt;, старые имена живут в Evt.
    /// Одинаковый id допустим только у алиасов одного события: русские классы-алиасы
    /// пропускаются, константа Evt совпадает с событием модуля по имени или
    /// записана в RenamedAliases.
    /// </summary>
    [TestFixture]
    public class EventIdsTests
    {
        /// <summary>Константы Evt — алиасы события модуля под другим именем</summary>
        private static readonly Dictionary<string, string> RenamedAliases = new Dictionary<string, string>
        {
            ["Evt.Scene.Unloaded"] = "EventBus.SceneFlow.UnloadCompleted",
            ["Evt.Settings.SettingsSaved"] = "EventBus.Settings.Saved",
            ["Evt.Settings.SettingsLoaded"] = "EventBus.Settings.Loaded",
            ["Evt.Settings.SettingsReset"] = "EventBus.Settings.ResetToDefaults",
            ["Evt.Settings.Audio.MasterVolumeChanged"] = "EventBus.Settings.Audio.MasterChanged",
            ["Evt.Settings.Audio.MusicVolumeChanged"] = "EventBus.Settings.Audio.MusicChanged",
            ["Evt.Settings.Audio.SFXVolumeChanged"] = "EventBus.Settings.Audio.SFXChanged",
            ["Evt.Settings.Audio.VoiceVolumeChanged"] = "EventBus.Settings.Audio.VoiceChanged",
            ["Evt.Settings.Audio.AmbientVolumeChanged"] = "EventBus.Settings.Audio.AmbientChanged",
            ["Evt.Settings.Audio.UIVolumeChanged"] = "EventBus.Settings.Audio.UIChanged",
        };

        [Test]
        public void EachEventId_MeansOneEvent()
        {
            var events = CollectEvents(typeof(EventBus), null).Concat(CollectEvents(typeof(Evt), null));
            var problems = new List<string>();

            foreach (var group in events.GroupBy(e => e.Id).Where(g => g.Count() > 1))
            {
                var names = group.Select(e => e.Name).ToList();
                for (int i = 0; i < names.Count; i++)
                    for (int j = i + 1; j < names.Count; j++)
                        if (!SameEvent(names[i], names[j]))
                            problems.Add($"{group.Key}: {names[i]} и {names[j]}");
            }

            Assert.IsEmpty(problems, "Один id у разных событий:\n" + string.Join("\n", problems));
        }

        [Test]
        public void RenamedAliases_PointToEventsWithSameId()
        {
            var ids = CollectEvents(typeof(EventBus), null).Concat(CollectEvents(typeof(Evt), null))
                .ToDictionary(e => e.Name, e => e.Id);

            foreach (var alias in RenamedAliases)
            {
                Assert.IsTrue(ids.ContainsKey(alias.Key), $"Нет константы {alias.Key}");
                Assert.IsTrue(ids.ContainsKey(alias.Value), $"Нет константы {alias.Value}");
                Assert.AreEqual(ids[alias.Value], ids[alias.Key], $"{alias.Key} должен быть алиасом {alias.Value}");
            }
        }

        private static bool SameEvent(string a, string b)
        {
            return LastSegment(a) == LastSegment(b)
                   || RenamedAliases.TryGetValue(a, out var aliasOfA) && aliasOfA == b
                   || RenamedAliases.TryGetValue(b, out var aliasOfB) && aliasOfB == a;
        }

        private static string LastSegment(string name) => name.Substring(name.LastIndexOf('.') + 1);

        /// <summary>
        /// Константы int класса и вложенных классов с полными именами.
        /// Русские классы-алиасы и [Obsolete]-классы пропускаются: они повторяют id своих событий.
        /// </summary>
        private static IEnumerable<(string Name, int Id)> CollectEvents(Type type, string prefix)
        {
            prefix = prefix == null ? type.Name : prefix + "." + type.Name;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(int))
                    yield return (prefix + "." + field.Name, (int)field.GetRawConstantValue());
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                if (nested.Name.Any(c => c > 127) || nested.IsDefined(typeof(ObsoleteAttribute), false))
                    continue;

                foreach (var e in CollectEvents(nested, prefix))
                    yield return e;
            }
        }
    }
}
