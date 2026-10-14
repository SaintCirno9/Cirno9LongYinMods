using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FoodAutomation
{
    internal static class TradeUiButtonUtility
    {
        private const float ButtonTextHorizontalPadding = 16f;
        private const float ButtonMinExtraWidth = 8f;

        internal static Button FindButton(GameObject root, string buttonName)
        {
            if (root == null || string.IsNullOrWhiteSpace(buttonName))
            {
                return null;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons == null)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && button.gameObject.name.Equals(buttonName, StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            return null;
        }

        internal static Button CreateButtonFromTemplate(Button templateButton, string objectName, string label, int fontSize, UnityAction onClick)
        {
            return CreateButtonFromTemplate(templateButton, templateButton?.transform?.parent, objectName, label, fontSize, onClick);
        }

        internal static Button CreateButtonFromTemplate(Button templateButton, Transform parent, string objectName, string label, int fontSize, UnityAction onClick)
        {
            if (templateButton == null || templateButton.gameObject == null || parent == null)
            {
                return null;
            }

            GameObject buttonObject = Object.Instantiate(templateButton.gameObject, parent) as GameObject;
            if (buttonObject == null)
            {
                return null;
            }

            buttonObject.name = objectName;
            buttonObject.transform.SetAsLastSibling();
            RemoveUnsafeBehaviours(buttonObject);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                Object.Destroy(buttonObject);
                return null;
            }

            button.enabled = true;
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            SetButtonLabel(buttonObject, label, fontSize);
            buttonObject.SetActive(true);
            return button;
        }

        internal static void SetButtonLabel(GameObject buttonObject, string label, int fontSize)
        {
            if (buttonObject == null)
            {
                return;
            }

            Text[] texts = buttonObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.text = label;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                AdjustButtonTextLayout(text);
                text.raycastTarget = false;
            }

            Image[] images = buttonObject.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null)
                {
                    image.raycastTarget = true;
                }
            }
        }

        internal static void RemoveButtonsByPrefix(GameObject root, string prefix)
        {
            Transform rootTransform = root?.transform;
            if (rootTransform == null)
            {
                return;
            }

            Transform[] transforms = rootTransform.GetComponentsInChildren<Transform>(true);
            if (transforms == null)
            {
                return;
            }

            List<GameObject> staleButtons = new();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    staleButtons.Add(child.gameObject);
                }
            }

            for (int i = 0; i < staleButtons.Count; i++)
            {
                Object.Destroy(staleButtons[i]);
            }
        }

        internal static float GetPreferredButtonWidth(GameObject buttonObject, float fallbackWidth)
        {
            Text[] texts = buttonObject?.GetComponentsInChildren<Text>(true);
            if (texts == null || texts.Length == 0)
            {
                return fallbackWidth;
            }

            float preferredWidth = 0f;
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                preferredWidth = Mathf.Max(preferredWidth, text.preferredWidth);
            }

            if (preferredWidth <= 0f)
            {
                return fallbackWidth;
            }

            return Mathf.Max(fallbackWidth, preferredWidth + (ButtonTextHorizontalPadding * 2f) + ButtonMinExtraWidth);
        }

        private static void RemoveUnsafeBehaviours(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours != null)
            {
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null || behaviour is Button || behaviour is Text || behaviour is Image)
                    {
                        continue;
                    }

                    string typeName = behaviour.GetType().FullName ?? string.Empty;
                    if (typeName.Contains("Tween", StringComparison.OrdinalIgnoreCase) ||
                        typeName.Contains("Animation", StringComparison.OrdinalIgnoreCase))
                    {
                        Object.Destroy(behaviour);
                    }
                }
            }

            Component animator = root.GetComponent("Animator");
            if (animator != null)
            {
                Object.Destroy(animator);
            }
        }

        private static void AdjustButtonTextLayout(Text text)
        {
            RectTransform textRect = text?.rectTransform;
            if (textRect == null)
            {
                return;
            }

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(ButtonTextHorizontalPadding, 0f);
            textRect.offsetMax = new Vector2(-ButtonTextHorizontalPadding, 0f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }
    }
}
