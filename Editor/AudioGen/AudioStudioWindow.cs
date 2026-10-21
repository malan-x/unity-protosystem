using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// «ИИ Аудио студия» — генерация звуков игры на локальном ComfyUI: UI-звуки, SFX,
    /// эмбиенты (Stable Audio Open), музыка/стингеры (ACE-Step). История вариантов
    /// с рецептами, публикация активного варианта прямо в SoundLibrary.
    /// Устройство зеркалит IconStudioWindow (арт-студию), но проще: без рендишенов,
    /// паков и 9-slice. UI Toolkit, retained mode — всё по событиям.
    /// Проектные надстройки (игровые лупы, гейт соло) — через AudioStudioExtensions.
    /// </summary>
    public class AudioStudioWindow : EditorWindow
    {
        [MenuItem("ProtoSystem/ИИ студии/ИИ Аудио студия")]
        public static void Open()
        {
            var w = GetWindow<AudioStudioWindow>();
            w.titleContent = new GUIContent("ИИ Аудио студия");
            w.minSize = new Vector2(900, 500);
        }

        /// <summary>Открыть студию сразу на звуке (переход из SoundLibrary/проекта).</summary>
        public static void OpenAt(string soundId)
        {
            Open();
            var w = GetWindow<AudioStudioWindow>();
            w.rootVisualElement.schedule.Execute(() =>
            {
                if (!w.NavigateToEntity(soundId))
                    w.ShowNotification(new GUIContent($"«{soundId}» не найден среди наборов студии"));
            }).StartingIn(120);
        }

        // ── Состояние окна (EditorPrefs) ──

        private static string ViewPref
        {
            get => EditorPrefs.GetString("AudioStudio.View", "");
            set => EditorPrefs.SetString("AudioStudio.View", value);
        }

        private static string ConfigPref
        {
            get => EditorPrefs.GetString("AudioStudio.Config", "");
            set => EditorPrefs.SetString("AudioStudio.Config", value);
        }

        private static int BatchCount
        {
            get => EditorPrefs.GetInt("AudioStudio.Batch", 3);
            set => EditorPrefs.SetInt("AudioStudio.Batch", value);
        }

        // ── Живые ссылки ──

        private ScrollView _leftPanel;
        private ScrollView _gridScroll;
        private VisualElement _grid;
        private VisualElement _pickerHost;
        private Toolbar _toolbar;
        private Label _queueLabel;

        /// <summary>Sound id, звучащие прямо сейчас в Play Mode (обновляет монитор) — для подсветки строк.</summary>
        private readonly HashSet<string> _playingSoundIds = new();

        private string _selectedEntityId;
        private string _searchText = "";
        private readonly HashSet<string> _pickedVariantIds = new();
        private string _pickAnchorId;   // якорь Shift-диапазона

        private static List<string> _serverCheckpoints;

        private void CreateGUI()
        {
            AudioVariantStore.Changed += OnStoreChanged;
            AiAudioGenerator.QueueChanged += OnQueueChanged;
            AudioGenAssetWatcher.AssetsChanged += OnStoreChanged;

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.protosystem.core/Editor/AudioGen/AudioStudio.uss");
            if (uss != null) rootVisualElement.styleSheets.Add(uss);

            BuildUI();
        }

        private void OnDisable()
        {
            AudioVariantStore.Changed -= OnStoreChanged;
            AiAudioGenerator.QueueChanged -= OnQueueChanged;
            AudioGenAssetWatcher.AssetsChanged -= OnStoreChanged;
            AudioPreviewUtil.StopAll();
        }

        // Дебаунс: сколько бы событий ни прилетело за кадр, пересборка одна.
        // Одноразовый EditorApplication.update, а не schedule — планировщик скрытой панели не тикает.
        private bool _refreshQueued;

        private void OnStoreChanged()
        {
            _clipToSoundId = null;   // библиотека могла измениться — реверс-карта пересоберётся
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.update += RefreshOnce;
        }

        private void RefreshOnce()
        {
            EditorApplication.update -= RefreshOnce;
            _refreshQueued = false;
            if (rootVisualElement == null) return;
            RebuildGrid();
            RebuildPicker();
        }

        private void OnQueueChanged()
        {
            UpdateQueueUI();
        }

        // ── Конфиг и вкладки ──

        private static List<AudioStudioConfig> AllConfigs()
        {
            var result = new List<AudioStudioConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioStudioConfig"))
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioStudioConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) result.Add(c);
            }
            return result;
        }

        private AudioStudioConfig ActiveConfig()
        {
            var all = AllConfigs();
            if (all.Count == 0) return null;
            string guid = ConfigPref;
            foreach (var c in all)
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c)) == guid)
                    return c;
            return all[0];
        }

        /// <summary>Вкладки: объекты конфига; без конфига — все наборы проекта по одному.</summary>
        private List<UnityEngine.Object> Views()
        {
            var config = ActiveConfig();
            if (config != null && config.tabs.Count > 0)
                return config.tabs.Where(t => t != null).ToList();

            var result = new List<UnityEngine.Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioCollectionAsset"))
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioCollectionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) result.Add(c);
            }
            return result;
        }

        private string TabTitle(UnityEngine.Object tab)
        {
            var config = ActiveConfig();
            if (config != null) return config.TabTitle(tab);
            return tab is AudioCollectionAsset col ? col.ResolveTitle() : tab.name;
        }

        private UnityEngine.Object ActiveView()
        {
            var views = Views();
            if (views.Count == 0) return null;
            string pref = ViewPref;
            foreach (var v in views)
                if (v.name == pref) return v;
            return views[0];
        }

        private List<AudioCollection> ActiveCollections()
        {
            var view = ActiveView();
            if (view == null) return new List<AudioCollection>();

            var config = ActiveConfig();
            var assets = config != null
                ? config.CollectionsOfTab(view)
                : view is AudioCollectionAsset col ? new List<AudioCollectionAsset> { col } : new();

            return assets.Select(AudioCollection.FromAsset).Where(c => c != null).ToList();
        }

        private (AudioCollection col, AudioItem item) FindSelected()
        {
            if (string.IsNullOrEmpty(_selectedEntityId)) return (null, null);
            foreach (var col in ActiveCollections())
                foreach (var item in col.GetItems())
                    if (item.EntityId == _selectedEntityId)
                        return (col, item);
            return (null, null);
        }

        // ── Каркас ──

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var split = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(split);

            _leftPanel = new ScrollView { name = "left-panel" };
            _leftPanel.AddToClassList("left-panel");
            split.Add(_leftPanel);

            var main = new VisualElement { style = { flexGrow = 1 } };
            split.Add(main);

            _toolbar = new Toolbar();
            main.Add(_toolbar);

            var vsplit = new TwoPaneSplitView(1, 260, TwoPaneSplitViewOrientation.Vertical);
            vsplit.style.flexGrow = 1;
            main.Add(vsplit);

            _gridScroll = new ScrollView();
            _gridScroll.AddToClassList("grid-scroll");
            _grid = new VisualElement();
            _gridScroll.Add(_grid);
            vsplit.Add(_gridScroll);

            var pickerScroll = new ScrollView();
            pickerScroll.AddToClassList("picker");
            _pickerHost = new VisualElement();
            pickerScroll.Add(_pickerHost);
            vsplit.Add(pickerScroll);

            RebuildToolbar();
            RebuildLeftPanel();
            RebuildGrid();
            RebuildPicker();
        }

        // ── Тулбар ──

        private void RebuildToolbar()
        {
            _toolbar.Clear();

            var configs = AllConfigs();
            if (configs.Count > 1)
            {
                var names = configs.Select(c => c.name).ToList();
                var active = ActiveConfig();
                var dd = new DropdownField(names, Mathf.Max(0, configs.IndexOf(active)));
                dd.RegisterValueChangedCallback(_ =>
                {
                    var picked = configs.FirstOrDefault(c => c.name == dd.value);
                    ConfigPref = picked != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked)) : "";
                    RebuildToolbar();
                    RebuildLeftPanel();
                    RebuildGrid();
                });
                _toolbar.Add(dd);
            }

            var activeView = ActiveView();
            foreach (var view in Views())
            {
                var toggle = new ToolbarToggle { text = TabTitle(view), value = view == activeView };
                var captured = view;
                toggle.RegisterValueChangedCallback(_ =>
                {
                    ViewPref = captured.name;
                    _selectedEntityId = null;
                    RebuildToolbar();
                    RebuildLeftPanel();
                    RebuildGrid();
                    RebuildPicker();
                });
                _toolbar.Add(toggle);
            }

            // Паки: применить снапшот вариантов одним кликом / сохранить текущий выбор
            var packsMenu = new ToolbarMenu { text = "Паки ▾" };
            foreach (var pack in AudioVariantStore.AllPacks())
            {
                var captured = pack;
                packsMenu.menu.AppendAction(
                    pack.ResolveName(),
                    _ => ApplyPack(captured),
                    AudioVariantStore.PackMatchesCurrent(pack)
                        ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            if (AudioVariantStore.AllPacks().Count > 0) packsMenu.menu.AppendSeparator();
            packsMenu.menu.AppendAction("Сохранить текущий выбор как пак", _ =>
            {
                var pack = AudioVariantStore.SavePackFromActive(
                    "Пак " + System.DateTime.Now.ToString("dd.MM HH:mm"));
                EditorGUIUtility.PingObject(pack);
                ShowNotification(new GUIContent($"Пак сохранён: {pack.selections.Count} звуков (F2 — переименовать)"));
            });
            _toolbar.Add(packsMenu);

            // Поиск по звукам активной вкладки (id/имя/описание)
            var search = new ToolbarSearchField { value = _searchText };
            search.AddToClassList("sound-search");
            search.RegisterValueChangedCallback(e =>
            {
                _searchText = e.newValue;
                RebuildGrid();
            });
            _toolbar.Add(search);

            _toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            // Громкость прослушивания — чтобы пачки вариантов не орали на максимум
            var volIcon = new Label("🔊");
            volIcon.AddToClassList("volume-icon");
            _toolbar.Add(volIcon);
            var volume = new Slider(0f, 1f) { value = AudioPreviewUtil.Volume };
            volume.AddToClassList("volume-slider");
            volume.tooltip = "Громкость прослушивания";
            volume.RegisterValueChangedCallback(e => AudioPreviewUtil.Volume = e.newValue);
            _toolbar.Add(volume);

            // Размер пачки
            var batch = new DropdownField(
                new List<string> { "×1", "×2", "×3", "×4", "×6", "×8" },
                BatchIndex(BatchCount));
            batch.RegisterValueChangedCallback(_ =>
                BatchCount = int.Parse(batch.value.TrimStart('×')));
            batch.tooltip = "Сколько вариантов генерировать за клик";
            _toolbar.Add(batch);

            var genAll = new ToolbarButton(() => EnqueueAll())
            {
                text = "Все ×" + BatchCount,
                tooltip = "Сгенерировать пачку для каждой сущности вкладки",
            };
            _toolbar.Add(genAll);

            var genMissing = new ToolbarButton(() => EnqueueAll(onlyMissing: true))
            {
                text = "Пустые ×" + BatchCount,
                tooltip = "Только сущности без клипа в игре",
            };
            _toolbar.Add(genMissing);

            var usageBtn = new ToolbarButton(RunUsageScan)
            {
                text = SoundUsageScanner.HasResults ? "Исп. ⟳" : "Исп.?",
                tooltip = "Проверить по проекту, где звуки реально используются " +
                          "(профили, схемы, префабы, сцены, код). Неиспользуемые пометятся ⚠",
            };
            _toolbar.Add(usageBtn);

            if (SoundSoloMode.Active)
            {
                var soloOff = new ToolbarButton(() =>
                {
                    SoundSoloMode.Disable();
                    RebuildToolbar();
                    RebuildGrid();
                })
                {
                    text = $"СОЛО: {SoundSoloMode.SoloCount} ✕",
                    tooltip = "Режим соло активен — в игре слышны только отмеченные звуки. Клик — выключить",
                };
                soloOff.AddToClassList("solo-master");
                _toolbar.Add(soloOff);
            }

            _queueLabel = new Label();
            _queueLabel.AddToClassList("queue-label");
            _toolbar.Add(_queueLabel);

            var cancel = new ToolbarButton(AiAudioGenerator.CancelAll) { text = "✕", tooltip = "Отменить очередь" };
            _toolbar.Add(cancel);

            UpdateQueueUI();
        }

        private static int BatchIndex(int count) => count switch
        {
            1 => 0, 2 => 1, 3 => 2, 4 => 3, 6 => 4, 8 => 5, _ => 2,
        };

        private void UpdateQueueUI()
        {
            if (_queueLabel == null) return;
            _queueLabel.text = AiAudioGenerator.IsRunning
                ? $"⏳ {AiAudioGenerator.Done}/{AiAudioGenerator.Total} · {AiAudioGenerator.CurrentLabel}"
                : "";
        }

        // ── Левая панель ──

        private void RebuildLeftPanel()
        {
            _leftPanel.Clear();

            var cols = ActiveCollections();
            var col = cols.FirstOrDefault();

            var header = new Label("ИИ Аудио студия");
            header.AddToClassList("panel-header");
            _leftPanel.Add(header);

            AddComfyStatusRow();

            if (!AudioConvert.IsAvailable)
            {
                var warn = new HelpBox(
                    "ffmpeg не найден — WAV не собрать. Путь: Project Settings ▸ AI Audio Tools.",
                    HelpBoxMessageType.Warning);
                _leftPanel.Add(warn);
            }

            if (col?.SourceAsset == null)
            {
                _leftPanel.Add(new HelpBox(
                    "Нет наборов. Создайте AudioCollectionAsset (ProtoSystem ▸ Audio Studio ▸ Collection) " +
                    "и добавьте его в AudioStudioConfig.", HelpBoxMessageType.Info));
                return;
            }

            // Профиль набора — хранится в ассете набора (не в EditorPrefs)
            var profiles = AudioGenProfile.All();
            var profileNames = profiles.Select(p => p.name).ToList();
            profileNames.Insert(0, "(без профиля)");
            int current = col.SourceAsset.profile != null
                ? profiles.ToList().IndexOf(col.SourceAsset.profile) + 1 : 0;

            var profileDd = new DropdownField("Профиль", profileNames, Mathf.Max(0, current));
            profileDd.RegisterValueChangedCallback(_ =>
            {
                int idx = profileNames.IndexOf(profileDd.value);
                foreach (var c in cols)
                {
                    if (c.SourceAsset == null) continue;
                    c.SourceAsset.profile = idx <= 0 ? null : profiles[idx - 1];
                    EditorUtility.SetDirty(c.SourceAsset);
                }
                RebuildLeftPanel();
            });
            _leftPanel.Add(profileDd);

            var profile = col.SourceAsset.profile;
            if (profile != null && !string.IsNullOrEmpty(profile.description))
            {
                var desc = new Label(profile.description);
                desc.AddToClassList("profile-desc");
                _leftPanel.Add(desc);
            }

            var style = profile != null ? profile.style : AudioStyleRegistry.Default;
            if (style == null)
            {
                _leftPanel.Add(new HelpBox(
                    "Нет стиля. Создайте AudioStylePreset (ProtoSystem ▸ Audio Style Preset).",
                    HelpBoxMessageType.Warning));
                return;
            }

            var engineLabel = new Label(style.engine switch
            {
                AudioEngine.AceStep => "Движок: ACE-Step (музыка, локально)",
                AudioEngine.ElevenLabs => "Движок: ElevenLabs (SFX, облако)",
                AudioEngine.ElevenLabsTts => "Движок: ElevenLabs TTS (голос, облако)",
                AudioEngine.QwenTts => "Движок: Qwen3-TTS (голос, ЛОКАЛЬНО)",
                _ => "Движок: Stable Audio Open (SFX, локально)",
            });
            engineLabel.AddToClassList("engine-label");
            _leftPanel.Add(engineLabel);

            // Промпт-шаблон и негатив пишутся прямо в ассет стиля
            _leftPanel.Add(MakeStyleField("Шаблон ({subject})", style.positiveTemplate,
                v => { style.positiveTemplate = v; EditorUtility.SetDirty(style); }));

            // Негатив есть только у Stable Audio, лирика — только у ACE-Step;
            // ElevenLabs не поддерживает ни того, ни другого — поля не показываем.
            if (style.engine == AudioEngine.StableAudio)
                _leftPanel.Add(MakeStyleField("Негатив", style.negative,
                    v => { style.negative = v; EditorUtility.SetDirty(style); }));
            else if (style.engine == AudioEngine.AceStep)
                _leftPanel.Add(MakeStyleField("Лирика (пусто — [inst])", style.lyrics,
                    v => { style.lyrics = v; EditorUtility.SetDirty(style); }));

            var row = new VisualElement();
            row.AddToClassList("param-row");
            var secondsField = new FloatField("Секунды") { value = style.seconds };
            secondsField.RegisterValueChangedCallback(e =>
            {
                style.seconds = Mathf.Clamp(e.newValue, 1f, 300f);
                EditorUtility.SetDirty(style);
            });
            secondsField.tooltip = "Дефолт стиля; сет/сущность могут переопределить";
            row.Add(secondsField);
            _leftPanel.Add(row);

            if (style.engine == AudioEngine.ElevenLabs)
            {
                // У облака нет шагов/CFG — только строгость следования промпту
                var infl = new Slider("Строгость промпта", 0f, 1f) { value = style.promptInfluence };
                infl.tooltip = "Выше — точнее по промпту, но однообразнее";
                infl.RegisterValueChangedCallback(e =>
                {
                    style.promptInfluence = e.newValue;
                    EditorUtility.SetDirty(style);
                });
                _leftPanel.Add(infl);
            }
            else if (style.engine == AudioEngine.ElevenLabsTts)
            {
                AddTtsRows(style);
            }
            else if (style.engine == AudioEngine.QwenTts)
            {
                // Локальный TTS: описание голоса (voice design, поле lyrics) + язык
                _leftPanel.Add(MakeStyleField("Описание голоса (англ.)", style.lyrics,
                    v => { style.lyrics = v; EditorUtility.SetDirty(style); }));

                var langField = new TextField("Язык (Russian/English)") { value = style.ttsLanguage };
                langField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    style.ttsLanguage = langField.value;
                    EditorUtility.SetDirty(style);
                });
                _leftPanel.Add(langField);

                if (!QwenTtsClient.IsAvailable)
                    _leftPanel.Add(new HelpBox(
                        "qwen-tts.exe или GGUF-модели не найдены — пути в Project Settings ▸ AI Audio Tools.",
                        HelpBoxMessageType.Warning));
            }
            else
            {
                var stepsField = new IntegerField("Шаги") { value = style.steps };
                stepsField.RegisterValueChangedCallback(e =>
                {
                    style.steps = Mathf.Clamp(e.newValue, 4, 200);
                    EditorUtility.SetDirty(style);
                });
                _leftPanel.Add(stepsField);

                var cfgField = new FloatField("CFG") { value = style.cfg };
                cfgField.RegisterValueChangedCallback(e =>
                {
                    style.cfg = Mathf.Clamp(e.newValue, 0f, 30f);
                    EditorUtility.SetDirty(style);
                });
                _leftPanel.Add(cfgField);
            }

            // Чекпоинт профиля — из списка на сервере (только ComfyUI-движкам)
            if (profile != null &&
                (style.engine == AudioEngine.StableAudio || style.engine == AudioEngine.AceStep))
            {
                var ckptRow = new VisualElement();
                ckptRow.AddToClassList("param-row");
                var options = new List<string> { "(движок по умолчанию)" };
                if (_serverCheckpoints != null)
                    options.AddRange(_serverCheckpoints.Where(c => c.Contains("audio") || c.Contains("ace")
                                                                    || c == profile.checkpoint));
                if (!string.IsNullOrEmpty(profile.checkpoint) && !options.Contains(profile.checkpoint))
                    options.Add(profile.checkpoint);

                int ci = string.IsNullOrEmpty(profile.checkpoint)
                    ? 0 : Mathf.Max(0, options.IndexOf(profile.checkpoint));
                var ckpt = new DropdownField("Модель", options, ci);
                ckpt.RegisterValueChangedCallback(_ =>
                {
                    profile.checkpoint = options.IndexOf(ckpt.value) <= 0 ? "" : ckpt.value;
                    EditorUtility.SetDirty(profile);
                });
                ckptRow.Add(ckpt);
                _leftPanel.Add(ckptRow);

                if (_serverCheckpoints == null) FetchModels();
            }

            if (AiAudioGenerator.IsCloud(style.engine))
                AddCreditsRow();

            var settingsBtn = new Button(() =>
                SettingsService.OpenProjectSettings("Project/AI Audio Tools"))
            { text = "Настройки AI-аудио…" };
            settingsBtn.AddToClassList("settings-btn");
            _leftPanel.Add(settingsBtn);

            AddNowPlayingSection();
        }

        /// <summary>Остаток кредитов ElevenLabs (обновляется раз в 30 с и после пачек).</summary>
        private void AddCreditsRow()
        {
            var label = new Label("Кредиты: …");
            label.AddToClassList("credits-row");
            _leftPanel.Add(label);

            async void Refresh()
            {
                var c = await ElevenLabsClient.GetCreditsAsync();
                if (label.panel == null) return;
                if (c.Ok)
                {
                    label.text = $"Кредиты: {c.Remaining:N0} из {c.Limit:N0}";
                    label.style.color = c.Remaining < c.Limit / 10
                        ? new Color(0.9f, 0.5f, 0.3f) : new Color(0.6f, 0.75f, 0.6f);
                    label.tooltip = $"Потрачено: {c.Used:N0}";
                }
                else if (c.NoPermission)
                {
                    label.text = "Кредиты: нет права User Read у ключа";
                    label.style.color = new Color(0.7f, 0.6f, 0.4f);
                    label.tooltip = "Включите Administration ▸ User ▸ Read у API-ключа на elevenlabs.io";
                }
                else
                {
                    label.text = "Кредиты: недоступно";
                }
            }

            label.schedule.Execute(Refresh).Every(30000);
            Refresh();

            // после завершения очереди — обновить сразу (потратились)
            void OnQueue()
            {
                if (!AiAudioGenerator.IsRunning)
                {
                    ElevenLabsClient.InvalidateCredits();
                    Refresh();
                }
            }
            AiAudioGenerator.QueueChanged += OnQueue;
            label.RegisterCallback<DetachFromPanelEvent>(_ => AiAudioGenerator.QueueChanged -= OnQueue);
        }

        // ── Монитор «Сейчас звучит» ──
        // Editor-полл активных AudioSource в Play Mode: видит ВСЕ звуки (пул SoundManager,
        // LoopPlayer'ы миссии, источник врагов, голос), в билд не попадает ни байта.
        // При выносе в пакет заменить/дополнить событиями SoundStarted у SoundManagerSystem.

        private VisualElement _nowPlayingHost;
        private Dictionary<AudioClip, string> _clipToSoundId;

        private void AddNowPlayingSection()
        {
            var header = new Label("Сейчас звучит");
            header.AddToClassList("now-playing-header");
            _leftPanel.Add(header);

            _nowPlayingHost = new VisualElement();
            _nowPlayingHost.AddToClassList("now-playing");
            _leftPanel.Add(_nowPlayingHost);

            _nowPlayingHost.schedule.Execute(RefreshNowPlaying).Every(300);
            RefreshNowPlaying();
        }

        private void RefreshNowPlaying()
        {
            if (_nowPlayingHost == null || _nowPlayingHost.panel == null) return;
            _nowPlayingHost.Clear();

            if (!EditorApplication.isPlaying)
            {
                _playingSoundIds.Clear();
                var hint = new Label("— (в Play Mode тут видно все играющие звуки)");
                hint.AddToClassList("now-playing-hint");
                _nowPlayingHost.Add(hint);
                return;
            }

            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            _playingSoundIds.Clear();
            foreach (var src in sources)
            {
                if (src == null || !src.isPlaying || src.clip == null) continue;
                string playingId = SoundIdOf(src.clip);
                if (playingId != null) _playingSoundIds.Add(playingId);
            }

            int shown = 0;
            foreach (var src in sources)
            {
                if (src == null || !src.isPlaying || src.clip == null) continue;
                if (shown >= 14)
                {
                    var more = new Label("…");
                    more.AddToClassList("now-playing-hint");
                    _nowPlayingHost.Add(more);
                    break;
                }
                _nowPlayingHost.Add(MakeNowPlayingRow(src));
                shown++;
            }

            if (shown == 0)
            {
                var silence = new Label("тишина");
                silence.AddToClassList("now-playing-hint");
                _nowPlayingHost.Add(silence);
            }
        }

        private VisualElement MakeNowPlayingRow(AudioSource src)
        {
            var row = new VisualElement();
            row.AddToClassList("now-playing-row");

            var stop = new Button(() => { if (src != null) src.Stop(); }) { text = "■" };
            stop.AddToClassList("now-playing-stop");
            stop.tooltip = "Остановить этот источник";
            row.Add(stop);

            string id = SoundIdOf(src.clip);
            var name = new Label(id ?? src.clip.name);
            name.AddToClassList("now-playing-name");
            name.tooltip = $"{src.clip.name} • {src.gameObject.name}" +
                           (src.outputAudioMixerGroup != null ? $" • {src.outputAudioMixerGroup.name}" : "");
            row.Add(name);

            string time = src.loop ? "∞" : $"{src.time:0.0}/{src.clip.length:0.0}";
            var info = new Label(time);
            info.AddToClassList("now-playing-time");
            row.Add(info);

            // Клик — перейти к звуку в студии (сменить вариант можно прямо во время игры);
            // Ctrl+клик или звук не из библиотеки — пинг источника в иерархии
            name.tooltip += "\nКлик — открыть в студии; Ctrl+клик — показать источник в иерархии";
            row.RegisterCallback<ClickEvent>(e =>
            {
                if (id != null && !e.ctrlKey && !e.commandKey)
                {
                    if (NavigateToEntity(id)) return;
                }
                if (src != null) EditorGUIUtility.PingObject(src.gameObject);
            });
            return row;
        }

        /// <summary>
        /// Найти сущность по sound id среди всех вкладок, переключиться и проскроллить к ней.
        /// Работает и в Play Mode — можно менять варианты на лету и слушать разницу.
        /// </summary>
        private bool NavigateToEntity(string entityId)
        {
            foreach (var view in Views())
            {
                var config = ActiveConfig();
                var assets = config != null
                    ? config.CollectionsOfTab(view)
                    : view is AudioCollectionAsset col ? new List<AudioCollectionAsset> { col } : new();

                foreach (var asset in assets)
                {
                    var collection = AudioCollection.FromAsset(asset);
                    if (collection == null) continue;
                    foreach (var item in collection.GetItems())
                    {
                        if (item.EntityId != entityId) continue;

                        ViewPref = view.name;
                        _selectedEntityId = entityId;
                        _pickedVariantIds.Clear();
                        _pickAnchorId = null;
                        RebuildToolbar();
                        RebuildGrid();
                        RebuildPicker();

                        // скролл после лейаута свежего грида
                        _gridScroll?.schedule.Execute(() =>
                        {
                            var row = _grid?.Q(name: entityId);
                            if (row != null) _gridScroll.ScrollTo(row);
                        }).StartingIn(60);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Живое применение громкости: уже играющие источники этого звука (лупы читают
        /// громкость один раз на старте) получают новое значение сразу, прямо в Play Mode.
        /// При активном «Соло» громкость заглушённого звука копится в сохранённой.
        /// </summary>
        private void ApplyVolumeToPlayingSources(string entityId, float volume)
        {
            if (!EditorApplication.isPlaying) return;

            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            AudioClip anyClip = null;
            foreach (var src in sources)
            {
                if (src == null || src.clip == null) continue;
                if (SoundIdOf(src.clip) != entityId) continue;
                anyClip = src.clip;
                if (src.isPlaying) src.volume = volume;
            }

            // Лупы игровых систем (мотор, колёса, эмбиенты) держат baseVolume и
            // перезаписывают громкость каждый кадр — их base правит проект по событию
            if (anyClip != null)
                AudioStudioExtensions.RaisePlayModeVolumeChanged(anyClip, volume);
        }

        /// <summary>Обратный поиск: клип → sound id из SoundLibrary (включая рандом-пулы).</summary>
        private string SoundIdOf(AudioClip clip)
        {
            if (_clipToSoundId == null)
            {
                _clipToSoundId = new Dictionary<AudioClip, string>();
                var sl = AudioVariantStore.SoundLibrary;
                if (sl != null)
                {
                    foreach (var e in sl.coreEntries)
                    {
                        if (e == null || string.IsNullOrEmpty(e.id)) continue;
                        if (e.clip != null) _clipToSoundId.TryAdd(e.clip, e.id);
                        if (e.clipVariants == null) continue;
                        foreach (var v in e.clipVariants)
                            if (v != null) _clipToSoundId.TryAdd(v, e.id);
                    }
                }
            }
            return clip != null && _clipToSoundId.TryGetValue(clip, out var id) ? id : null;
        }

        private static List<(string id, string name)> _serverVoices;

        /// <summary>Параметры TTS: голос из аккаунта ElevenLabs + подача. Шагов/CFG у TTS нет.</summary>
        private void AddTtsRows(AudioStylePreset style)
        {
            var options = new List<string> { "(голос не выбран)" };
            int current = 0;
            if (_serverVoices != null)
            {
                for (int i = 0; i < _serverVoices.Count; i++)
                {
                    options.Add(_serverVoices[i].name);
                    if (_serverVoices[i].id == style.voiceId) current = i + 1;
                }
                if (current == 0 && !string.IsNullOrEmpty(style.voiceId))
                {
                    options.Add(style.voiceId);   // голос из стиля, которого нет в списке
                    current = options.Count - 1;
                }
            }
            else if (!string.IsNullOrEmpty(style.voiceId))
            {
                options.Add(style.voiceId);
                current = 1;
            }

            var voiceDd = new DropdownField("Голос", options, current);
            voiceDd.RegisterValueChangedCallback(_ =>
            {
                int idx = options.IndexOf(voiceDd.value);
                if (idx <= 0) style.voiceId = "";
                else if (_serverVoices != null && idx - 1 < _serverVoices.Count)
                    style.voiceId = _serverVoices[idx - 1].id;
                EditorUtility.SetDirty(style);
            });
            _leftPanel.Add(voiceDd);
            if (_serverVoices == null) FetchVoices();

            var stability = new Slider("Стабильность", 0f, 1f) { value = style.ttsStability };
            stability.tooltip = "Ниже — эмоциональнее и разнообразнее, выше — ровнее";
            stability.RegisterValueChangedCallback(e =>
            {
                style.ttsStability = e.newValue;
                EditorUtility.SetDirty(style);
            });
            _leftPanel.Add(stability);

            var similarity = new Slider("Похожесть", 0f, 1f) { value = style.ttsSimilarity };
            similarity.tooltip = "Похожесть на исходный голос";
            similarity.RegisterValueChangedCallback(e =>
            {
                style.ttsSimilarity = e.newValue;
                EditorUtility.SetDirty(style);
            });
            _leftPanel.Add(similarity);

            // Дропдаун, а не TextField: текстовое поле перезатирало значение ассета
            // старым текстом при потере фокуса после внешних правок
            var models = new List<string>
            {
                "eleven_v3", "eleven_multilingual_v2", "eleven_turbo_v2_5", "eleven_flash_v2_5",
            };
            if (!models.Contains(style.ttsModelId) && !string.IsNullOrEmpty(style.ttsModelId))
                models.Add(style.ttsModelId);
            var model = new DropdownField("Модель TTS", models,
                Mathf.Max(0, models.IndexOf(style.ttsModelId)));
            model.tooltip = "eleven_v3 — лучшие интонации и русский, понимает [аудио-теги] в тексте";
            model.RegisterValueChangedCallback(e =>
            {
                style.ttsModelId = e.newValue;
                EditorUtility.SetDirty(style);
            });
            _leftPanel.Add(model);
        }

        private async void FetchVoices()
        {
            try { _serverVoices = await ElevenLabsClient.GetVoicesAsync(); }
            catch { _serverVoices = new List<(string, string)>(); }
            if (rootVisualElement != null)
            {
                RebuildLeftPanel();
                RebuildPicker();   // имена голосов на ячейках вариантов
            }
        }

        /// <summary>
        /// Короткое имя голоса по id: «Doc RU - Deep Dispatcher» → «Doc RU». Пока список
        /// не загружен — обрезанный id (после фетча пикер перерисуется с именами).
        /// </summary>
        private string VoiceDisplayName(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return null;
            if (_serverVoices == null)
            {
                FetchVoices();
                return voiceId.Substring(0, Mathf.Min(6, voiceId.Length)) + "…";
            }
            foreach (var v in _serverVoices)
            {
                if (v.id != voiceId) continue;
                int dash = v.name.IndexOf(" - ", StringComparison.Ordinal);
                return dash > 0 ? v.name.Substring(0, dash) : v.name;
            }
            return voiceId.Substring(0, Mathf.Min(6, voiceId.Length)) + "…";
        }

        private VisualElement MakeStyleField(string label, string value, Action<string> commit)
        {
            var host = new VisualElement();
            host.AddToClassList("style-field");
            host.Add(new Label(label));
            var field = new TextField { value = value ?? "", multiline = true };
            field.RegisterCallback<FocusOutEvent>(_ => commit(field.value));
            host.Add(field);
            return host;
        }

        private async void FetchModels()
        {
            try { _serverCheckpoints = await ComfyAudioClient.GetCheckpointsAsync(); }
            catch { _serverCheckpoints = new List<string>(); }
            if (rootVisualElement != null) RebuildLeftPanel();
        }

        private void AddComfyStatusRow()
        {
            var row = new VisualElement();
            row.AddToClassList("comfy-row");

            var dot = new Label("●");
            var status = new Label();
            var launch = new Button { text = "Запустить" };
            var refresh = new Button { text = "⟳", tooltip = "Проверить сейчас" };

            void Sync()
            {
                bool online = ComfyAudioServerControl.IsOnline;
                dot.style.color = online ? new Color(0.35f, 0.8f, 0.35f)
                    : ComfyAudioServerControl.Launching ? new Color(0.9f, 0.75f, 0.2f)
                    : new Color(0.85f, 0.3f, 0.3f);
                status.text = online ? "ComfyUI работает"
                    : ComfyAudioServerControl.Launching ? "ComfyUI стартует…" : "ComfyUI не запущен";
                launch.style.display = !online && ComfyAudioServerControl.CanLaunch
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            launch.clicked += () => ComfyAudioServerControl.Launch();
            refresh.clicked += ComfyAudioServerControl.CheckNow;

            ComfyAudioServerControl.Changed += Sync;
            row.RegisterCallback<DetachFromPanelEvent>(_ => ComfyAudioServerControl.Changed -= Sync);
            row.schedule.Execute(() => { ComfyAudioServerControl.Poll(3.0); Sync(); }).Every(3000);

            row.Add(dot);
            row.Add(status);
            row.Add(launch);
            row.Add(refresh);
            _leftPanel.Add(row);

            ComfyAudioServerControl.CheckNow();
            Sync();
        }

        // ── Витрина сущностей ──

        private void RebuildGrid()
        {
            if (_grid == null) return;
            _grid.Clear();

            foreach (var col in ActiveCollections())
            {
                var section = new VisualElement();
                section.AddToClassList("section");

                var head = new VisualElement();
                head.AddToClassList("section-head");
                head.Add(new Label(col.Title) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                if (!string.IsNullOrEmpty(col.UsageNote))
                {
                    var note = new Label(col.UsageNote);
                    note.AddToClassList("section-note");
                    head.Add(note);
                }
                head.Add(new VisualElement { style = { flexGrow = 1 } });

                var allBtn = new Button(() => EnqueueAll(col)) { text = $"Все ×{BatchCount}" };
                allBtn.AddToClassList("mini-btn");
                head.Add(allBtn);
                section.Add(head);

                foreach (var item in col.GetItems())
                {
                    if (!MatchesSearch(item)) continue;
                    section.Add(MakeRow(item, col));
                }

                _grid.Add(section);
            }
        }

        /// <summary>Скан использований по всем сущностям ВСЕХ вкладок (не только активной).</summary>
        private async void RunUsageScan()
        {
            if (SoundUsageScanner.IsScanning) return;

            var ids = new List<string>();
            foreach (var view in Views())
            {
                var config = ActiveConfig();
                var assets = config != null
                    ? config.CollectionsOfTab(view)
                    : view is AudioCollectionAsset c ? new List<AudioCollectionAsset> { c } : new();
                foreach (var asset in assets)
                {
                    var collection = AudioCollection.FromAsset(asset);
                    if (collection == null) continue;
                    foreach (var item in collection.GetItems())
                        ids.Add(item.EntityId);
                }
            }

            ShowNotification(new GUIContent("Сканирую использования…"));
            await SoundUsageScanner.ScanAsync(ids);
            RemoveNotification();
            RebuildToolbar();
            RebuildGrid();
        }

        /// <summary>
        /// Применить пак: для каждой сущности всех вкладок опубликовать записанный
        /// в паке вариант (SetActive = write-through клипа в SoundLibrary).
        /// </summary>
        private void ApplyPack(ProtoSystem.AudioGen.Editor.AudioPack pack)
        {
            if (pack == null) return;
            int applied = 0, missing = 0;

            foreach (var view in Views())
            {
                var config = ActiveConfig();
                var assets = config != null
                    ? config.CollectionsOfTab(view)
                    : view is AudioCollectionAsset c ? new List<AudioCollectionAsset> { c } : new();

                foreach (var asset in assets)
                {
                    var collection = AudioCollection.FromAsset(asset);
                    if (collection == null) continue;
                    foreach (var item in collection.GetItems())
                    {
                        string variantId = pack.VariantFor(item.EntityId);
                        if (string.IsNullOrEmpty(variantId)) continue;

                        var history = AudioVariantStore.Library.Find(item.EntityId);
                        var variant = history?.Find(variantId);
                        if (variant?.clip == null) { missing++; continue; }
                        if (history.activeVariantId == variantId) continue;

                        AudioVariantStore.SetActive(item.EntityId, variantId, item);
                        applied++;
                    }
                }
            }

            ShowNotification(new GUIContent(
                $"Пак «{pack.ResolveName()}»: применено {applied}" +
                (missing > 0 ? $", без файла {missing}" : "")));
            RebuildGrid();
            RebuildPicker();
        }

        private bool MatchesSearch(AudioItem item)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            var q = _searchText.Trim();
            return (item.EntityId != null && item.EntityId.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (item.DisplayName != null && item.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (item.UsageNote != null && item.UsageNote.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private VisualElement MakeRow(AudioItem item, AudioCollection col)
        {
            var row = new VisualElement { name = item.EntityId };
            row.AddToClassList("entity-row");
            if (item.EntityId == _selectedEntityId) row.AddToClassList("selected");

            // Подсветка «звучит прямо сейчас» (Play Mode; сет обновляет монитор слева)
            row.schedule.Execute(() =>
                row.EnableInClassList("playing", _playingSoundIds.Contains(item.EntityId))
            ).Every(400);

            var accent = new VisualElement();
            accent.AddToClassList("accent-strip");
            accent.style.backgroundColor = item.Accent;
            row.Add(accent);

            var clip = item.GetClip?.Invoke();

            var playBtn = new Button();
            playBtn.AddToClassList("play-btn");
            playBtn.SetEnabled(clip != null);
            BindPlayButton(playBtn, () => item.GetClip?.Invoke(), item.Loop);
            row.Add(playBtn);

            // Соло (как в DAW): в Play Mode слышны только отмеченные звуки
            var soloBtn = new Button(() =>
            {
                SoundSoloMode.Toggle(item.EntityId);
                RebuildToolbar();
                RebuildGrid();
            }) { text = "S" };
            soloBtn.AddToClassList("solo-btn");
            if (SoundSoloMode.IsSolo(item.EntityId)) soloBtn.AddToClassList("solo-on");
            soloBtn.tooltip = "Соло: глушить в игре всё, кроме отмеченных звуков";
            row.Add(soloBtn);

            var name = new Label(item.DisplayName);
            name.AddToClassList("entity-name");
            if (!string.IsNullOrEmpty(item.UsageNote)) name.tooltip = item.UsageNote;
            row.Add(name);

            if (item.Loop)
            {
                var loop = new Label("∞");
                loop.AddToClassList("loop-mark");
                loop.tooltip = "Луп";
                row.Add(loop);
            }

            var info = new Label(clip != null ? $"{clip.length:0.0}с" : "—");
            info.AddToClassList("clip-info");
            info.style.color = clip != null
                ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.85f, 0.45f, 0.3f);
            row.Add(info);

            bool hasPrompt = !string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke());
            var promptMark = new Label(hasPrompt ? "✎" : "✎?");
            promptMark.AddToClassList("prompt-mark");
            promptMark.tooltip = hasPrompt ? "Промпт задан" : "Промпта нет";
            promptMark.style.opacity = hasPrompt ? 0.8f : 0.35f;
            row.Add(promptMark);

            var history = AudioVariantStore.Library.Find(item.EntityId);
            int variants = history?.variants.Count ?? 0;
            var badge = new Label(variants > 0 ? $"{variants} вар." : "");
            badge.AddToClassList("variant-badge");
            row.Add(badge);

            // Использование в проекте (после скана «Исп.?»): ⚠ — ссылок нет, звук не прозвучит
            bool? used = SoundUsageScanner.IsUsed(item.EntityId);
            if (used.HasValue)
            {
                var usage = new Label(used.Value ? "✓" : "⚠ нигде");
                usage.AddToClassList(used.Value ? "usage-ok" : "usage-none");
                if (used.Value)
                {
                    var refs = SoundUsageScanner.UsagesOf(item.EntityId);
                    var shown = new List<string>();
                    for (int i = 0; i < refs.Count && i < 6; i++) shown.Add(refs[i]);
                    usage.tooltip = "Используется:\n" + string.Join("\n", shown) +
                        (refs.Count > 6 ? $"\n… и ещё {refs.Count - 6}" : "");
                    usage.RegisterCallback<ClickEvent>(e =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refs[0]);
                        if (asset != null) EditorGUIUtility.PingObject(asset);
                        e.StopPropagation();
                    });
                }
                else
                {
                    usage.tooltip = "Ни одной ссылки в профилях/схемах/префабах/сценах/коде — " +
                                    "в игре этот звук не триггерится";
                }
                row.Add(usage);
            }

            var gen = new Button(() => EnqueueBatch(item, col)) { text = $"+{BatchCount}" };
            gen.AddToClassList("mini-btn");
            row.Add(gen);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedEntityId = item.EntityId;
                _pickedVariantIds.Clear();
                _pickAnchorId = null;
                RebuildGrid();
                RebuildPicker();
            });

            return row;
        }

        /// <summary>
        /// Кнопка превью с живой иконкой ▶/■: пока клип звучит — показывает «стоп»
        /// (длинные эмбиенты иначе не остановить, не догадавшись кликнуть ▶ повторно).
        /// </summary>
        private static void BindPlayButton(Button btn, Func<AudioClip> getClip, bool loop)
        {
            void Sync()
            {
                var c = getClip();
                btn.text = c != null && AudioPreviewUtil.IsPlaying(c) ? "■" : "▶";
            }

            btn.clicked += () =>
            {
                var c = getClip();
                if (c == null) return;
                if (AudioPreviewUtil.IsPlaying(c)) AudioPreviewUtil.StopAll();
                else AudioPreviewUtil.Play(c, loop);
                Sync();
            };
            btn.schedule.Execute(Sync).Every(250);
            Sync();
        }

        // ── Пикер вариантов ──

        private void RebuildPicker()
        {
            if (_pickerHost == null) return;
            _pickerHost.Clear();

            var (col, item) = FindSelected();
            if (item == null)
            {
                var hint = new Label("Выберите звук в списке выше.");
                hint.AddToClassList("picker-hint");
                _pickerHost.Add(hint);
                return;
            }

            var head = new VisualElement();
            head.AddToClassList("picker-head");
            head.Add(new Label(item.DisplayName) { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // Описание «где звучит»: редактируемое, если провайдер разрешил (write-through)
            if (item.SetUsageNote != null)
            {
                var noteField = new TextField { value = item.UsageNote ?? "" };
                noteField.AddToClassList("usage-note-field");
                noteField.tooltip = "Где/когда звучит этот звук — описание для людей, хранится в конфиге";
                var placeholder = new Label("где звучит?");
                placeholder.AddToClassList("usage-note-placeholder");
                placeholder.pickingMode = PickingMode.Ignore;
                noteField.Add(placeholder);
                void SyncPlaceholder() =>
                    placeholder.style.display = string.IsNullOrEmpty(noteField.value)
                        ? DisplayStyle.Flex : DisplayStyle.None;
                noteField.RegisterValueChangedCallback(_ => SyncPlaceholder());
                SyncPlaceholder();
                noteField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Описание звука");
                    item.SetUsageNote(noteField.value);
                    if (item.UndoTarget != null)
                    {
                        EditorUtility.SetDirty(item.UndoTarget);
                        AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                    }
                });
                head.Add(noteField);
            }
            else if (!string.IsNullOrEmpty(item.UsageNote))
            {
                var note = new Label(item.UsageNote);
                note.AddToClassList("section-note");
                head.Add(note);
            }
            head.Add(new VisualElement { style = { flexGrow = 1 } });

            // Громкость записи SoundLibrary — не бегая в инспектор библиотеки.
            // Слайдер в процентах + числовое поле (точный ввод), применяется сразу.
            if (item.GetVolume != null && item.SetVolume != null && item.GetClip?.Invoke() != null)
            {
                void Commit(float percent)
                {
                    float v = Mathf.Clamp(percent, 0f, 100f) / 100f;
                    item.SetVolume(v);
                    ApplyVolumeToPlayingSources(item.EntityId, v);
                }

                var vol = new Slider("Громк.", 0f, 100f) { value = item.GetVolume() * 100f };
                vol.AddToClassList("entry-volume");
                vol.tooltip = "Громкость записи в SoundLibrary, % (микс в игре, применяется сразу)";

                var volField = new IntegerField { value = Mathf.RoundToInt(item.GetVolume() * 100f) };
                volField.AddToClassList("entry-volume-num");

                vol.RegisterValueChangedCallback(e =>
                {
                    Commit(e.newValue);
                    volField.SetValueWithoutNotify(Mathf.RoundToInt(e.newValue));
                });
                volField.RegisterValueChangedCallback(e =>
                {
                    int clamped = Mathf.Clamp(e.newValue, 0, 100);
                    Commit(clamped);
                    vol.SetValueWithoutNotify(clamped);
                });
                void SaveIfDirty()
                {
                    if (item.UndoTarget != null) AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                }
                vol.RegisterCallback<FocusOutEvent>(_ => SaveIfDirty());
                volField.RegisterCallback<FocusOutEvent>(_ => SaveIfDirty());

                head.Add(vol);
                head.Add(volField);
                head.Add(new Label("%") { pickingMode = PickingMode.Ignore });
            }

            // Аудиошина записи: красная подсветка, когда шина расходится с шиной группы —
            // группа должна регулироваться одной шиной, иначе звуку место в другом сете
            if (item.GetBus != null && item.SetBus != null)
            {
                var mismatchTint = new Color(0.55f, 0.15f, 0.15f, 0.9f);
                var busField = new EnumField(item.GetBus());
                busField.AddToClassList("entry-bus");
                void SyncBusTint()
                {
                    bool mismatch = col?.DefaultBus != null &&
                                    item.GetBus() != col.DefaultBus.Value;
                    busField.style.backgroundColor = mismatch ? mismatchTint : (StyleColor)StyleKeyword.Null;
                    busField.tooltip = mismatch
                        ? $"Шина записи не совпадает с шиной группы ({col.DefaultBus.Value}) — это странно: " +
                          "группа регулируется одной шиной, иначе звуку место в другом сете."
                        : "Аудиошина записи (SoundCategory в SoundLibrary) — каким ползунком громкости её регулирует игрок";
                }
                busField.RegisterValueChangedCallback(e =>
                {
                    if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Шина звука");
                    item.SetBus((ProtoSystem.Sound.SoundCategory)e.newValue);
                    if (item.UndoTarget != null)
                    {
                        EditorUtility.SetDirty(item.UndoTarget);
                        AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                    }
                    SyncBusTint();
                });
                SyncBusTint();
                head.Add(busField);
            }

            // Полное отключение звука: надоевший звук глушится, не удаляя запись и клип
            if (item.GetMuted != null && item.SetMuted != null)
            {
                var mutedTint = new Color(0.55f, 0.15f, 0.15f, 0.9f);
                var muteBtn = new Button();
                muteBtn.AddToClassList("mini-btn");
                void SyncMute()
                {
                    bool m = item.GetMuted();
                    muteBtn.text = m ? "🔇 выкл" : "🔊";
                    muteBtn.style.backgroundColor = m ? mutedTint : (StyleColor)StyleKeyword.Null;
                    muteBtn.tooltip = m
                        ? "Звук отключён — не проигрывается нигде (SoundEntry.muted). Нажмите, чтобы включить."
                        : "Отключить звук полностью — запись и клип остаются, но нигде не проигрываются";
                }
                muteBtn.clicked += () =>
                {
                    if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Мьют звука");
                    item.SetMuted(!item.GetMuted());
                    if (item.UndoTarget != null)
                    {
                        EditorUtility.SetDirty(item.UndoTarget);
                        AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                    }
                    SyncMute();
                };
                SyncMute();
                head.Add(muteBtn);
            }

            var genBtn = new Button(() => EnqueueBatch(item, col)) { text = $"+{BatchCount} ещё" };
            head.Add(genBtn);

            if (item.SetClipVariants != null)
            {
                var poolBtn = new Button(() =>
                {
                    AudioVariantStore.PublishClipVariants(item, _pickedVariantIds.ToList());
                    ShowNotification(new GUIContent($"В рандом-пул: {_pickedVariantIds.Count}"));
                })
                {
                    text = _pickedVariantIds.Count > 0
                        ? $"Выбранные ({_pickedVariantIds.Count}) → рандом-пул"
                        : "Выбранные → рандом-пул",
                    tooltip = "Ctrl+клик — по одному, Shift+клик — диапазон, затем сюда",
                };
                head.Add(poolBtn);
            }

            _pickerHost.Add(head);

            // Промпт сущности — write-through в провайдера
            var promptField = new TextField("Промпт") { value = item.GetPrompt?.Invoke() ?? "", multiline = true };
            promptField.AddToClassList("prompt-field");

            // Для TTS-наборов: ввод «[» открывает список аудио-тегов эмоций + явная кнопка
            var colStyle = col?.SourceAsset != null && col.SourceAsset.profile != null
                ? col.SourceAsset.profile.style : null;
            if (colStyle != null && colStyle.engine == AudioEngine.ElevenLabsTts)
            {
                AttachTagAutocomplete(promptField);

                var tagBtn = new Button(() => ShowTagMenu(promptField)) { text = "[тег]" };
                tagBtn.AddToClassList("mini-btn");
                tagBtn.tooltip = "Вставить аудио-тег эмоции (или просто введите «[» в тексте)";
                head.Add(tagBtn);
            }
            promptField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (item.UndoTarget != null) Undo.RecordObject(item.UndoTarget, "Промпт звука");
                item.SetPrompt?.Invoke(promptField.value);
                if (item.UndoTarget != null)
                {
                    EditorUtility.SetDirty(item.UndoTarget);
                    AssetDatabase.SaveAssetIfDirty(item.UndoTarget);
                }
            });
            _pickerHost.Add(promptField);

            var trimEditor = BuildTrimEditor(item, col);
            if (trimEditor != null) _pickerHost.Add(trimEditor);

            var history = AudioVariantStore.Library.Find(item.EntityId);
            if (history == null || history.variants.Count == 0)
            {
                var empty = new Label("Вариантов пока нет — жмите «+N ещё».");
                empty.AddToClassList("picker-hint");
                _pickerHost.Add(empty);
                return;
            }

            var lane = new ScrollView(ScrollViewMode.Horizontal) { name = "variants-lane" };
            lane.AddToClassList("variants-lane");
            // Delete — удалить выбранные (с подтверждением), Esc — снять выделение.
            // Ячейки focusable: клик фокусирует ячейку, KeyDown всплывает до ленты.
            lane.focusable = true;
            lane.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Delete && _pickedVariantIds.Count > 0)
                {
                    DeleteVariantsWithConfirm(item, _pickedVariantIds.ToList());
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape && _pickedVariantIds.Count > 0)
                {
                    _pickedVariantIds.Clear();
                    _pickAnchorId = null;
                    RebuildPicker();
                    e.StopPropagation();
                }
            });
            if (_pickedVariantIds.Count > 0)
                lane.schedule.Execute(() =>
                {
                    // после пересборки вернуть фокус, чтобы Delete работал сразу
                    var focusTarget = lane.Q(className: "picked");
                    (focusTarget ?? (VisualElement)lane).Focus();
                });
            // Порядок показа (новые слева) — он же порядок Shift-диапазона
            var ordered = new List<AudioVariant>();
            for (int i = history.variants.Count - 1; i >= 0; i--)
                ordered.Add(history.variants[i]);
            for (int i = 0; i < ordered.Count; i++)
                lane.Add(MakeVariantCell(history, ordered, i, item, col));
            _pickerHost.Add(lane);
        }

        private VisualElement MakeVariantCell(
            ProtoSystem.AudioGen.Editor.EntityAudioHistory history, List<AudioVariant> ordered,
            int index, AudioItem item, AudioCollection col)
        {
            var variant = ordered[index];
            var cell = new VisualElement();
            cell.AddToClassList("variant-cell");
            cell.focusable = true;
            bool isActive = history.activeVariantId == variant.id;
            if (isActive) cell.AddToClassList("active");
            if (variant.bad) cell.AddToClassList("bad");
            if (_pickedVariantIds.Contains(variant.id)) cell.AddToClassList("picked");

            var wave = new Image { scaleMode = ScaleMode.StretchToFill };
            wave.AddToClassList("waveform");
            if (variant.clip != null)
            {
                var tex = AudioPreviewUtil.Waveform(variant.clip);
                if (tex != null) wave.image = tex;
                else
                {
                    // превью грузится асинхронно — добираем несколько секунд
                    int attempts = 0;
                    wave.schedule.Execute(() =>
                    {
                        var t = AudioPreviewUtil.Waveform(variant.clip);
                        if (t != null) wave.image = t;
                    }).Every(400).Until(() => wave.image != null || ++attempts > 12);
                }
            }
            cell.Add(wave);

            var meta = new VisualElement();
            meta.AddToClassList("variant-meta");

            var playBtn = new Button();
            playBtn.AddToClassList("play-btn");
            playBtn.SetEnabled(variant.clip != null);
            BindPlayButton(playBtn, () => variant.clip, item.Loop);
            meta.Add(playBtn);

            bool isOriginal = variant.recipe == null;
            string label = variant.clip != null ? $"{variant.clip.length:0.0}с" : "файл удалён";
            if (isOriginal) label += " · база";
            if (isActive) label = "★ " + label;
            var info = new Label(label);
            info.AddToClassList("variant-info");
            meta.Add(info);

            // TTS-вариант: показать, ЧЕЙ это голос — иначе варианты не различить
            string voiceName = isOriginal ? null : VoiceDisplayName(variant.recipe.voiceId);
            if (voiceName == null && !isOriginal &&
                variant.recipe.ttsModelId != null && variant.recipe.ttsModelId.StartsWith("qwen"))
                voiceName = "Qwen";
            if (!string.IsNullOrEmpty(voiceName))
            {
                var voiceLbl = new Label(voiceName);
                voiceLbl.AddToClassList("variant-voice");
                meta.Add(voiceLbl);
            }

            cell.Add(meta);
            cell.tooltip = isOriginal
                ? "Базовый вариант — звук, который был в игре до студии"
                : $"{variant.recipe.styleName} · seed {variant.recipe.seed} · {variant.recipe.generatedAtUtc}" +
                  (string.IsNullOrEmpty(voiceName) ? "" : $"\nГолос: {voiceName} ({variant.recipe.ttsModelId})");

            // Выделение — на PointerDown, а не ClickEvent: у ClickEvent модификаторы
            // в редакторе доезжают ненадёжно, плюс отклик мгновенный, как в списках Unity.
            cell.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;   // правая кнопка — контекстное меню

                if (e.clickCount >= 2)
                {
                    AudioVariantStore.SetActive(item.EntityId, variant.id, item);
                    return;
                }

                if (e.shiftKey && !string.IsNullOrEmpty(_pickAnchorId))
                {
                    int anchor = ordered.FindIndex(v => v.id == _pickAnchorId);
                    if (anchor < 0) anchor = index;
                    if (!e.ctrlKey && !e.commandKey) _pickedVariantIds.Clear();
                    for (int i = Mathf.Min(anchor, index); i <= Mathf.Max(anchor, index); i++)
                        _pickedVariantIds.Add(ordered[i].id);
                    // якорь не двигаем — можно тянуть диапазон в обе стороны
                }
                else if (e.ctrlKey || e.commandKey)
                {
                    if (!_pickedVariantIds.Remove(variant.id)) _pickedVariantIds.Add(variant.id);
                    _pickAnchorId = variant.id;
                }
                else
                {
                    _pickedVariantIds.Clear();
                    _pickedVariantIds.Add(variant.id);
                    _pickAnchorId = variant.id;
                }
                RebuildPicker();
            });

            cell.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Сделать активным",
                    _ => AudioVariantStore.SetActive(item.EntityId, variant.id, item),
                    variant.clip != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(variant.bad ? "Снять пометку «плохой»" : "Пометить плохим",
                    _ => AudioVariantStore.MarkBad(item.EntityId, variant.id, !variant.bad));
                evt.menu.AppendAction("Перегенерировать по рецепту",
                    _ => RestoreVariant(item, col, variant),
                    variant.recipe != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Показать WAV в проекте", _ =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<AudioClip>(variant.wavAssetPath);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }, variant.clip != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                if (_pickedVariantIds.Count > 1 && _pickedVariantIds.Contains(variant.id))
                    evt.menu.AppendAction($"Удалить выбранные ({_pickedVariantIds.Count})",
                        _ => DeleteVariantsWithConfirm(item, _pickedVariantIds.ToList()));
                else
                    evt.menu.AppendAction("Удалить вариант",
                        _ => DeleteVariantsWithConfirm(item, new List<string> { variant.id }));
                evt.menu.AppendAction("Чистка: удалить файлы кроме активного",
                    _ => ShowNotification(new GUIContent(
                        $"Удалено файлов: {AudioVariantStore.DeleteFiles(item.EntityId)}")));
                evt.menu.AppendAction("Чистка: выкинуть рецепты без файлов",
                    _ => ShowNotification(new GUIContent(
                        $"Удалено рецептов: {AudioVariantStore.DeleteRecipes(item.EntityId)}")));
            }));

            return cell;
        }

        /// <summary>Удаление вариантов — всегда через модальное подтверждение.</summary>
        private void DeleteVariantsWithConfirm(AudioItem item, List<string> variantIds)
        {
            if (variantIds.Count == 0) return;

            string what = variantIds.Count == 1
                ? "Удалить вариант?"
                : $"Удалить выбранные варианты: {variantIds.Count} шт.?";
            if (!EditorUtility.DisplayDialog("Удаление вариантов",
                    what + "\n\nWAV-файлы студии будут удалены с диска, рецепты пропадут.\n" +
                    "Базовые варианты и оригинальные файлы игры не трогаются.",
                    "Удалить", "Отмена"))
                return;

            int removed = AudioVariantStore.DeleteVariants(item.EntityId, variantIds);
            _pickedVariantIds.Clear();
            _pickAnchorId = null;
            ShowNotification(new GUIContent($"Удалено: {removed}"));
        }

        // ── Аудио-теги эмоций (eleven_v3): подсказка по вводу «[» ──

        private static readonly string[] TtsEmotionTags =
        {
            "urgently", "shouting", "whispers", "quietly", "calmly", "seriously",
            "tense", "nervously", "worried", "angry", "sarcastic", "deadpan",
            "cheerfully", "excited", "pleased", "relieved", "hopeful", "curious",
            "tired", "sadly", "sighs", "laughs", "chuckles", "gasps", "exhales",
            "crying", "disgusted", "pause", "hesitates", "clears throat",
        };

        /// <summary>
        /// Появление «[» в тексте (любым способом: клавиша, вставка, смена раскладки) —
        /// выпадающий список тегов. KeyDown не используем: его глотает внутренний TextInput,
        /// а на русской раскладке клавиши «[» вовсе нет.
        /// </summary>
        private static void AttachTagAutocomplete(TextField field)
        {
            field.RegisterValueChangedCallback(e =>
            {
                int before = CountChar(e.previousValue, '[');
                int after = CountChar(e.newValue, '[');
                if (after <= before) return;

                // курсор должен встать сразу за введённой «[»
                field.schedule.Execute(() =>
                {
                    string v = field.value ?? "";
                    int cursor = Mathf.Clamp(field.cursorIndex, 0, v.Length);
                    if (cursor > 0 && v[cursor - 1] == '[')
                        ShowTagMenu(field);
                }).StartingIn(20);
            });
        }

        private static int CountChar(string s, char c)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            foreach (var ch in s)
                if (ch == c) n++;
            return n;
        }

        private static void ShowTagMenu(TextField field)
        {
            // IMGUI GenericMenu, а не GenericDropdownMenu: тот закрывался мгновенно —
            // поле ввода возвращало себе фокус и меню считало это «кликом мимо».
            int cursorAtOpen = field.cursorIndex;
            var menu = new UnityEditor.GenericMenu();
            foreach (var tag in TtsEmotionTags)
            {
                string t = tag;
                menu.AddItem(new GUIContent(t), false, () =>
                {
                    string v = field.value ?? "";
                    int cursor = Mathf.Clamp(cursorAtOpen, 0, v.Length);
                    // «[» уже есть перед курсором — дописать остаток; нет — вставить целиком
                    string insert = cursor > 0 && v[cursor - 1] == '[' ? t + "] " : "[" + t + "] ";
                    field.value = v.Insert(cursor, insert);
                    int newPos = cursor + insert.Length;
                    field.cursorIndex = newPos;
                    field.selectIndex = newPos;
                    field.schedule.Execute(field.Focus).StartingIn(10);
                });
            }
            var wb = field.worldBound;
            menu.DropDown(new Rect(wb.xMin, wb.yMax, 0, 0));
        }

        // ── Генерация ──

        private async void EnqueueBatch(AudioItem item, AudioCollection col)
        {
            if (string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke()))
            {
                ShowNotification(new GUIContent("Сначала напишите промпт"));
                return;
            }
            if (!await EnsureOnline()) return;

            string runId = AudioVariantStore.NewVariantId();
            var list = new List<AudioGenRequest>();
            for (int i = 0; i < BatchCount; i++)
            {
                var req = AudioVariantStore.BuildRequest(item, col, runId);
                req.Seed = UnityEngine.Random.Range(1, 100000000);
                list.Add(req);
            }
            AiAudioGenerator.Enqueue(list);
        }

        private async void EnqueueAll(AudioCollection only = null, bool onlyMissing = false)
        {
            if (!await EnsureOnline()) return;

            string runId = AudioVariantStore.NewVariantId();
            var list = new List<AudioGenRequest>();
            var cols = only != null ? new List<AudioCollection> { only } : ActiveCollections();
            foreach (var col in cols)
            {
                foreach (var item in col.GetItems())
                {
                    if (onlyMissing && item.GetClip?.Invoke() != null) continue;
                    if (string.IsNullOrWhiteSpace(item.GetPrompt?.Invoke())) continue;
                    for (int i = 0; i < BatchCount; i++)
                    {
                        var req = AudioVariantStore.BuildRequest(item, col, runId);
                        req.Seed = UnityEngine.Random.Range(1, 100000000);
                        list.Add(req);
                    }
                }
            }

            if (list.Count == 0)
            {
                ShowNotification(new GUIContent("Нечего генерировать (нет промптов)"));
                return;
            }
            AiAudioGenerator.Enqueue(list);
        }

        private void RestoreVariant(AudioItem item, AudioCollection col, AudioVariant variant)
        {
            string variantId = AudioVariantStore.NewVariantId();
            string path = AudioVariantStore.VariantWavPath(col.WavFolder, item.EntityId, variantId);
            var direct = AudioVariantStore.RecipeToRequest(variant.recipe);

            AiAudioGenerator.Enqueue(new AudioGenRequest
            {
                Prompt = variant.recipe.subject,
                AssetPath = path,
                DisplayName = item.DisplayName + " (рецепт)",
                Loop = item.Loop,
                DirectRequest = direct,
                RunId = variant.recipe.runId,
                UndoTarget = item.UndoTarget,
                OnDone = (clip, result) =>
                {
                    bool hadClip = item.GetClip?.Invoke() != null;
                    AudioVariantStore.AddVariant(item.EntityId, variantId, clip, result, makeActive: !hadClip);
                    if (!hadClip) item.SetClip?.Invoke(clip);
                },
            });
        }

        /// <summary>
        /// Готов ли движок активной вкладки: облаку нужен ключ, Qwen — exe и модели,
        /// ComfyUI-движкам — сервер.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> EnsureOnline()
        {
            bool needsComfy = false, needsCloud = false, needsQwen = false;
            foreach (var c in ActiveCollections())
            {
                var s = c.SourceAsset != null && c.SourceAsset.profile != null
                    ? c.SourceAsset.profile.style : AudioStyleRegistry.Default;
                var engine = s != null ? s.engine : AudioEngine.StableAudio;
                if (AiAudioGenerator.IsCloud(engine)) needsCloud = true;
                else if (engine == AudioEngine.QwenTts) needsQwen = true;
                else needsComfy = true;
            }

            if (needsCloud && !ElevenLabsClient.HasKey)
            {
                EditorUtility.DisplayDialog("ElevenLabs",
                    "Не задан API-ключ. Укажите его в Project Settings ▸ AI Audio Tools.", "Ок");
                return false;
            }

            if (needsQwen && !QwenTtsClient.IsAvailable)
            {
                EditorUtility.DisplayDialog("Qwen3-TTS",
                    "Не найден qwen-tts.exe или GGUF-модели. Пути — в Project Settings ▸ AI Audio Tools.", "Ок");
                return false;
            }

            if (needsComfy && !await ComfyAudioClient.IsOnlineAsync())
            {
                bool launch = EditorUtility.DisplayDialog("ComfyUI не отвечает",
                    "Сервер ComfyUI не запущен. Запустить?",
                    ComfyAudioServerControl.CanLaunch ? "Запустить" : "Ок", "Отмена");
                if (launch && ComfyAudioServerControl.CanLaunch) ComfyAudioServerControl.Launch();
                return false;
            }
            return true;
        }

        // ── Мини-редактор обрезки: фрагмент активного клипа → новый вариант ──

        private static bool _trimFoldOpen;

        /// <summary>
        /// «✂ Обрезка»: вейвформа активного клипа, маркеры начала/конца (затемнение
        /// отрезаемых краёв), предпрослушивание фрагмента и сохранение фрагмента новым
        /// вариантом (WAV пишется сэмплами, анти-щелчок — 3 мс фейды на краях).
        /// </summary>
        private VisualElement BuildTrimEditor(AudioItem item, AudioCollection col)
        {
            var clip = item.GetClip?.Invoke();
            if (clip == null || col == null) return null;

            var fold = new Foldout { text = "✂ Обрезка", value = _trimFoldOpen };
            fold.AddToClassList("trim-editor");
            fold.RegisterValueChangedCallback(e => { if (e.target == fold) _trimFoldOpen = e.newValue; });

            float len = Mathf.Max(clip.length, 0.001f);
            float selStart = 0f, selEnd = len;

            // Вейвформа + затемнение отрезаемых краёв
            var wave = new Image { scaleMode = ScaleMode.StretchToFill };
            wave.style.height = 56;
            wave.image = AudioPreviewUtil.Waveform(clip);
            if (wave.image == null)   // AssetPreview прогружается асинхронно
                wave.schedule.Execute(() => wave.image = AudioPreviewUtil.Waveform(clip))
                    .Every(200).Until(() => wave.image != null);

            var waveWrap = new VisualElement();
            waveWrap.style.position = Position.Relative;
            waveWrap.Add(wave);
            var shadeL = new VisualElement();
            var shadeR = new VisualElement();
            foreach (var s in new[] { shadeL, shadeR })
            {
                s.style.position = Position.Absolute;
                s.style.top = 0;
                s.style.bottom = 0;
                s.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
                s.pickingMode = PickingMode.Ignore;
                waveWrap.Add(s);
            }
            shadeL.style.left = 0;
            shadeR.style.right = 0;
            fold.Add(waveWrap);

            var range = new MinMaxSlider("Фрагмент", 0f, len, 0f, len);
            var startField = new FloatField("нач., с") { value = 0f };
            var endField = new FloatField("кон., с") { value = len };
            startField.AddToClassList("trim-num");
            endField.AddToClassList("trim-num");

            void SyncVisual()
            {
                shadeL.style.width = Length.Percent(Mathf.Clamp01(selStart / len) * 100f);
                shadeR.style.width = Length.Percent(Mathf.Clamp01(1f - selEnd / len) * 100f);
            }
            void SetSelection(float s, float e, bool toSlider, bool toFields)
            {
                selStart = Mathf.Clamp(s, 0f, len);
                selEnd = Mathf.Clamp(e, selStart + 0.01f, len);
                if (toSlider) range.SetValueWithoutNotify(new Vector2(selStart, selEnd));
                if (toFields)
                {
                    startField.SetValueWithoutNotify(Mathf.Round(selStart * 1000f) / 1000f);
                    endField.SetValueWithoutNotify(Mathf.Round(selEnd * 1000f) / 1000f);
                }
                SyncVisual();
            }
            range.RegisterValueChangedCallback(e => SetSelection(e.newValue.x, e.newValue.y, false, true));
            startField.RegisterValueChangedCallback(e => SetSelection(e.newValue, selEnd, true, true));
            endField.RegisterValueChangedCallback(e => SetSelection(selStart, e.newValue, true, true));
            SyncVisual();

            var playBtn = new Button(() =>
            {
                var seg = ExtractSegmentClip(clip, selStart, selEnd);
                if (seg == null)
                {
                    EditorUtility.DisplayDialog("Обрезка",
                        "Не удалось прочитать сэмплы клипа (сжатый формат импорта?).", "Ок");
                    return;
                }
                AudioPreviewUtil.Play(seg);
            }) { text = "▶ фрагмент", tooltip = "Прослушать выделенный фрагмент" };

            var stopBtn = new Button(AudioPreviewUtil.StopAll) { text = "■" };

            var saveBtn = new Button(() => SaveTrimmedVariant(item, col, clip, selStart, selEnd))
            {
                text = "Фрагмент → вариант",
                tooltip = "Сохранить фрагмент новым вариантом (WAV), сделать активным и опубликовать",
            };

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.Add(range);
            range.style.flexGrow = 1;
            row.Add(startField);
            row.Add(endField);
            row.Add(playBtn);
            row.Add(stopBtn);
            row.Add(saveBtn);
            fold.Add(row);
            return fold;
        }

        /// <summary>Сэмплы фрагмента [start..end] с 3 мс фейдами на краях. null — GetData не удался.</summary>
        private static float[] ExtractSegment(AudioClip clip, float start, float end,
                                              out int channels, out int freq)
        {
            channels = clip.channels;
            freq = clip.frequency;
            int total = clip.samples;
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * freq), 0, total - 1);
            int s1 = Mathf.Clamp(Mathf.RoundToInt(end * freq), s0 + 1, total);

            var all = new float[total * channels];
            if (!clip.GetData(all, 0)) return null;

            int frames = s1 - s0;
            var seg = new float[frames * channels];
            System.Array.Copy(all, s0 * channels, seg, 0, seg.Length);

            int fade = Mathf.Min(Mathf.RoundToInt(0.003f * freq), frames / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                for (int c = 0; c < channels; c++)
                {
                    seg[i * channels + c] *= k;
                    seg[(frames - 1 - i) * channels + c] *= k;
                }
            }
            return seg;
        }

        private static AudioClip ExtractSegmentClip(AudioClip clip, float start, float end)
        {
            var samples = ExtractSegment(clip, start, end, out int channels, out int freq);
            if (samples == null) return null;
            var seg = AudioClip.Create("trim_preview", samples.Length / channels, channels, freq, false);
            seg.SetData(samples, 0);
            return seg;
        }

        private void SaveTrimmedVariant(AudioItem item, AudioCollection col, AudioClip clip,
                                        float start, float end)
        {
            var samples = ExtractSegment(clip, start, end, out int channels, out int freq);
            if (samples == null)
            {
                EditorUtility.DisplayDialog("Обрезка",
                    "Не удалось прочитать сэмплы клипа (сжатый формат импорта?).", "Ок");
                return;
            }

            string folder = ($"{col.WavFolder}/{item.EntityId}").Replace('\\', '/');
            System.IO.Directory.CreateDirectory(folder);
            string variantId = "trim" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            string path = $"{folder}/Snd_{item.EntityId}_{variantId}.wav";

            WriteWavPcm16(path, samples, channels, freq);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AiAudioGenerator.ConfigureAsClip(path);
            var newClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (newClip == null)
            {
                EditorUtility.DisplayDialog("Обрезка", $"WAV записан, но клип не загрузился: {path}", "Ок");
                return;
            }

            AudioVariantStore.AddTrimmedVariant(item.EntityId, variantId, newClip, path);
            AudioVariantStore.SetActive(item.EntityId, variantId, item);
            ShowNotification(new GUIContent($"Фрагмент {start:0.###}–{end:0.###} с → вариант"));
            RebuildPicker();
        }

        private static void WriteWavPcm16(string path, float[] samples, int channels, int freq)
        {
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
            using var w = new System.IO.BinaryWriter(fs);
            int dataLen = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);              // PCM
            w.Write((short)channels);
            w.Write(freq);
            w.Write(freq * channels * 2);   // byte rate
            w.Write((short)(channels * 2)); // block align
            w.Write((short)16);             // bits per sample
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (var f in samples)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(f, -1f, 1f) * 32767f));
        }
    }
}
