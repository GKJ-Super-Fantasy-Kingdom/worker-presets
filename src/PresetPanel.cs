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
        private static readonly string[] Names = { "Day Shift", "Night Shift", "Harvest", "Construction", "Supplies", "Recovery" };

        internal static int PreviewRows(float logicalHeight) =>
            logicalHeight < 420 ? 1 : logicalHeight < 580 ? 2 : MaxPreviewRows;

        internal static float PanelWidth(float logicalWidth) => Math.Max(1, Math.Min(420, logicalWidth - 64));

        private float Scale => GetComponentInParent<Canvas>().rootCanvas.scaleFactor;

        internal static Button Button(Transform parent, string title, Action action)
        {
            var prefab = Resources.Load<GameObject>("UI/OverlayElementButton").transform.Find("Button");
            var button = Instantiate(prefab.GetComponent<Button>(), parent, false);
            button.name = title;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = title;
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
            panel.Slots();
        }

        private void Clear(string header)
        {
            GetComponentInParent<UIOverlayVariable>().SetHeaderText(header);
            var size = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = PanelWidth(Screen.width / Scale);
            hints = null;
            foreach (Transform item in transform)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        private TextMeshProUGUI Text(string value)
        {
            var item = Instantiate(Resources.Load<GameObject>("UI/OverlayElementText"), transform);
            var label = item.GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = value;
            label.fontSize = 21;
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
                return icon == token ? caption : icon + " " + caption;
            }
            hints.text = Icon("UI/Submit", "Select") + "     " + Icon("UI/Cancel", "Close");
        }

        private void Slots(string message = null)
        {
            Clear("Worker Presets");
            if (message != null) Text(message);
            Button first = null;
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var layout = book.Slots[slot];
                var button = ActionButton(layout.Name + (layout.Entries == null ? "  ·  Empty" : ""), () =>
                {
                    index = slot;
                    Manage();
                });
                if (first == null) first = button;
            }
            ActionButton("Back to jobs", () =>
            {
                OverlayManager.Instance.Cancel();
                if (source != null) source.GetComponent<UIBuildingOverlay>()?.Open(true);
            });
            Focus(first);
        }

        private void Manage(string message = null)
        {
            Clear(Slot.Name);
            if (message != null) Text(message);
            Button first = null;
            if (Slot.Entries != null) first = ActionButton("Preview layout", () => ShowPreview(new CityStaffing().Plan(Slot), 0));
            var save = ActionButton("Save current staffing", () =>
            {
                if (Slot.Entries == null) Save();
                else
                {
                    Clear(Slot.Name);
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
            Slots(Slot.Name + " saved · " + entries.Count + " buildings");
        }

        private void Rename()
        {
            Clear("Rename layout");
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
            Clear(Slot.Name + " · Preview");
            var changes = plan.Rows.Count(row => row.Changes);
            int skipped = plan.Rows.Count(row => row.Building == null);
            string summary = $"{changes} to change · {skipped} skipped";
            if (plan.WorkersShort > 0) summary += $"\nShort {plan.WorkersShort} worker(s)";
            if (plan.CarriersShort > 0) summary += $"\nShort {plan.CarriersShort} carrier(s)";
            Text(message == null ? summary : message + "\n" + summary);
            var rows = plan.Rows.OrderByDescending(row => row.Changes || row.Building == null).ToArray();
            int pageSize = PreviewRows(Screen.height / Scale);
            int pages = Math.Max(1, (rows.Length + pageSize - 1) / pageSize);
            page = Math.Min(page, pages - 1);
            Text(string.Join("\n", rows.Skip(page * pageSize).Take(pageSize).Select(Describe)));
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
            if (changes > 0) ActionButton("Confirm apply", () => Confirm(plan));
            Focus(pageFocus ?? back);
        }

        private static string Describe(Change row)
        {
            if (row.Building == null) return row.Saved.Title + " · skipped";
            var site = row.Building;
            string amount = site.AmountText(row.Target);
            if (row.Changes) amount = site.AmountText(site.Current) + " → " + amount;
            if (row.Target != row.Saved.Amount) amount += " (limited)";
            return site.Title + "   " + amount;
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
            else Manage("Layout applied · " + current.Rows.Count(r => r.Changes) + " changed");
        }
    }
}
