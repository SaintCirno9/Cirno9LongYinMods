using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HotkeyRemap
{
    internal static class HotkeySettingsMenuUi
    {
        private const string RootName = "HotkeyRemap_SettingsRoot";
        private const string OpenButtonName = "HotkeyRemap_OpenButton";
        private const string PanelName = "HotkeyRemap_Panel";
        private const string RowButtonPrefix = "HotkeyRemap_RowButton_";
        private const float PanelWidth = 640f;
        private const float PanelHeight = 740f;
        private const float RowHeight = 32f;
        private const float RowStartY = -96f;
        private const float RowGap = 30f;

        private static readonly List<BindingEntry> BindingEntries = new()
        {
            new("快速保存", () => Main.Config.QuickSave, key => Main.Config.QuickSave = key),
            new("快速读档", () => Main.Config.QuickLoad, key => Main.Config.QuickLoad = key),
            new("任务开关", () => Main.Config.MissionToggle, key => Main.Config.MissionToggle = key),
            new("大地图休息", () => Main.Config.BigMapRest, key => Main.Config.BigMapRest = key),
            new("剧情推进", () => Main.Config.PlotAdvance, key => Main.Config.PlotAdvance = key),
            new("人物详情", () => Main.Config.HeroDetail, key => Main.Config.HeroDetail = key),
            new("离开城市", () => Main.Config.LeaveCity, key => Main.Config.LeaveCity = key),
            new("战斗自动", () => Main.Config.BattleAutoToggle, key => Main.Config.BattleAutoToggle = key),
            new("战斗动作 W", () => Main.Config.BattleActionFastW, key => Main.Config.BattleActionFastW = key),
            new("战斗动作 E", () => Main.Config.BattleActionFastE, key => Main.Config.BattleActionFastE = key),
            new("战斗动作 S", () => Main.Config.BattleActionFastS, key => Main.Config.BattleActionFastS = key),
            new("战斗动作 D", () => Main.Config.BattleActionFastD, key => Main.Config.BattleActionFastD = key),
            new("战斗动作 X", () => Main.Config.BattleActionFastX, key => Main.Config.BattleActionFastX = key),
            new("战斗动作 C", () => Main.Config.BattleActionFastC, key => Main.Config.BattleActionFastC = key),
            new("修炼上", () => Main.Config.StudyUp, key => Main.Config.StudyUp = key),
            new("修炼下", () => Main.Config.StudyDown, key => Main.Config.StudyDown = key),
            new("修炼左", () => Main.Config.StudyLeft, key => Main.Config.StudyLeft = key),
            new("修炼右", () => Main.Config.StudyRight, key => Main.Config.StudyRight = key),
            new("界面确认", () => Main.Config.UiSubmit, key => Main.Config.UiSubmit = key),
            new("界面取消", () => Main.Config.UiCancel, key => Main.Config.UiCancel = key),
        };

        private static GameObject _root;
        private static Button _openButton;
        private static GameObject _panel;
        private static Text _statusText;
        private static Font _font;
        private static BindingEntry _pendingBinding;
        private static int _captureArmFrame;

        internal static void EnsureInjected(SettingMenuController controller)
        {
            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"设置菜单注入入口触发：instanceType={controller?.GetType().FullName ?? "<null>"}");
            }

            GameObject settingMenu = GetSettingMenuObject(controller);
            if (settingMenu == null)
            {
                Main.Log.Warning("设置菜单注入失败：未找到 settingMenu 字段或对象为空。");
                return;
            }

            if (_root != null && _root.transform.parent == settingMenu.transform)
            {
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg("设置菜单热键面板已存在，执行刷新。");
                }

                RefreshAll();
                SetPanelVisible(false);
                return;
            }

            BuildUi(controller, settingMenu);
            RefreshAll();
            SetPanelVisible(false);
        }

        internal static void HandleInput()
        {
            if (_panel == null || !_panel.activeInHierarchy || _pendingBinding == null)
            {
                return;
            }

            if (Time.frameCount <= _captureArmFrame)
            {
                return;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || !Input.GetKeyDown(key))
                {
                    continue;
                }

                ApplyBinding(key);
                return;
            }
        }

        private static void BuildUi(SettingMenuController controller, GameObject settingMenu)
        {
            Text templateText = settingMenu.GetComponentInChildren<Text>(true);
            RectTransform menuRect = settingMenu.GetComponent<RectTransform>();
            if (templateText == null || menuRect == null)
            {
                Main.Log.Warning(
                    $"设置菜单缺少可复用的文本模板或根节点，跳过热键设置面板注入。text={(templateText != null)} rect={(menuRect != null)}");
                return;
            }

            Button templateButton = FindTemplateButton(settingMenu);
            Main.Log.Msg($"设置菜单注入开始：root={settingMenu.name}，templateButton={(templateButton == null ? "<null>" : templateButton.gameObject.name)}");

            _font = templateText.font;

            _root = new GameObject(RootName);
            _root.transform.SetParent(settingMenu.transform, false);
            RectTransform rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Transform openButtonParent = GetSettingsContentParent(controller, settingMenu);
            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"热键入口父节点：{DescribeTransform(openButtonParent)}");
                Main.Log.Msg($"热键入口父链：{DescribeTransformChain(openButtonParent, 6)}");
            }
            _openButton = CreateOpenButton(openButtonParent, OpenButtonName, (UnityAction)OnOpenButtonClicked);
            if (_openButton == null)
            {
                Main.Log.Warning("设置菜单注入失败：无法创建热键设置按钮。");
                Object.Destroy(_root);
                _root = null;
                return;
            }

            PositionOpenButton(controller, openButtonParent, _openButton.GetComponent<RectTransform>());

            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"热键设置按钮已创建：parent={openButtonParent.name} info={DescribeRectTransform(_openButton.GetComponent<RectTransform>())}");
            }

            _panel = CreatePanel(_root.transform);
            if (_panel == null)
            {
                Main.Log.Warning("设置菜单注入失败：无法创建热键设置面板。");
                return;
            }

            CreateHeaderAndRows(templateButton);
            Main.Log.Msg("设置菜单注入完成：已创建热键设置按钮与面板。");
        }

        private static GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new(PanelName);
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = new Vector2(0f, -10f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.88f);
            panel.SetActive(false);
            return panel;
        }

        private static void CreateHeaderAndRows(Button templateButton)
        {
            CreateText(_panel.transform, "Title", "热键设置", 24, TextAnchor.UpperLeft, new Vector2(24f, -18f), new Vector2(240f, 36f));
            CreateText(
                _panel.transform,
                "Hint",
                "点击右侧键位按钮后，按下新的按键即可保存到 HotkeyRemap.cfg。",
                18,
                TextAnchor.UpperLeft,
                new Vector2(24f, -50f),
                new Vector2(560f, 40f));

            _statusText = CreateText(
                _panel.transform,
                "Status",
                "当前未选择待修改项。",
                18,
                TextAnchor.UpperLeft,
                new Vector2(24f, -76f),
                new Vector2(560f, 28f));

            Button closeButton = CreateStandaloneButton(_panel.transform, "HotkeyRemap_CloseButton", "关闭", 18, (UnityAction)(() => SetPanelVisible(false)));
            RectTransform closeRect = closeButton?.GetComponent<RectTransform>();
            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-24f, -18f);
                closeRect.sizeDelta = new Vector2(120f, 32f);
            }

            for (int i = 0; i < BindingEntries.Count; i++)
            {
                BindingEntry entry = BindingEntries[i];
                float rowY = RowStartY - (i * RowGap);
                entry.LabelText = CreateText(
                    _panel.transform,
                    $"HotkeyRemap_RowLabel_{i}",
                    entry.DisplayName,
                    18,
                    TextAnchor.MiddleLeft,
                    new Vector2(24f, rowY),
                    new Vector2(240f, RowHeight));
                entry.ValueButton = CreateStandaloneButton(
                    _panel.transform,
                    RowButtonPrefix + i,
                    string.Empty,
                    18,
                    (UnityAction)(() => BeginCapture(entry)));

                RectTransform valueRect = entry.ValueButton?.GetComponent<RectTransform>();
                if (valueRect != null)
                {
                    valueRect.anchorMin = new Vector2(1f, 1f);
                    valueRect.anchorMax = new Vector2(1f, 1f);
                    valueRect.pivot = new Vector2(1f, 1f);
                    valueRect.anchoredPosition = new Vector2(-24f, rowY);
                    valueRect.sizeDelta = new Vector2(196f, RowHeight);
                }
            }
        }

        private static void OnOpenButtonClicked()
        {
            Main.Log.Msg("已点击热键设置按钮。");
            SetPanelVisible(true);
            RefreshAll();
        }

        private static void SetPanelVisible(bool visible)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.SetActive(visible);
            if (!visible)
            {
                _pendingBinding = null;
                if (_statusText != null)
                {
                    _statusText.text = "当前未选择待修改项。";
                }

                RefreshAll();
            }
        }

        private static void BeginCapture(BindingEntry entry)
        {
            Main.Log.Msg($"开始等待新的热键输入：{entry.DisplayName}");
            _pendingBinding = entry;
            _captureArmFrame = Time.frameCount + 1;
            if (_statusText != null)
            {
                _statusText.text = $"请按下新的按键，用于 {entry.DisplayName}。";
            }

            RefreshAll();
        }

        private static void ApplyBinding(KeyCode key)
        {
            if (_pendingBinding == null)
            {
                return;
            }

            _pendingBinding.Setter(key);
            SyncRuntimeHotkeyState();
            HotkeyRemapConfigStore.Save(Main.Config, Main.Log);
            BattleHotkeyListHelper.TryApplyConfiguredLists();

            if (_statusText != null)
            {
                _statusText.text = $"{_pendingBinding.DisplayName} 已更新为 {GetKeyDisplayText(key)}。";
            }

            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"已通过设置菜单更新热键：{_pendingBinding.DisplayName}={key}");
            }

            _pendingBinding = null;
            RefreshAll();
        }

        private static void SyncRuntimeHotkeyState()
        {
            if (Main.Config.HeroDetail == KeyCode.Q)
            {
                HudHeroDetailNativePatch.TryRestore(Main.Log);
                return;
            }

            HudHeroDetailNativePatch.TryInstall(Main.Log);
        }

        private static void RefreshAll()
        {
            for (int i = 0; i < BindingEntries.Count; i++)
            {
                BindingEntry entry = BindingEntries[i];
                if (entry.ValueButton == null)
                {
                    continue;
                }

                string label = _pendingBinding == entry ? "按任意键..." : GetKeyDisplayText(entry.Getter());
                SetButtonLabel(entry.ValueButton.gameObject, label, 18);
            }
        }

        private static string GetKeyDisplayText(KeyCode key)
        {
            return key switch
            {
                KeyCode.Space => "空格",
                KeyCode.Return => "回车",
                KeyCode.KeypadEnter => "小键盘回车",
                KeyCode.Escape => "Esc",
                KeyCode.Tab => "Tab",
                KeyCode.LeftShift => "左 Shift",
                KeyCode.RightShift => "右 Shift",
                KeyCode.LeftControl => "左 Ctrl",
                KeyCode.RightControl => "右 Ctrl",
                KeyCode.LeftAlt => "左 Alt",
                KeyCode.RightAlt => "右 Alt",
                KeyCode.Mouse0 => "鼠标左键",
                KeyCode.Mouse1 => "鼠标右键",
                KeyCode.Mouse2 => "鼠标中键",
                _ => key.ToString()
            };
        }

        private static Button FindTemplateButton(GameObject root)
        {
            Button[] buttons = root?.GetComponentsInChildren<Button>(true);
            if (buttons == null)
            {
                Main.Log.Warning("设置菜单按钮模板查找失败：按钮数组为空。");
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button?.gameObject == null)
                {
                    continue;
                }

                if (!button.gameObject.name.StartsWith("HotkeyRemap_", StringComparison.Ordinal))
                {
                    return button;
                }
            }

            Main.Log.Warning("设置菜单按钮模板查找失败：未找到可复用按钮。");
            return null;
        }

        private static GameObject GetSettingMenuObject(SettingMenuController controller)
        {
            if (controller == null)
            {
                return null;
            }

            GameObject settingMenu = controller.settingMenu;
            if (settingMenu == null)
            {
                Main.Log.Warning($"设置菜单字段读取失败：type={controller.GetType().FullName} 的 settingMenu 为空。");
                return null;
            }

            return settingMenu;
        }

        private static Transform GetSettingsContentParent(SettingMenuController controller, GameObject settingMenu)
        {
            RectTransform autoSaveRect = controller?.autoSave?.GetComponent<RectTransform>();
            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"热键入口参考控件 autoSave：{DescribeRectTransform(autoSaveRect)}");
            }
            if (autoSaveRect?.parent != null)
            {
                return autoSaveRect.parent;
            }

            return settingMenu.transform;
        }

        private static void PositionOpenButton(SettingMenuController controller, Transform parentTransform, RectTransform openButtonRect)
        {
            if (openButtonRect == null)
            {
                return;
            }

            RectTransform languageRect = controller?.languageDropDown?.GetComponent<RectTransform>();
            RectTransform fullScreenRect = controller?.fullScreen?.GetComponent<RectTransform>();
            if (languageRect != null && fullScreenRect != null && languageRect.parent == parentTransform && fullScreenRect.parent == parentTransform)
            {
                float buttonWidth = 72f;
                float buttonHeight = 64f;
                float x = fullScreenRect.localPosition.x;
                float y = languageRect.localPosition.y + 14f;
                openButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
                openButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
                openButtonRect.pivot = new Vector2(0.5f, 0.5f);
                openButtonRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                openButtonRect.localPosition = new Vector3(x, y, 0f);
                openButtonRect.SetAsLastSibling();
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg($"热键入口定位后 buttonRect(全屏/语言参考)：{DescribeRectTransform(openButtonRect)}");
                }
                return;
            }

            if (parentTransform is RectTransform parentRect)
            {
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg($"热键入口定位前 parentRect：{DescribeRectTransform(parentRect)}");
                }
                openButtonRect.anchorMin = new Vector2(1f, 1f);
                openButtonRect.anchorMax = new Vector2(1f, 1f);
                openButtonRect.pivot = new Vector2(1f, 1f);
                openButtonRect.anchoredPosition = new Vector2(-18f, -12f);
                openButtonRect.sizeDelta = new Vector2(92f, 24f);
                openButtonRect.SetAsLastSibling();
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg($"热键入口定位后 buttonRect：{DescribeRectTransform(openButtonRect)}");
                }
                return;
            }

            openButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
            openButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
            openButtonRect.pivot = new Vector2(0.5f, 0.5f);
            openButtonRect.sizeDelta = new Vector2(92f, 24f);

            if (parentTransform != null)
            {
                openButtonRect.localPosition = new Vector3(122f, 206f, 0f);
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg($"热键入口定位后 buttonRect(Transform父节点)：{DescribeRectTransform(openButtonRect)}");
                }
                return;
            }

            openButtonRect.anchoredPosition = new Vector2(0f, -150f);
            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"热键入口定位后 buttonRect(兜底)：{DescribeRectTransform(openButtonRect)}");
            }
        }

        private static string DescribeTransformChain(Transform transform, int maxDepth)
        {
            if (transform == null)
            {
                return "<null>";
            }

            List<string> parts = new();
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                parts.Add($"{depth}:{DescribeTransform(current)}");
                current = current.parent;
                depth++;
            }

            return string.Join(" <= ", parts);
        }

        private static string DescribeTransform(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            if (transform is RectTransform rectTransform)
            {
                return DescribeRectTransform(rectTransform);
            }

            return $"{transform.name}(type={transform.GetType().Name}, local={transform.localPosition}, world={transform.position}, parent={transform.parent?.name ?? "<null>"})";
        }

        private static string DescribeRectTransform(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return "<null>";
            }

            return $"{rectTransform.name}(anchorMin={rectTransform.anchorMin}, anchorMax={rectTransform.anchorMax}, pivot={rectTransform.pivot}, anchored={rectTransform.anchoredPosition}, size={rectTransform.sizeDelta}, local={rectTransform.localPosition}, parent={rectTransform.parent?.name ?? "<null>"})";
        }

        private static Button CreateButtonFromTemplate(
            Button templateButton,
            Transform parent,
            string objectName,
            string label,
            int fontSize,
            UnityAction onClick)
        {
            if (templateButton == null || templateButton.gameObject == null || parent == null)
            {
                return null;
            }

            GameObject buttonObject = Object.Instantiate(templateButton.gameObject, parent);
            if (buttonObject == null)
            {
                return null;
            }

            buttonObject.name = objectName;
            RemoveUnsafeBehaviours(buttonObject);
            buttonObject.transform.SetAsLastSibling();

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

        private static Button CreateStandaloneButton(
            Transform parent,
            string objectName,
            string label,
            int fontSize,
            UnityAction onClick)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject buttonObject = new(objectName);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.transform.SetAsLastSibling();

            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(156f, 42f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.98f, 0.98f, 0.98f, 0.98f);

            Outline border = buttonObject.AddComponent<Outline>();
            border.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);
            border.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.98f, 0.98f, 0.98f, 0.98f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            button.colors = colors;
            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            GameObject labelObject = new("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = label;
            return button;
        }

        private static Button CreateOpenButton(
            Transform parent,
            string objectName,
            UnityAction onClick)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject buttonObject = new(objectName);
            buttonObject.transform.SetParent(parent, false);
            buttonObject.transform.SetAsLastSibling();

            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(72f, 64f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.14f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colors;
            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            CreateOpenButtonText(buttonObject.transform, "TopLabel", "热键", 22, new Vector2(0f, 16f), new Vector2(72f, 22f));
            CreateOpenButtonText(buttonObject.transform, "BottomLabel", "设置", 22, new Vector2(0f, -12f), new Vector2(72f, 22f));
            return button;
        }

        private static void CreateOpenButtonText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new(objectName);
            textObject.transform.SetParent(parent, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = content;
        }

        private static void RemoveUnsafeBehaviours(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours == null)
            {
                return;
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is Button || behaviour is Text || behaviour is Image)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Tween", StringComparison.OrdinalIgnoreCase)
                    || typeName.Contains("Animation", StringComparison.OrdinalIgnoreCase))
                {
                    Object.Destroy(behaviour);
                }
            }
        }

        private static void SetButtonLabel(GameObject buttonObject, string label, int fontSize)
        {
            Text[] texts = buttonObject?.GetComponentsInChildren<Text>(true);
            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.text = label;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.black;
                text.raycastTarget = false;
            }
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new(objectName);
            textObject.transform.SetParent(parent, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private sealed class BindingEntry
        {
            internal BindingEntry(string displayName, Func<KeyCode> getter, Action<KeyCode> setter)
            {
                DisplayName = displayName;
                Getter = getter;
                Setter = setter;
            }

            internal string DisplayName { get; }
            internal Func<KeyCode> Getter { get; }
            internal Action<KeyCode> Setter { get; }
            internal Text LabelText { get; set; }
            internal Button ValueButton { get; set; }
        }
    }

    [HarmonyPatch]
    internal static class SettingMenuControllerShowSettingMenuPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("SettingMenuController");
            MethodBase method = type == null ? null : AccessTools.Method(type, "ShowSettingMenu");
            Main.Log.Msg($"HotkeySettingsMenuUi.TargetMethod: type={(type == null ? "<null>" : type.FullName)} method={(method == null ? "<null>" : method.Name)}");
            return method;
        }

        [HarmonyPostfix]
        private static void Postfix(SettingMenuController __instance)
        {
            try
            {
                HotkeySettingsMenuUi.EnsureInjected(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"注入热键设置菜单失败：{ex.Message}");
            }
        }
    }
}
