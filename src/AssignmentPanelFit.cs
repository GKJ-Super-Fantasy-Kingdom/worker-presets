using System.Collections.Generic;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace WorkerPresets
{
    // The native selection frame is shared by several overlays. Apply this fit
    // only while the worker row that requested it is present.
    internal sealed class AssignmentPanelFit : MonoBehaviour
    {
        private RectTransform frame, panel, ribbon;
        private Transform owner, resourceBar;
        private readonly List<Graphic> counters = new List<Graphic>();
        private readonly Vector3[] corners = new Vector3[4];
        private float factor = 1, shift, nextCheck;

        internal static void Bind(Transform selection, Transform launcher)
        {
            var panel = selection.parent as RectTransform;
            var frame = panel?.parent as RectTransform;
            var ribbon = frame?.Find("HeaderContainer/Header") as RectTransform;
            if (!frame || !ribbon) return;
            var fit = frame.GetComponent<AssignmentPanelFit>() ?? frame.gameObject.AddComponent<AssignmentPanelFit>();
            fit.Restore();
            fit.frame = frame;
            fit.panel = panel;
            fit.ribbon = ribbon;
            fit.owner = launcher;
            fit.resourceBar = FindObjectOfType<UIHeader>()?.transform.Find("PanelResources");
            fit.nextCheck = 0;
            fit.enabled = true;
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (frame && (factor != 1 || shift != 0))
            {
                frame.localScale /= factor;
                frame.anchoredPosition -= Vector2.up * shift;
            }
            factor = 1;
            shift = 0;
        }

        private Rect Bounds(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(corners);
            var low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        private void LateUpdate()
        {
            if (!owner || !owner.gameObject.activeInHierarchy) { enabled = false; return; }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .12f;
            var canvas = frame.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var header = Bounds(ribbon, camera);
            var body = Bounds(panel, camera);
            float height = (header.yMax - body.yMin) / factor;
            if (height <= 0) return;
            var safe = Screen.safeArea;
            float upper = safe.yMax, lower = safe.yMin;
            if (resourceBar)
            {
                resourceBar.GetComponentsInChildren(false, counters);
                foreach (var graphic in counters)
                {
                    if (!graphic.isActiveAndEnabled || graphic.color.a <= 0) continue;
                    var rect = Bounds(graphic.rectTransform, camera);
                    if (rect.yMin > safe.center.y && rect.xMax > header.xMin && rect.xMin < header.xMax)
                        upper = Mathf.Min(upper, rect.yMin);
                }
            }
            float margin = 8 * canvas.scaleFactor;
            upper -= margin;
            lower += margin;
            if (upper <= lower) return;
            float fit = Mathf.Min(1, (upper - lower) / height);
            var parent = frame.parent;
            float pixels = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(Vector3.up)).y -
                RectTransformUtility.WorldToScreenPoint(camera, parent.position).y;
            if (pixels <= 0) return;
            float pivot = RectTransformUtility.WorldToScreenPoint(camera, frame.position).y;
            float origin = pivot - shift * pixels;
            float top = origin + (header.yMax - pivot) * fit / factor;
            float bottom = origin + (body.yMin - pivot) * fit / factor;
            float offset = Mathf.Clamp(0, lower - bottom, upper - top) / pixels;
            if (Mathf.Abs(fit - factor) < .001f && Mathf.Abs(offset - shift) < .25f) return;
            frame.localScale *= fit / factor;
            frame.anchoredPosition += Vector2.up * (offset - shift);
            factor = fit;
            shift = offset;
        }
    }
}
