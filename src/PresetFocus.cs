using SuperFantasyKingdom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkerPresets
{
    internal sealed class PresetFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private static PresetFocus keyboardSelection;
        private Button button;
        private GameObject outline;
        private bool pointer;
        private float submitUntil;

        internal void Draw(Sprite nativeFrame = null)
        {
            button = GetComponent<Button>();
            if (nativeFrame == null) nativeFrame = button.spriteState.selectedSprite;
            if (nativeFrame == null) return;

            outline = new GameObject("Focus outline", typeof(RectTransform), typeof(Image));
            var image = outline.GetComponent<Image>();
            image.sprite = nativeFrame;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = button.image.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            var frame = image.rectTransform;
            frame.SetParent(transform, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (outline == null) return;
            bool selected = EventSystem.current?.currentSelectedGameObject == gameObject;
            bool selectedByInput = GamepadManager.Instance.IsGamepadInUse() ? selected
                : keyboardSelection != null ? keyboardSelection == this && selected : pointer;
            bool show = button.IsInteractable() && (selectedByInput || selected && Time.unscaledTime < submitUntil);
            if (outline.activeSelf != show) outline.SetActive(show);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!GamepadManager.Instance.IsGamepadInUse()) keyboardSelection = null;
            pointer = true;
            Refresh();
        }
        public void OnPointerExit(PointerEventData eventData) { pointer = false; Refresh(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnPointerEnter(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!isActiveAndEnabled || !button.IsInteractable()) return;
            submitUntil = Time.unscaledTime + Mathf.Max(.1f, button.colors.fadeDuration);
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is AxisEventData && !GamepadManager.Instance.IsGamepadInUse()) keyboardSelection = this;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (keyboardSelection == this) keyboardSelection = null;
            submitUntil = 0;
            Refresh();
        }

        private void OnDisable()
        {
            if (keyboardSelection == this) keyboardSelection = null;
            pointer = false;
            submitUntil = 0;
        }
    }
}
