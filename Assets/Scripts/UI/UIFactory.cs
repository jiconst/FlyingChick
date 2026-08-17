using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingChick
{
    // Small set of runtime-construction helpers so UI screens don't each
    // repeat Canvas/TMP/Image boilerplate. Positioning mirrors the old
    // OnGUI `new Rect(x, y, w, h)` convention on purpose (x/y measured from
    // the TOP-LEFT, y growing down) so porting each OnGUI call site was a
    // close-to-mechanical translation instead of a layout redesign.
    public static class UIFactory
    {
        private static Sprite whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite != null) return whiteSprite;
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                return whiteSprite;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        // Top-left origin, matches `new Rect(x, y, w, h)` from OnGUI exactly.
        public static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // Center-pivoted but still positioned/sized like a top-left Rect --
        // used wherever the old OnGUI code centered something itself (e.g.
        // pulsing the Fever badge around its own middle, or toasts that
        // rise/fade in place).
        public static void SetTopLeftCentered(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, int fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var rt = CreateChild(parent, name);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UIFontProvider.Get();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var rt = CreateChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WhiteSprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            var rt = CreateChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize, Color labelColor, out TextMeshProUGUI text)
        {
            var rt = CreateChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WhiteSprite;
            img.color = new Color(1f, 1f, 1f, 0.9f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            text = CreateText(rt, "Label", fontSize, labelColor, TextAlignmentOptions.Center, FontStyles.Bold);
            text.text = label;
            StretchFull((RectTransform)text.transform);

            return btn;
        }

        // Minimal runtime TMP_InputField: background Image + a masked
        // "Text Area" holding the actual TMP_Text the input field types
        // into. This is the standard TMP_InputField hierarchy (Editor's
        // "Create > UI > Input Field - TextMeshPro" builds the same shape),
        // just assembled by hand since everything here is code-driven.
        public static TMP_InputField CreateInputField(Transform parent, string name, int fontSize, Color textColor, int characterLimit = 0, bool password = false)
        {
            var rt = CreateChild(parent, name);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = WhiteSprite;
            bg.color = new Color(1f, 1f, 1f, 0.9f);

            var textAreaRt = CreateChild(rt, "Text Area");
            textAreaRt.gameObject.AddComponent<RectMask2D>();
            StretchFull(textAreaRt);
            textAreaRt.offsetMin = new Vector2(8f, 4f);
            textAreaRt.offsetMax = new Vector2(-8f, -4f);

            var textComponent = CreateText(textAreaRt, "Text", fontSize, textColor, TextAlignmentOptions.MidlineLeft);
            StretchFull((RectTransform)textComponent.transform);

            var inputField = rt.gameObject.AddComponent<TMP_InputField>();
            inputField.targetGraphic = bg;
            inputField.textViewport = textAreaRt;
            inputField.textComponent = textComponent;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            if (characterLimit > 0) inputField.characterLimit = characterLimit;
            if (password)
            {
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.inputType = TMP_InputField.InputType.Password;
                inputField.ForceLabelUpdate();
            }

            return inputField;
        }

        // A fully transparent, full-screen button used as a "tap anywhere to
        // start" catcher (see StartScreen). Must be created BEFORE any real
        // buttons under the same parent -- later siblings render on top and
        // intercept clicks first, so this only ever fires when nothing else
        // was hit.
        public static Button CreateFullScreenTapCatcher(Transform parent, string name)
        {
            var rt = CreateChild(parent, name);
            StretchFull(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            return btn;
        }
    }
}
