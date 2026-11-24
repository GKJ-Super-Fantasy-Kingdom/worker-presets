using System.Linq;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StaffingChecks")]

namespace WorkerPresets
{
    [BepInPlugin("gkj.sfk.workerpresets", "Worker Presets", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony patches;
        internal static LayoutBook Book { get; private set; }
        internal static void Reset(LayoutBook saved) => Book = saved ?? new LayoutBook();
        internal static bool HasSavedLayouts(LayoutBook book) => book.Slots.Any(slot => slot.Entries != null) ||
            book.Slots[0].Name != "Day Shift" || book.Slots[1].Name != "Night Shift";

        private void Awake()
        {
            patches = new Harmony("gkj.sfk.workerpresets");
            patches.PatchAll(typeof(Plugin).Assembly);
            CityManager.OnBuildingRemoved += Removed;
        }

        private void OnDestroy()
        {
            CityManager.OnBuildingRemoved -= Removed;
            patches?.UnpatchSelf();
            Book = null;
        }

        private static void Removed(BuildingCity building, bool cancelJobs) => Book?.Remove(CityStaffing.Key(building));

        [HarmonyPatch(typeof(UIBuildingOverlayElementWorker), nameof(UIBuildingOverlayElementWorker.Draw), typeof(Building))]
        private static class AssignmentPanel
        {
            private static void Postfix(Building building, bool __runOriginal)
            {
                if (!__runOriginal || Book == null || !(building is BuildingCity city)) return;
                var panel = OverlayManager.Instance.GetOverlaySelection();
                var button = PresetPanel.Button(panel, "Worker Presets", () => PresetPanel.Open(city));
                GamepadManager.Instance.SetGameObjectToSelect(button.gameObject);
            }
        }
    }
}
