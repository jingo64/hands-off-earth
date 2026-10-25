using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HandsOffEarth.Priorities;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HandsOffEarth.UI
{
    // The "Priority rules" window: an ordered list of rules (preset + conditions), and a preview of
    // which rule a nation matches right now
    public static class RulesWindow
    {
        static readonly Color RuleColor = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color MatchedRuleColor = new Color(0.39f, 0.78f, 0.84f, 0.22f);

        static GameObject root;
        static Transform ruleList;
        static ScrollRect scroll;
        static TMP_Dropdown nationDropdown;
        static TMP_Text statsLeft, statsRight, resultText;
        static GameObject textTemplate, dropdownTemplate, buttonTemplate, inputTemplate;
        static readonly List<Image> ruleBackgrounds = new List<Image>();
        static List<TINationState> previewNations = new List<TINationState>();

        static TIFactionState Player => GameControl.control.activePlayer;
        static List<Rule> Rules => PriorityRules.current.rules;

        public static void Open(NationInfoController controller, TINationState nation)
        {
            try
            {
                // The templates belong to the nation screen; rebuild if a scene change destroyed them
                if (root != null && textTemplate == null)
                    Object.Destroy(root);
                if ((root == null || textTemplate == null) && !Build(controller))
                    return;
                PopulateNations(nation);
                RebuildRules();
                root.SetActive(true);
            }
            catch (Exception e)
            {
                Main.Error("Couldn't open the priority rules window: " + e);
            }
        }

        static void Close()
        {
            PriorityRules.Save();
            if (root != null)
                root.SetActive(false);
            TIInputManager.RestoreKeybindings();
            // Rule edits take effect straight away rather than at the next monthly check
            RuleApplier.ApplyAll(Player);
            PrioritiesTab.Redraw();
        }

        static bool Build(NationInfoController c)
        {
            textTemplate = c.priorityHeader1.gameObject;
            dropdownTemplate = c.priorityPresetDropdown.gameObject;
            buttonTemplate = c.designPresetPanelButtonText.transform.parent.gameObject;
            inputTemplate = c.inputPresetName.gameObject;

            // Own overlay canvas, scaled like the game's UI
            root = new GameObject("Priority Rules Window", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below 30000, which is where TMP_Dropdown puts its open list
            canvas.sortingOrder = 29000;
            var gameCanvas = c.GetComponentInParent<Canvas>();
            var gameScaler = gameCanvas != null ? gameCanvas.rootCanvas.GetComponent<CanvasScaler>() : null;
            var scaler = root.GetComponent<CanvasScaler>();
            if (gameScaler != null)
            {
                scaler.uiScaleMode = gameScaler.uiScaleMode;
                scaler.referenceResolution = gameScaler.referenceResolution;
                scaler.screenMatchMode = gameScaler.screenMatchMode;
                scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
                scaler.scaleFactor = gameScaler.scaleFactor;
            }

            var blocker = UiKit.NewUIObject("Blocker", root.transform);
            UiKit.Stretch(blocker);
            blocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panel = UiKit.NewUIObject("Panel", root.transform);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 700f);
            var panelImage = panel.AddComponent<Image>();
            var work = c.designPresetPanel != null ? c.designPresetPanel.transform.Find("Work Panel") : null;
            var background = work != null ? work.GetComponent<Image>() : null;
            if (background != null)
                UiKit.CopyImage(background, panelImage);
            else
                panelImage.color = new Color(0.05f, 0.08f, 0.1f, 0.97f);
            var column = UiKit.Column(panel, 14, 8);
            column.padding.bottom = 22;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = UiKit.CloneText(c.presetBuilderHeaderText.gameObject, panel.transform, "Title", "PRIORITY RULES");
            title.alignment = TextAlignmentOptions.Center;
            UiKit.Layout(title.gameObject, preferredHeight: 28);
            Hint(panel.transform, "Nations set to use priority rules get the preset of the first rule, top to bottom, whose conditions all hold. " +
                                  "A rule with no conditions always matches, so it works as a catch-all at the bottom. If nothing matches, priorities are left alone.", 52);

            // Scrolling rule list
            var viewport = UiKit.NewUIObject("Rules", panel.transform);
            UiKit.Layout(viewport, preferredHeight: 340);
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            // A stencil Mask, as the game's own lists use; RectMask2D didn't clip the game's text
            viewport.AddComponent<Mask>().showMaskGraphic = true;
            var content = UiKit.NewUIObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            // Leave room on the right for the scrollbar
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new Vector2(-16f, 0f);
            UiKit.Column(content, 6, 6);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            AddScrollbar(c, viewport.transform);
            ruleList = content.transform;

            var addRow = UiKit.Row(panel.transform, "Add Rule Row", 32);
            Button(addRow.transform, "Add rule", 160, "GenericSelect", () =>
            {
                Rules.Add(new Rule { preset = PriorityRules.PresetNames(Player).FirstOrDefault() ?? "" });
                Changed(rebuild: true);
                scroll.verticalNormalizedPosition = 0f;
            });

            // Preview
            Heading(panel.transform, "Preview");
            Hint(panel.transform, "A nation's stats today, and the rule it would get.", 18);
            nationDropdown = Dropdown(panel.transform, "Preview Nation", -1);
            nationDropdown.onValueChanged.AddListener(_ =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ListLineSelect");
                RefreshPreview();
            });
            var stats = UiKit.Row(panel.transform, "Stats", 132, 20);
            statsLeft = UiKit.CloneText(textTemplate, stats.transform, "Stats Left", "", wrap: true);
            statsLeft.alignment = TextAlignmentOptions.TopLeft;
            UiKit.Layout(statsLeft.gameObject, flexibleWidth: 1, preferredHeight: 132);
            statsRight = UiKit.CloneText(textTemplate, stats.transform, "Stats Right", "", wrap: true);
            statsRight.alignment = TextAlignmentOptions.TopLeft;
            UiKit.Layout(statsRight.gameObject, flexibleWidth: 1, preferredHeight: 132);
            resultText = UiKit.CloneText(textTemplate, panel.transform, "Result", "", wrap: true);
            resultText.fontStyle |= FontStyles.Bold;
            UiKit.Layout(resultText.gameObject, preferredHeight: 24);

            var footer = UiKit.Row(panel.transform, "Buttons", 40, 10);
            footer.childAlignment = TextAnchor.MiddleCenter;
            Button(footer.transform, "Close", 200, "CloseSmall", Close);

            root.SetActive(false);
            return true;
        }

        static void AddScrollbar(NationInfoController c, Transform viewport)
        {
            var template = c.GetComponentsInChildren<Scrollbar>(true).FirstOrDefault(b => b.direction == Scrollbar.Direction.BottomToTop);
            if (template == null)
                template = Resources.FindObjectsOfTypeAll<Scrollbar>().FirstOrDefault(b => b.direction == Scrollbar.Direction.BottomToTop);
            if (template == null)
            {
                Main.Log("No scrollbar to copy; the rule list scrolls with the mouse wheel only");
                return;
            }
            var bar = UiKit.CloneWidget(template.gameObject, viewport, "Scrollbar").GetComponent<Scrollbar>();
            bar.onValueChanged = new Scrollbar.ScrollEvent();
            bar.interactable = true;
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(12f, -8f);
            rect.anchoredPosition = new Vector2(-2f, 0f);
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        static void PopulateNations(TINationState selected)
        {
            var player = Player;
            previewNations = GameStateManager.AllNations()
                .Where(n => n != null && !n.alienNation && n.regions != null && n.regions.Count > 0)
                .OrderBy(n => n.FactionHasControlPoint(player) ? 0 : 1)
                .ThenBy(n => n.displayName)
                .ToList();
            nationDropdown.ClearOptions();
            nationDropdown.AddOptions(previewNations
                .Select(n => n.FactionHasControlPoint(player) ? n.displayName : $"<alpha=#99>{n.displayName}")
                .ToList());
            nationDropdown.SetValueWithoutNotify(Math.Max(0, previewNations.IndexOf(selected)));
            nationDropdown.RefreshShownValue();
        }

        static TINationState PreviewNation =>
            previewNations.Count > 0 ? previewNations[Mathf.Clamp(nationDropdown.value, 0, previewNations.Count - 1)] : null;

        static void RefreshPreview()
        {
            var nation = PreviewNation;
            if (nation == null)
            {
                statsLeft.text = statsRight.text = resultText.text = "";
                return;
            }
            var half = (Metric.All.Count + 1) / 2;
            statsLeft.text = StatLines(nation, Metric.All.Take(half));
            statsRight.text = StatLines(nation, Metric.All.Skip(half));

            var index = PriorityRules.Evaluate(nation, Player, out var preset);
            resultText.text = index >= 0
                ? $"Rule {index + 1} matches: {preset.displayName}"
                : "No rule matches: priorities would be left as they are";
            for (int i = 0; i < ruleBackgrounds.Count; i++)
                ruleBackgrounds[i].color = i == index ? MatchedRuleColor : RuleColor;
        }

        static string StatLines(TINationState nation, IEnumerable<Metric> metrics) =>
            string.Join("\n", metrics.Select(m => $"{m.label}<pos=62%>{m.Format(nation)}"));

        // Save and refresh after any edit; structural edits (add/remove/move) redraw the list
        static void Changed(bool rebuild)
        {
            PriorityRules.Save();
            if (rebuild)
                RebuildRules();
            else
                RefreshPreview();
        }

        static void RebuildRules()
        {
            foreach (Transform child in ruleList)
                Object.Destroy(child.gameObject);
            ruleBackgrounds.Clear();
            var presets = PriorityRules.PresetNames(Player);
            for (int i = 0; i < Rules.Count; i++)
                BuildRule(i, presets);
            if (Rules.Count == 0)
                Hint(ruleList, "No rules yet. Press Add rule to start.", 24);
            // Most of the game's text has masking switched off, so it would draw outside the scroll box
            foreach (var graphic in ruleList.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
            RefreshPreview();
        }

        static void BuildRule(int index, List<string> presets)
        {
            var rule = Rules[index];
            var block = UiKit.NewUIObject($"Rule {index + 1}", ruleList);
            var image = block.AddComponent<Image>();
            image.color = RuleColor;
            ruleBackgrounds.Add(image);
            UiKit.Column(block, 6, 4);

            // Header: number, preset, move, remove
            var header = UiKit.Row(block.transform, "Header", 30);
            Label(header.transform, $"{index + 1}.", 30);
            Label(header.transform, "Use preset", 90);
            var options = new List<string>(presets);
            if (!options.Contains(rule.preset))
                options.Insert(0, rule.preset);
            var presetDropdown = Dropdown(header.transform, "Preset", -1);
            presetDropdown.AddOptions(options.Select(o => presets.Contains(o) ? o : $"{o} (not in this campaign)").ToList());
            presetDropdown.SetValueWithoutNotify(options.IndexOf(rule.preset));
            presetDropdown.RefreshShownValue();
            presetDropdown.onValueChanged.AddListener(v =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ListLineSelect");
                rule.preset = options[v];
                Changed(rebuild: false);
            });
            var up = Button(header.transform, "Up", 60, "GenericSelect", () => Move(index, -1));
            up.interactable = index > 0;
            var down = Button(header.transform, "Down", 70, "GenericSelect", () => Move(index, 1));
            down.interactable = index < Rules.Count - 1;
            Button(header.transform, "Remove", 90, "Cancel", () =>
            {
                Rules.RemoveAt(index);
                Changed(rebuild: true);
            });

            for (int i = 0; i < rule.conditions.Count; i++)
                BuildCondition(block.transform, rule, i);

            var footer = UiKit.Row(block.transform, "Footer", 28);
            Label(footer.transform, "", 30);
            Button(footer.transform, "Add condition", 160, "GenericSelect", () =>
            {
                rule.conditions.Add(new Condition());
                Changed(rebuild: true);
            });
            if (rule.conditions.Count == 0)
                Hint(footer.transform, "No conditions: always matches.", 20, 300);
        }

        static void BuildCondition(Transform parent, Rule rule, int index)
        {
            var condition = rule.conditions[index];
            var row = UiKit.Row(parent, $"Condition {index + 1}", 28);
            Label(row.transform, "", 30);
            Label(row.transform, index == 0 ? "when" : "and", 50);

            var metric = Dropdown(row.transform, "Metric", 250);
            metric.AddOptions(Metric.All.Select(m => m.label).ToList());
            metric.SetValueWithoutNotify(Math.Max(0, Metric.All.FindIndex(m => m.id == condition.metric)));
            metric.RefreshShownValue();
            metric.onValueChanged.AddListener(v =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ListLineSelect");
                condition.metric = Metric.All[v].id;
                Changed(rebuild: false);
            });

            var comparison = Dropdown(row.transform, "Comparison", 80);
            comparison.AddOptions(new List<string> { "<", ">=" });
            comparison.SetValueWithoutNotify(condition.lessThan ? 0 : 1);
            comparison.RefreshShownValue();
            comparison.onValueChanged.AddListener(v =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ListLineSelect");
                condition.lessThan = v == 0;
                Changed(rebuild: false);
            });

            var input = UiKit.CloneWidget(inputTemplate, row.transform, "Value").GetComponent<TMP_InputField>();
            UiKit.Layout(input.gameObject, preferredWidth: 140, preferredHeight: 26);
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 14;
            input.interactable = true;
            input.SetTextWithoutNotify(condition.value.ToString("0.##", CultureInfo.InvariantCulture));
            input.onSelect.AddListener(_ => TIInputManager.BlockKeybindings());
            input.onDeselect.AddListener(_ => TIInputManager.RestoreKeybindings());
            input.onEndEdit.AddListener(text =>
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    condition.value = v;
                input.SetTextWithoutNotify(condition.value.ToString("0.##", CultureInfo.InvariantCulture));
                Changed(rebuild: false);
            });

            Button(row.transform, "X", 36, "Cancel", () =>
            {
                rule.conditions.RemoveAt(index);
                Changed(rebuild: true);
            });
        }

        static void Move(int index, int by)
        {
            var target = index + by;
            if (target < 0 || target >= Rules.Count)
                return;
            (Rules[index], Rules[target]) = (Rules[target], Rules[index]);
            Changed(rebuild: true);
        }

        static Button Button(Transform parent, string label, float width, string sound, Action onClick)
        {
            var go = UiKit.CloneWidget(buttonTemplate, parent, label);
            UiKit.SetLabel(go, label);
            UiKit.Layout(go, preferredWidth: width, preferredHeight: 28);
            var button = go.GetComponent<Button>();
            button.interactable = true;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_" + sound);
                onClick();
            });
            return button;
        }

        static TMP_Dropdown Dropdown(Transform parent, string name, float width)
        {
            var dropdown = UiKit.CloneWidget(dropdownTemplate, parent, name).GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.interactable = true;
            // The game's template puts the list 400 above the box; open it just below instead.
            // TMP_Dropdown shrinks the list to fit its items from the bottom and flips it up if it would go off screen
            var list = dropdown.template;
            if (list != null)
            {
                list.anchorMin = new Vector2(0f, 0f);
                list.anchorMax = new Vector2(1f, 0f);
                list.pivot = new Vector2(0.5f, 1f);
                list.anchoredPosition = new Vector2(0f, -2f);
                list.sizeDelta = new Vector2(0f, 300f);
            }
            if (width > 0)
                UiKit.Layout(dropdown.gameObject, preferredWidth: width, preferredHeight: 28);
            else
                UiKit.Layout(dropdown.gameObject, preferredHeight: 28, flexibleWidth: 1);
            return dropdown;
        }

        static void Label(Transform parent, string text, float width)
        {
            var t = UiKit.CloneText(textTemplate, parent, "Label", text);
            UiKit.Layout(t.gameObject, preferredWidth: width, preferredHeight: 26);
        }

        static void Heading(Transform parent, string heading)
        {
            var h = UiKit.CloneText(textTemplate, parent, heading, heading.ToUpperInvariant());
            h.fontStyle |= FontStyles.Bold;
            UiKit.Layout(h.gameObject, preferredHeight: 22);
        }

        static void Hint(Transform parent, string hint, float height, float width = -1)
        {
            var t = UiKit.CloneText(textTemplate, parent, "Hint", hint, wrap: true);
            t.fontSize *= 0.85f;
            t.alpha = 0.75f;
            UiKit.Layout(t.gameObject, preferredWidth: width, preferredHeight: height);
        }
    }
}
