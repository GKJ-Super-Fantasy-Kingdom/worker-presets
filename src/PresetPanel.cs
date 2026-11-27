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
        private const int LayoutsPerPage = 2;
        private BuildingCity source;
        private LayoutBook book;
        private int index;
        private Layout Slot => book.Slots[index];
        private Action backAction;
        private RectTransform backdrop;
        private Button backTemplate;
        private Button actionTemplate;
        private static readonly Color32 Gold = new Color32(255, 226, 149, 255);
        private static readonly Color32 Linen = new Color32(241, 222, 241, 255);
        private static readonly Color32 MutedLabel = new Color32(194, 174, 194, 255);
        private static readonly string[] Names =
            { LayoutBook.FirstDefaultName, LayoutBook.SecondDefaultName, "Harvest", "Construction", "Supplies", "Recovery" };

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
            var button = WithNativeFocus(Button(panel, "Worker Presets", () => Open(source)));
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
            group.spacing = 5;
            group.padding = new RectOffset(5, 5, 7, 0);
            var panel = root.AddComponent<PresetPanel>();
            panel.source = source;
            panel.book = Plugin.Book;
        }

        // The game activates its selection canvas after the opening transition.
        private void Start()
        {
            var frame = transform.parent.parent.parent;
            var scrim = new GameObject("Worker Presets backdrop", typeof(RectTransform),
                typeof(LayoutElement), typeof(Image));
            backdrop = (RectTransform)scrim.transform;
            backdrop.SetParent(frame, false);
            backdrop.SetAsFirstSibling();
            scrim.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = scrim.GetComponent<Image>();
            image.color = new Color32(15, 12, 22, 126);
            image.raycastTarget = false;
            backTemplate = OverlayManager.Instance.GetMerchant()?.transform.Find("Panel/Buttons/Cancel/Button")?.GetComponent<Button>();
            actionTemplate = Resources.Load<GameObject>("UI/OverlayMessage")?.transform.Find("Buttons/BuyButton")?.GetComponent<Button>();
            Slots();
        }

        private void LateUpdate()
        {
            if (backdrop == null) return;
            var parent = (RectTransform)backdrop.parent;
            var canvas = backdrop.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                new Vector2(Screen.width, Screen.height), camera, out var max);
            backdrop.localPosition = (min + max) * .5f;
            backdrop.sizeDelta = max - min;
        }

        private void HideBackdrop()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop.gameObject);
        }

        private void Clear(string header, Action back)
        {
            GetComponentInParent<UIOverlayVariable>().SetHeaderText(header);
            var size = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = PanelWidth(Screen.width / Scale);
            backAction = back;
            foreach (Transform item in transform)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        private TextMeshProUGUI Text(string value)
        {
            var label = Label(transform);
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

        private static TextMeshProUGUI Label(Transform parent) =>
            Instantiate(Resources.Load<GameObject>("UI/OverlayElementText")
                .GetComponentInChildren<TextMeshProUGUI>(true), parent, false);

        private Button ActionButton(string title, Action action, Transform parent = null) => Button(parent ?? transform, title, () =>
        {
            if (book == Plugin.Book && isActiveAndEnabled) action();
        });

        private Button MenuButton(string title, Action action) => WithNativeFocus(ActionButton(title, action));

        private static Button WithNativeFocus(Button button)
        {
            var sprites = button.spriteState;
            var frame = sprites.selectedSprite;
            // Keep the native resting, pressed and disabled art. The edge-only frame owns focus.
            sprites.selectedSprite = button.image.sprite;
            button.spriteState = sprites;
            button.gameObject.AddComponent<PresetFocus>().Draw(frame);
            return button;
        }

        private void Focus(Button first)
        {
            Canvas.ForceUpdateCanvases();
            GamepadManager.Instance.SetGameObjectToSelect(first.gameObject, true);
            EventSystem.current?.SetSelectedGameObject(first.gameObject);
        }

        private void OnEnable()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(true);
        }

        private void OnDisable() => HideBackdrop();

        private Transform ButtonRow(string name, int gap = 10, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(transform, false);
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.spacing = gap;
            group.childAlignment = alignment;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return row.transform;
        }

        private void StyleRed(Button button, int height)
        {
            var size = button.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;

            // Use the cancel plate with dark edges. PresetFocus draws the bright edge on demand.
            var template = backTemplate != null ? backTemplate : button;
            var plate = button.image;
            if (template.targetGraphic is Image source)
            {
                plate.type = source.type;
                plate.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            }
            var darkPlate = template.spriteState.highlightedSprite;
            if (darkPlate != null) plate.sprite = darkPlate;
            plate.overrideSprite = null;
            plate.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color32(184, 191, 225, 255);
            colors.highlightedColor = colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color32(148, 153, 181, 255);
            colors.disabledColor = new Color32(169, 158, 205, 112);
            colors.fadeDuration = .075f;
            button.colors = colors;
            button.gameObject.AddComponent<PresetFocus>().Draw(template.spriteState.selectedSprite);
        }

        private Button FooterButton(string title, Action action, Transform parent = null, bool back = false)
        {
            var button = ActionButton(title, action, parent);
            var template = back ? backTemplate : actionTemplate;
            if (back) StyleRed(button, 36);
            else
            {
                var size = button.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 38;
                if (!(template?.targetGraphic is Image sourceImage) || sourceImage.sprite == null)
                {
                    button.image.color = new Color32(255, 203, 165, 255);
                    return WithNativeFocus(button);
                }

                // Keep the light native action plate, with a dark edge and the same focus ring as Back.
                var plate = button.image;
                plate.overrideSprite = null;
                var sprites = template.spriteState;
                var frame = sprites.selectedSprite;
                var darkPlate = sprites.highlightedSprite;
                plate.sprite = darkPlate != null ? darkPlate : sourceImage.sprite;
                plate.type = sourceImage.type;
                plate.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
                Color tint = sourceImage.color * (235f / 255f);
                tint.a = sourceImage.color.a;
                plate.color = tint;
                button.transition = Selectable.Transition.SpriteSwap;
                sprites.highlightedSprite = sprites.selectedSprite = plate.sprite;
                button.spriteState = sprites;
                button.gameObject.AddComponent<PresetFocus>().Draw(frame);
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            var nativeLabel = template?.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (nativeLabel != null)
            {
                label.font = nativeLabel.font;
                label.fontSharedMaterial = nativeLabel.fontSharedMaterial;
                label.color = nativeLabel.color;
            }
            label.alignment = TextAlignmentOptions.Center;
            label.fontSizeMin = 14;
            label.fontSizeMax = back ? 20 : 25;
            return button;
        }

        private static void Place(RectTransform rect, float top, float bottom, float inset)
        {
            rect.anchorMin = new Vector2(0, bottom);
            rect.anchorMax = new Vector2(1, top);
            rect.offsetMin = new Vector2(inset, 0);
            rect.offsetMax = new Vector2(-inset, 0);
        }

        private Button SlotButton(Transform row, int slot)
        {
            var layout = book.Slots[slot];
            var button = ActionButton(layout.Name, () =>
            {
                index = slot;
                Manage();
            }, row);
            var size = button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 1;
            bool workerIcon = slot % LayoutsPerPage == 0;
            StyleRed(button, 142);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            Place(label.rectTransform, .48f, .27f, 12);
            label.fontSizeMax = 30;

            var icon = Label(button.transform);
            string sprite = !workerIcon ? "IconWorking" :
                RaceManager.Instance.GetRace() == Races.Undead ? "IconWorkerUndead" : "IconWorkerBig";
            icon.text = "<sprite name=" + sprite + ">";
            icon.alignment = TextAlignmentOptions.Center;
            icon.fontSize = 54;
            icon.raycastTarget = false;
            Place(icon.rectTransform, .94f, .49f, 8);

            var details = Label(button.transform);
            details.text = layout.Entries == null ? "Empty" : Count(layout.Entries.Count, "building");
            details.color = Linen;
            details.alignment = TextAlignmentOptions.Center;
            details.fontSize = 22;
            details.raycastTarget = false;
            Place(details.rectTransform, .27f, .09f, 8);
            return button;
        }

        private void Slots(string message = null, int? requestedPage = null)
        {
            Clear("Worker Presets", Jobs);
            if (message != null) Text(message);
            int slotCount = book.Slots.Length;
            Button first = null;
            if (slotCount == 0) Text("No layouts yet.");
            else
            {
                int pages = (slotCount - 1) / LayoutsPerPage + 1;
                int page = Math.Min(requestedPage ?? index / LayoutsPerPage, pages - 1);
                int firstSlot = page * LayoutsPerPage;
                var row = ButtonRow("Layout slots", 12);
                first = SlotButton(row, firstSlot);
                if (firstSlot + 1 < slotCount)
                {
                    var second = SlotButton(row, firstSlot + 1);
                    if (index == firstSlot + 1) first = second;
                }
                else
                {
                    var empty = new GameObject("Empty card space", typeof(RectTransform), typeof(LayoutElement));
                    empty.transform.SetParent(row, false);
                    var size = empty.GetComponent<LayoutElement>();
                    size.minWidth = size.preferredWidth = size.flexibleWidth = 1;
                }
                if (pages > 1) PageButtons(page, pages, next => Slots(requestedPage: next));
            }
            var footer = ButtonRow("Picker actions", alignment: TextAnchor.MiddleLeft);
            FooterButton("Back to jobs", Jobs, footer, back: true);
            var add = FooterButton("Add layout", () =>
            {
                index = book.Add();
                Manage();
            }, footer);
            Focus(first ?? add);
        }

        internal bool Back()
        {
            if (backAction == null || book != Plugin.Book || !isActiveAndEnabled) return false;
            backAction();
            return true;
        }

        private void Jobs()
        {
            HideBackdrop();
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
            if (Slot.Entries != null) first = MenuButton("Preview layout", () => ShowPreview(new CityStaffing().Plan(Slot), 0));
            var save = MenuButton("Save current staffing", () =>
            {
                if (Slot.Entries == null) Save();
                else
                {
                    Clear(Slot.Name, () => Manage());
                    Text("Replace this layout with the current staffing?");
                    var back = FooterButton("Keep saved layout", () => Manage(), back: true);
                    FooterButton("Replace layout", Save);
                    Focus(back);
                }
            });
            MenuButton("Rename", Rename);
            MenuButton("Remove layout", ConfirmRemoval);
            FooterButton("Back", () => Slots(), back: true);
            Focus(first ?? save);
        }

        private void ConfirmRemoval()
        {
            Clear(Slot.Name, () => Manage());
            Text("Remove this layout?");
            var keep = FooterButton("Keep layout", () => Manage(), back: true);
            FooterButton("Confirm removal", () =>
            {
                string name = Slot.Name;
                book.RemoveAt(index);
                index = Math.Min(index, Math.Max(0, book.Slots.Length - 1));
                Slots(name + " removed");
            });
            Focus(keep);
        }

        private void Save()
        {
            var entries = new CityStaffing().Capture();
            if (entries.Count == 0) { Manage("No staffing controls are available."); return; }
            Slot.Entries = entries;
            Slots(Slot.Name + " saved, " + Count(entries.Count, "building"));
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
            var suggestions = Names.Append(book.NextLayoutName(index + 1))
                .Where(name => !book.NameInUse(name, index))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var cycle = MenuButton("Choose name", () => {});
            cycle.onClick.RemoveAllListeners();
            cycle.onClick.AddListener(() =>
            {
                draft = suggestions[(Array.IndexOf(suggestions, draft) + 1) % suggestions.Length];
                if (field != null) field.SetTextWithoutNotify(draft);
                else cycle.GetComponentInChildren<TextMeshProUGUI>().text = draft;
            });
            FooterButton("Save name", () =>
            {
                string value = field != null ? field.text : draft;
                value = new string(value.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(20).ToArray()).Trim();
                if (value.Length == 0 || book.NameInUse(value, index))
                {
                    Manage("Choose a distinct, non-empty name.");
                    return;
                }
                Slot.Name = value;
                Manage();
            });
            FooterButton("Back", () => Manage(), back: true);
            Focus(cycle);
        }

        private void ShowPreview(Preview plan, int page, string message = null, int pageDirection = 0)
        {
            Clear(Slot.Name + " - Preview", () => Manage());
            int changes = plan.Rows.Count(row => row.Changes);
            int skipped = plan.Rows.Count(row => row.Building == null);
            if (message != null) Text(message);
            if (changes > 0)
            {
                string summary = Count(changes, "building") + " will change";
                if (skipped > 0) summary += "  |  " + skipped + " skipped";
                Text(summary).color = Linen;
            }
            var shortages = new List<string>();
            if (plan.WorkersShort > 0) shortages.Add(Count(plan.WorkersShort, "worker"));
            if (plan.CarriersShort > 0) shortages.Add(Count(plan.CarriersShort, "carrier"));
            if (shortages.Count > 0)
                Text("<sprite name=IconWorking> Short " + string.Join(" and ", shortages)).color = Gold;
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
                var (previous, next) = PageButtons(page, pages,
                    target => ShowPreview(plan, target, pageDirection: Math.Sign(target - page)));
                if (pageDirection != 0)
                    pageFocus = next.interactable && (pageDirection > 0 || !previous.interactable) ? next : previous;
            }
            if (changes == 0) Text(UnavailableReason(plan.Rows)).color = Linen;
            var footer = ButtonRow("Preview actions", alignment: TextAnchor.MiddleLeft);
            var back = FooterButton("Back", () => Manage(), footer, back: true);
            var backSize = back.GetComponent<LayoutElement>();
            backSize.minWidth = backSize.preferredWidth = 112;
            backSize.flexibleWidth = 0;
            var apply = FooterButton(changes == 0 ? "Apply" : "Apply " + Count(changes, "change"), () => Confirm(plan), footer);
            apply.interactable = changes > 0;
            Focus(pageFocus ?? back);
        }

        internal static string UnavailableReason(IReadOnlyList<Change> rows)
        {
            if (rows.Count == 0) return "This layout has no saved jobs.";
            if (rows.All(row => row.Building == null)) return "All saved buildings are unavailable.";
            if (rows.Any(row => row.Building != null && row.Target != row.Saved.Amount))
                return "Current job limits prevent further changes.";
            return rows.Any(row => row.Building == null)
                ? "Available jobs already match. Missing buildings skipped."
                : "Staffing already matches this layout.";
        }

        private (Button Previous, Button Next) PageButtons(int page, int pages, Action<int> open)
        {
            var bar = ButtonRow("Pages", 8);
            var previous = ActionButton("Previous", () => open(page - 1), bar);
            PageNumber(bar, page, pages);
            var next = ActionButton("Next", () => open(page + 1), bar);
            previous.interactable = page > 0;
            next.interactable = page + 1 < pages;
            foreach (var button in new[] { previous, next })
            {
                StyleRed(button, 34);
                if (!button.interactable) button.GetComponentInChildren<TextMeshProUGUI>().color = MutedLabel;
                var size = button.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 80;
            }
            return (previous, next);
        }

        private static void PageNumber(Transform bar, int page, int pages)
        {
            var label = Label(bar);
            label.text = $"{page + 1} / {pages}";
            label.fontSize = 23;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Linen;
            label.raycastTarget = false;
            var size = label.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 78;
            size.flexibleWidth = 0;
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
            if (changes)
            {
                var marker = new GameObject("Changed assignment", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                marker.transform.SetParent(row.transform, false);
                marker.raycastTarget = false;
                marker.color = Gold;
                var rect = marker.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.up;
                rect.offsetMin = new Vector2(0, 3);
                rect.offsetMax = new Vector2(3, -3);
            }
            void Cell(string value, float left, float right, TextAlignmentOptions alignment, bool accent = false)
            {
                var label = Label(row.transform);
                label.transform.localScale = Vector3.one;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(left, 0);
                rect.anchorMax = new Vector2(right, 1);
                rect.offsetMin = new Vector2(left == 0 ? 12 : 8, 0);
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
