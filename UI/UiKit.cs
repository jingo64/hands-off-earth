using System.Linq;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HandsOffEarth.UI
{
    // Helpers for building UI from clones of the game's own widgets, so mod screens match the game's look
    static class UiKit
    {
        // Clones a game widget and strips everything that ties it to its original screen
        public static GameObject CloneWidget(GameObject template, Transform parent, string name)
        {
            var clone = Object.Instantiate(template, parent, false);
            clone.name = name;
            clone.SetActive(true);
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var typeName = behaviour.GetType().Name;
                if (typeName == "TooltipTrigger" || typeName == "RightClickHandler" || typeName == "RectTransformChangeListener")
                    Object.DestroyImmediate(behaviour);
            }
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child != clone.transform && (child.name.StartsWith("Dummy_") || child.name == "RightClick Icon"))
                    Object.DestroyImmediate(child.gameObject);
            }
            // Replacing the events drops the persistent (inspector-wired) listeners that call the original screen
            if (clone.TryGetComponent<Button>(out var button))
                button.onClick = new Button.ButtonClickedEvent();
            if (clone.TryGetComponent<Toggle>(out var toggle))
            {
                toggle.onValueChanged = new Toggle.ToggleEvent();
                toggle.group = null;
            }
            if (clone.TryGetComponent<Slider>(out var slider))
                slider.onValueChanged = new Slider.SliderEvent();
            if (clone.TryGetComponent<TMP_Dropdown>(out var dropdown))
                dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            if (clone.TryGetComponent<TMP_InputField>(out var input))
            {
                input.onValueChanged = new TMP_InputField.OnChangeEvent();
                input.onEndEdit = new TMP_InputField.SubmitEvent();
                input.onSubmit = new TMP_InputField.SubmitEvent();
                input.onSelect = new TMP_InputField.SelectionEvent();
                input.onDeselect = new TMP_InputField.SelectionEvent();
            }
            return clone;
        }

        public static TMP_Text CloneText(GameObject template, Transform parent, string name, string text, bool wrap = false)
        {
            var clone = CloneWidget(template, parent, name);
            foreach (Transform child in clone.transform)
                Object.Destroy(child.gameObject);
            var tmp = clone.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            return tmp;
        }

        public static void SetLabel(GameObject widget, string text)
        {
            var label = widget.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = text;
        }

        public static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Layout(GameObject go, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minWidth = preferredWidth;
            element.minHeight = preferredHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float height, float spacing = 6)
        {
            var row = NewUIObject(name, parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Layout(row, preferredHeight: height);
            return layout;
        }

        public static VerticalLayoutGroup Column(GameObject go, int padding, float spacing)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void CopyImage(Image from, Image to)
        {
            to.sprite = from.sprite;
            to.type = from.type;
            to.color = from.color;
            to.material = from.material;
        }

        public static Slider CreateSlider(Transform parent)
        {
            var graphics = Resources.FindObjectsOfTypeAll<GraphicsMenuController>().FirstOrDefault(g => g.uiScaleSlider != null);
            Slider slider;
            if (graphics != null)
            {
                slider = CloneWidget(graphics.uiScaleSlider.gameObject, parent, "Slider").GetComponent<Slider>();
                FitSliderToWidth(slider);
            }
            else
            {
                var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
                go.name = "Slider";
                go.transform.SetParent(parent, false);
                slider = go.GetComponent<Slider>();
                foreach (var graphic in go.GetComponentsInChildren<Image>())
                    graphic.color = graphic.name == "Background" ? new Color(0.1f, 0.15f, 0.2f) : TIUtilities.UITextColor;
            }
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.wholeNumbers = true;
            slider.interactable = true;
            return slider;
        }

        // The options-menu slider has a fixed-width track and no inset for its handle; make it fit any row
        static void FitSliderToWidth(Slider slider)
        {
            if (slider.transform.Find("Background") is RectTransform background)
            {
                background.anchorMin = new Vector2(0f, 0.5f);
                background.anchorMax = new Vector2(1f, 0.5f);
                background.anchoredPosition = Vector2.zero;
                background.sizeDelta = new Vector2(0f, background.sizeDelta.y);
            }
            var handle = slider.handleRect;
            if (handle == null || handle.parent != slider.transform)
                return;
            var halfWidth = handle.sizeDelta.x / 2f;
            var area = NewUIObject("Handle Slide Area", slider.transform);
            Stretch(area);
            var areaRect = (RectTransform)area.transform;
            areaRect.offsetMin = new Vector2(halfWidth, 0f);
            areaRect.offsetMax = new Vector2(-halfWidth, 0f);
            area.transform.SetSiblingIndex(handle.GetSiblingIndex());
            handle.SetParent(area.transform, false);
            slider.handleRect = handle;
        }
    }
}
