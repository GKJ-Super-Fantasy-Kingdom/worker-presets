using System;
using System.Collections.Generic;
using System.Linq;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace WorkerPresets
{
    internal sealed class PresetPanel : MonoBehaviour
    {
        private const int MaxPreviewRows = 3;
        private BuildingCity source;
        private LayoutBook book;
        private int index;
        private Layout Slot => book.Slots[index];
        private TextMeshProUGUI hints;
        private Action backAction;
        private static readonly string[] Names = { "Day Shift", "Night Shift", "Harvest", "Construction", "Supplies", "Recovery" };

        internal static int PreviewRows(float logicalHeight) =>
            logicalHeight < 420 ? 1 : logicalHeight < 580 ? 2 : MaxPreviewRows;

        internal static float PanelWidth(float logicalWidth)
        {
            // Leave space for the calendar dock beside the shared selection ribbon.
            float preferred = Math.Max(448, Math.Min(520, logicalWidth * .35f));
            return Math.Max(1, Math.Min(preferred, logicalWidth - 48));
        }

        private float Scale => GetComponentInParent<Canvas>().rootCanvas.scaleFactor;

        internal static Button Button(Transform parent, string title, Action action)
        {
            var prefab = Resources.Load<GameObject>("UI/OverlayElementButton").transform.Find("Button");
            var button = Instantiate(prefab.GetComponent<Button>(), parent, false);
            button.name = title;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = title;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 1);
            label.rectTransform.offsetMax = new Vector2(-8, -1);
            label.enableAutoSizing = true;
            label.fontSizeMin = 15;
            label.fontSizeMax = 28;
            var size = button.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 42;
            size.flexibleWidth = 1;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => action());
            var nav = button.navigation;
            nav.mode = Navigation.Mode.Automatic;
            button.navigation = nav;
            button.gameObject.SetActive(true);
            return button;
        }

        internal static Button AddLauncher(Transform panel, RectTransform workerRow, BuildingCity source)
        {
            var button = Button(panel, "Worker Presets", () => Open(source));
            var jobs = workerRow.Find("Jobs")?.GetComponent<TextMeshProUGUI>();
            // Reuse the space between a short job title and its +/- controls.
            // Timed or unusually long translated titles keep their full native row.
            const float amountWidth = 106, buttonWidth = 100;
            if (jobs != null && jobs.preferredWidth + 32 <= workerRow.rect.width - amountWidth - buttonWidth)
            {
                var rect = (RectTransform)button.transform;
                rect.SetParent(workerRow, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, .5f);
                rect.anchoredPosition = new Vector2(-amountWidth, 0);
                rect.sizeDelta = new Vector2(buttonWidth, 38);
                button.GetComponent<LayoutElement>().ignoreLayout = true;
                button.GetComponentInChildren<TextMeshProUGUI>().text = "Presets";
            }
            return button;
        }

        internal static void Open(BuildingCity source)
        {
            var overlays = OverlayManager.Instance;
            if (source == null || Plugin.Book == null || !overlays.IsOpen()) return;
            // Close the worker panel to release its building-specific input prompts.
            overlays.CloseOverlay();
            overlays.OverlayVariable((BuildingWorld)null, new List<UIBuildingOverlayElement>(), "Worker Presets");
            var root = new GameObject("Worker Presets", typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(overlays.GetOverlaySelection(), false);
            var group = root.GetComponent<VerticalLayoutGroup>();
            group.childControlWidth = group.childForceExpandWidth = group.childControlHeight = true;
            group.childForceExpandHeight = false;
            group.spacing = 4;
            var panel = root.AddComponent<PresetPanel>();
            panel.source = source;
            panel.book = Plugin.Book;
        }

        // The game activates its selection canvas after the opening transition.
        private void Start() => Slots();

        private void Clear(string header, Action back)
        {
            GetComponentInParent<UIOverlayVariable>().SetHeaderText(header);
            var size = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = PanelWidth(Screen.width / Scale);
            backAction = back;
            hints = null;
            foreach (Transform item in transform)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        private TextMeshProUGUI Text(string value)
        {
            var template = Resources.Load<GameObject>("UI/OverlayElementText").GetComponentInChildren<TextMeshProUGUI>(true);
            var label = Instantiate(template, transform, false);
            label.transform.localScale = Vector3.one;
            label.margin = new Vector4(4, 0, 4, 0);
            label.text = value;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private Button ActionButton(string title, Action action) => Button(transform, title, () =>
        {
            if (book == Plugin.Book && isActiveAndEnabled) action();
        });

        private void Focus(Button first)
        {
            hints = Text("");
            Prompts();
            Canvas.ForceUpdateCanvases();
            GamepadManager.Instance.SetGameObjectToSelect(first.gameObject, true);
            EventSystem.current?.SetSelectedGameObject(first.gameObject);
        }

        private void OnEnable() => GamepadManager.OnControlSchemeChanged += SchemeChanged;
        private void OnDisable() => GamepadManager.OnControlSchemeChanged -= SchemeChanged;
        private void SchemeChanged(string current, string previous) => Prompts();

        private void Prompts()
        {
            if (hints == null) return;
            string Icon(string action, string caption)
            {
                string token = "$" + action + "$";
                string icon = Helper.Instance.ReplaceStringWithInputIcon(token);
                if (icon != token) return icon + " " + caption;
                // UI/Submit uses the Input System's generic */{Submit} binding.
                // Resolve its concrete control on the active device for the native sprite table.
                var manager = GamepadManager.Instance;
                var bound = manager.GetPlayerInput()?.actions.FindAction(action);
                if (bound != null)
                    foreach (var control in bound.controls)
                    {
                        string device = control.device is Gamepad ? "Gamepad" : control.device is Keyboard ? "Keyboard" : null;
                        if (device == null || (device == "Gamepad") != manager.IsGamepadInUse()) continue;
                        var sprite = manager.GetButtonPrompt("<" + device + ">/" + control.name);
                        if (sprite != null) return "<sprite name=" + sprite.name + "> " + caption;
                    }
                return caption;
            }
            hints.text = Icon("UI/Submit", "Select") + "     " + Icon("UI/Cancel", "Back");
        }

        private void Slots(string message = null)
        {
            Clear("Worker Presets", Jobs);
            if (message != null) Text(message);
            Button first = null;
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var layout = book.Slots[slot];
                var button = ActionButton(layout.Name + (layout.Entries == null ? "  (Empty)" : "  |  " + Count(layout.Entries.Count, "building")), () =>
                {
                    index = slot;
                    Manage();
                });
                if (slot == index) first = button;
            }
            ActionButton("Back to jobs", Jobs);
            Focus(first);
        }

        internal bool Back()
        {
            if (backAction == null || book != Plugin.Book || !isActiveAndEnabled) return false;
            backAction();
            return true;
        }

        private void Jobs()
        {
            var view = source != null ? source.GetComponent<UIBuildingOverlay>() : null;
            if (view == null) { OverlayManager.Instance.Cancel(); return; }
            view.Open(true);
            var launcher = OverlayManager.Instance.GetOverlaySelection().GetComponentsInChildren<Button>()
                .FirstOrDefault(b => b.name == "Worker Presets");
            if (launcher != null)
                GamepadManager.Instance.SetGameObjectToSelect(launcher.gameObject, true);
        }

        private void Manage(string message = null)
        {
            Clear(Slot.Name, () => Slots());
            if (message != null) Text(message);
            Button first = null;
            if (Slot.Entries != null) first = ActionButton("Preview layout", () => ShowPreview(new CityStaffing().Plan(Slot), 0));
            var save = ActionButton("Save current staffing", () =>
            {
                if (Slot.Entries == null) Save();
                else
                {
                    Clear(Slot.Name, () => Manage());
                    Text("Replace this layout with the current staffing?");
                    var back = ActionButton("Keep saved layout", () => Manage());
                    ActionButton("Replace layout", Save);
                    Focus(back);
                }
            });
            ActionButton("Rename", Rename);
            ActionButton("Back", () => Slots());
            Focus(first ?? save);
        }

        private void Save()
        {
            var entries = new CityStaffing().Capture();
            if (entries.Count == 0) { Manage("No staffing controls are available."); return; }
            Slot.Entries = entries;
            Slots(Slot.Name + " saved, " + entries.Count + " buildings");
        }

        private void Rename()
        {
            Clear("Rename layout", () => Manage());
            // Reuse the game's text field independently of its unit-rename callbacks.
            var template = OverlayManager.Instance.GetComponentInChildren<UIRename>(true)?.GetComponentInChildren<TMP_InputField>(true);
            TMP_InputField field = null;
            if (template != null)
            {
                field = Instantiate(template, transform);
                field.onValueChanged = new TMP_InputField.OnChangeEvent();
                field.onEndEdit = new TMP_InputField.SubmitEvent();
                field.onSubmit = new TMP_InputField.SubmitEvent();
                field.characterLimit = 20;
                field.lineType = TMP_InputField.LineType.SingleLine;
                field.SetTextWithoutNotify(Slot.Name);
                var size = field.GetComponent<LayoutElement>() ?? field.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 46;
                field.gameObject.SetActive(true);
            }
            string draft = Slot.Name;
            var cycle = ActionButton("Choose name", () => {});
            cycle.onClick.RemoveAllListeners();
            cycle.onClick.AddListener(() =>
            {
                draft = Names[(Array.IndexOf(Names, draft) + 1) % Names.Length];
                if (field != null) field.SetTextWithoutNotify(draft);
                else cycle.GetComponentInChildren<TextMeshProUGUI>().text = draft;
            });
            ActionButton("Save name", () =>
            {
                string value = field != null ? field.text : draft;
                value = new string(value.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(20).ToArray()).Trim();
                if (value.Length == 0 || string.Equals(value, book.Slots[1 - index].Name, StringComparison.OrdinalIgnoreCase))
                {
                    Manage("Choose a distinct, non-empty name.");
                    return;
                }
                Slot.Name = value;
                Manage();
            });
            ActionButton("Back", () => Manage());
            Focus(cycle);
        }

        private void ShowPreview(Preview plan, int page, string message = null, int pageDirection = 0)
        {
            Clear(Slot.Name + " - Preview", () => Manage());
            int changes = plan.Rows.Count(row => row.Changes);
            int skipped = plan.Rows.Count(row => row.Building == null);
            if (message != null) Text(message);
            string summary = changes == 0 ? "No assignments to change" : Count(changes, "building") + " will change";
            if (skipped > 0) summary += "  |  " + skipped + " skipped";
            Text(summary);
            var shortages = new List<string>();
            if (plan.WorkersShort > 0) shortages.Add(Count(plan.WorkersShort, "worker"));
            if (plan.CarriersShort > 0) shortages.Add(Count(plan.CarriersShort, "carrier"));
            if (shortages.Count > 0)
                Text("<color=#FFE295><sprite name=IconWorking> Short " + string.Join(" and ", shortages) + "</color>");
            var rows = plan.Rows.OrderByDescending(row => row.Changes || row.Building == null).ToArray();
            var duplicates = rows.GroupBy(r => r.BuildingName).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToHashSet();
            int pageSize = PreviewRows(Screen.height / Scale);
            int pages = Math.Max(1, (rows.Length + pageSize - 1) / pageSize);
            page = Math.Max(0, Math.Min(page, pages - 1));
            TableRow("Building", "Now", "After", null, true);
            foreach (var row in rows.Skip(page * pageSize).Take(pageSize))
            {
                string detail = duplicates.Contains(row.BuildingName) ? "Plot " + row.Plot : "";
                if (row.Building == null) detail = JoinDetail(detail, "Skipped");
                else if (row.Target != row.Saved.Amount) detail = JoinDetail(detail, "Limited");
                TableRow(row.BuildingName, row.Building?.AmountText(row.Building.Current) ?? "-",
                    row.Building?.AmountText(row.Target) ?? "-", detail, false, row.Changes);
            }
            Button pageFocus = null;
            if (pages > 1)
            {
                var bar = new GameObject("Pages", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                bar.transform.SetParent(transform, false);
                bar.GetComponent<HorizontalLayoutGroup>().spacing = 6;
                var previous = Button(bar.transform, "Previous", () => ShowPreview(plan, page - 1, pageDirection: -1));
                var next = Button(bar.transform, $"{page + 1}/{pages}  Next", () => ShowPreview(plan, page + 1, pageDirection: 1));
                previous.interactable = page > 0;
                next.interactable = page + 1 < pages;
                if (pageDirection != 0)
                    pageFocus = next.interactable && (pageDirection > 0 || !previous.interactable) ? next : previous;
            }
            var back = ActionButton("Back", () => Manage());
            if (changes > 0) ActionButton("Apply " + Count(changes, "change"), () => Confirm(plan));
            Focus(pageFocus ?? back);
        }

        private static string Count(int n, string noun) => n + " " + noun + (n == 1 ? "" : "s");
        private static string JoinDetail(string a, string b) => a.Length == 0 ? b : a + "  |  " + b;

        private void TableRow(string title, string current, string target, string detail, bool header, bool changes = false)
        {
            var row = new GameObject("Staffing row", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            row.transform.SetParent(transform, false);
            var size = row.GetComponent<LayoutElement>();
            size.preferredHeight = size.minHeight = header ? 30 : string.IsNullOrEmpty(detail) ? 34 : 54;
            var background = row.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = header ? new Color32(62, 35, 68, 255) : new Color32(91, 54, 99, 255);
            var template = Resources.Load<GameObject>("UI/OverlayElementText").GetComponentInChildren<TextMeshProUGUI>(true);
            void Cell(string value, float left, float right, TextAlignmentOptions alignment, bool accent = false)
            {
                var label = Instantiate(template, row.transform, false);
                label.transform.localScale = Vector3.one;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(left, 0);
                rect.anchorMax = new Vector2(right, 1);
                rect.offsetMin = new Vector2(8, 0);
                rect.offsetMax = new Vector2(-8, 0);
                label.margin = Vector4.zero;
                label.text = value;
                label.alignment = alignment;
                label.enableAutoSizing = true;
                label.fontSizeMax = header ? 26 : 30;
                label.fontSizeMin = 15;
                label.enableWordWrapping = true;
                label.color = header || accent ? new Color32(255, 226, 149, 255) : new Color32(255, 255, 255, 255);
                label.raycastTarget = false;
            }
            if (!string.IsNullOrEmpty(detail)) title += "\n<size=84%><color=#E7D4E9>" + detail + "</color></size>";
            Cell(title, 0, .62f, TextAlignmentOptions.MidlineLeft);
            Cell(current, .62f, .81f, TextAlignmentOptions.Center);
            Cell(target, .81f, 1, TextAlignmentOptions.Center, changes);
        }

        private void Confirm(Preview seen)
        {
            var city = new CityStaffing();
            var current = city.Plan(Slot);
            if (!seen.Matches(current))
            {
                ShowPreview(current, 0, "Staffing changed. Review again.");
                return;
            }
            try { city.Apply(current); }
            catch (Exception e)
            {
                Debug.LogWarning("Worker Presets stopped applying: " + e);
                ShowPreview(new CityStaffing().Plan(Slot), 0, "Apply stopped. Review remaining changes.");
                return;
            }
            var remaining = new CityStaffing().Plan(Slot);
            if (remaining.Rows.Any(r => r.Changes))
                ShowPreview(remaining, 0, "Some assignments are still limited.");
            else Manage("Layout applied, " + current.Rows.Count(r => r.Changes) + " changed");
        }
    }
}
