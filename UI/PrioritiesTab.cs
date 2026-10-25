using System;
using System.Collections.Generic;
using System.Linq;
using HandsOffEarth.Priorities;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HandsOffEarth.UI
{
    // Adds a row above the nation priorities tab's buttons: a "Use priority rules" checkbox for the
    // nation, and a button that opens the rules window
    public static class PrioritiesTab
    {
        const string RowName = "PriorityRulesRow";
        const float RowHeight = 26f;

        static NationInfoController controller;
        static GameObject row;
        static Toggle toggle;

        static void Build(NationInfoController c)
        {
            var pane = c.prioritiesTab.transform;
            if (pane.Find(RowName) != null)
                return;
            var buttons = pane.Find("Button Container");
            var designButton = buttons != null ? buttons.Find("Open Design Preset Panel") : null;
            var toggleTemplate = Resources.FindObjectsOfTypeAll<FleetsScreenController>().FirstOrDefault()?.ShowObsoletePartsToggle;
            if (designButton == null || toggleTemplate == null)
            {
                Main.Error("Priorities tab widgets not found; priority rules controls not added");
                return;
            }

            row = UiKit.NewUIObject(RowName, pane);
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            // Just above the game's buttons (5 from the bottom, 30 high)
            rowRect.offsetMin = new Vector2(5f, 38f);
            rowRect.offsetMax = new Vector2(-5f, 38f + RowHeight);

            // Make room by shortening the priority list
            if (pane.Find("PriorityListContainer") is RectTransform list)
                list.offsetMin += new Vector2(0f, RowHeight + 4f);

            toggle = UiKit.CloneWidget(toggleTemplate.gameObject, row.transform, "Use Priority Rules").GetComponent<Toggle>();
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = new Vector2(0f, 0f);
            toggleRect.anchorMax = new Vector2(0.55f, 1f);
            toggleRect.pivot = new Vector2(0f, 0.5f);
            toggleRect.offsetMin = toggleRect.offsetMax = Vector2.zero;
            UiKit.SetLabel(toggle.gameObject, "Use priority rules");
            toggle.interactable = true;
            toggle.onValueChanged.AddListener(OnToggle);

            var button = UiKit.CloneWidget(designButton.gameObject, row.transform, "Open Priority Rules");
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(0.55f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.offsetMin = new Vector2(10f, 0f);
            buttonRect.offsetMax = Vector2.zero;
            UiKit.SetLabel(button, "Priority rules");
            var b = button.GetComponent<Button>();
            b.interactable = true;
            b.onClick.AddListener(() =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_OpenSmall");
                RulesWindow.Open(controller, controller != null ? controller.nation : null);
            });
        }

        public static void Update(NationInfoController c)
        {
            controller = c;
            if (row == null)
                Build(c);
            Refresh();
        }

        public static void Refresh()
        {
            if (row == null || controller == null)
                return;
            var nation = controller.nation;
            var player = GameControl.control.activePlayer;
            row.SetActive(Main.enabled);
            var hasControlPoint = nation != null && nation.FactionHasControlPoint(player);
            toggle.gameObject.SetActive(hasControlPoint);
            toggle.SetIsOnWithoutNotify(OptIns.IsOn(nation));
            // Not via UpdatePriorityPresetFromChanges, which adds another "Custom" option each time it runs
            controller.priorityPresetDropdown.interactable = hasControlPoint && !(Main.enabled && OptIns.IsOn(nation));
        }

        static void OnToggle(bool on)
        {
            var nation = controller != null ? controller.nation : null;
            if (nation == null)
                return;
            OptIns.Set(nation, on);
            AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_GenericConfirm");
            Main.Log($"{nation.displayName}: priority rules {(on ? "on" : "off")}");
            if (on)
                RuleApplier.Apply(nation, GameControl.control.activePlayer);
            Redraw();
        }

        // After pips change, redraw the tab the way the game does when you pick a preset
        public static void Redraw()
        {
            if (controller == null || controller.nation == null)
                return;
            controller.UpdatePriorityList();
            controller.UpdateTinyControlPoints();
            var presets = Traverse.Create(controller).Field("priorityPresetDictionary").GetValue<Dictionary<TIPriorityPresetTemplate, int>>();
            NationInfoController.UpdatePriorityPresetFromChanges(controller.priorityPresetDropdown, controller.nation, presets);
            Refresh();
        }
    }

    [HarmonyPatch(typeof(NationInfoController), nameof(NationInfoController.UpdatePriorityList))]
    static class UpdatePriorityList_Patch
    {
        static void Postfix(NationInfoController __instance)
        {
            try
            {
                PrioritiesTab.Update(__instance);
            }
            catch (Exception e)
            {
                Main.Error("Updating the priority rules controls failed: " + e);
            }
        }
    }

    // Nations that follow the rules get their preset from them, so the preset dropdown is locked
    [HarmonyPatch(typeof(NationInfoController), nameof(NationInfoController.UpdatePriorityPresetFromChanges))]
    static class UpdatePriorityPresetFromChanges_Patch
    {
        static void Postfix(TMP_Dropdown priorityPresetDropdown, TINationState nation)
        {
            if (Main.enabled && priorityPresetDropdown != null && OptIns.IsOn(nation))
                priorityPresetDropdown.interactable = false;
        }
    }
}
