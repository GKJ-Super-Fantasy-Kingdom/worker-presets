using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace WorkerPresets
{
    internal sealed class CityStaffing
    {
        private readonly Dictionary<string, BuildingCity> sources = new Dictionary<string, BuildingCity>();
        internal readonly Dictionary<string, Workplace> Sites = new Dictionary<string, Workplace>();
        private int otherWorkers, otherCarriers;

        internal static string Key(BuildingCity building)
        {
            var point = building.transform.position;
            return Workplace.Plot(building.name.Replace("(Clone)", "").Trim(), point.x, point.y);
        }

        private static bool HasWorkerControl(BuildingCity building)
        {
            var elements = building.GetComponent<UIBuildingOverlay>()?.buttons;
            if (elements == null) return false;
            foreach (var element in elements)
            {
                if (element is UIBuildingOverlayElementWorker) return true;
                // A conditional panel can stop drawing before the staffing row.
                if (element is UIBuildingOverlayElementVein || element is UIBuildingOverlayElementWaterExpedition)
                    return false;
            }
            return false;
        }

        private static bool Specialist(BuildingCity b) =>
            b.GetBuildingSize() == BuildingSize.Small && b.CanUpgradeBeActivated();

        private static int Current(BuildingCity b) => Specialist(b) ? (b.IsUpgradeActive() ? 1 : 0) : b.GetPossibleWorkers();

        internal CityStaffing()
        {
            var city = CityManager.Instance;
            var buildings = city.GetBuildings().Concat(city.GetOutposts())
                .Where(b => b != null && b.isActiveAndEnabled && !b.ShouldRemove()).Distinct();
            foreach (var group in buildings.GroupBy(Key))
            {
                foreach (var b in group)
                {
                    bool specialist = Specialist(b);
                    if (group.Count() != 1 || !HasWorkerControl(b) || (!specialist && b.GetMaxWorkers() < 1))
                    {
                        if (b.CanCarrierWork()) otherCarriers += b.GetPossibleJobs();
                        else otherWorkers += b.GetPossibleJobs();
                        continue;
                    }
                    int max = specialist ? 1 : (b.AssignJobsIndividually() ? 9 : b.GetMaxWorkers());
                    sources.Add(group.Key, b);
                    var point = b.transform.position;
                    Sites.Add(group.Key, new Workplace {
                        Key = group.Key, Title = $"{b.GetTitle()} ({point.x:0.##}, {point.y:0.##})",
                        Current = Current(b), Max = max,
                        Min = !specialist && b.AssignJobsIndividually() ? Math.Min(max, b.GetCurrentlyWorking()) : 0,
                        Carrier = b.CanCarrierWork(), Individual = !specialist && b.AssignJobsIndividually(),
                        Specialist = specialist,
                        CrewSize = specialist ? 1 : b.GetWorkerIncrementAmount(),
                        JobCapacity = b is BuildingGeneratingElves ? 0 : (specialist ? 1 : b.GetMaxWorkers())
                    });
                }
            }
        }

        internal List<Entry> Capture() => Sites.Values.OrderBy(b => b.Key, StringComparer.Ordinal)
            .Select(b => b.Capture()).ToList();

        internal Preview Plan(Layout layout) => new Preview(layout, Sites, JobManager.Instance.GetWorkerAmount(),
            JobManager.Instance.GetCarrierAmount(), otherWorkers, otherCarriers);

        private static readonly AccessTools.FieldRef<UIBuildingOverlayElementWorker, BuildingCity> RowBuilding =
            AccessTools.FieldRefAccess<UIBuildingOverlayElementWorker, BuildingCity>("m_Building");
        private static readonly AccessTools.FieldRef<UIBuildingOverlayElementWorker, Transform> RowView =
            AccessTools.FieldRefAccess<UIBuildingOverlayElementWorker, Transform>("m_OverlayElement");

        internal void Apply(Preview plan)
        {
            // ChangeAmount updates a view as well as the building. Give it a private,
            // inactive instance of its own prefab so no visible worker row gets rebound.
            var view = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("UI/OverlayElementWorker"));
            try
            {
                view.SetActive(false);
                var row = new UIBuildingOverlayElementWorker();
                RowView(row) = view.transform;
                foreach (var change in plan.InApplyOrder())
                {
                    var b = sources[change.Saved.Key];
                    if (change.Saved.Removed || b == null || b.ShouldRemove()) continue;
                    ApplyRow(row, b, change.Target);
                }
            }
            finally { UnityEngine.Object.Destroy(view); }
        }

        internal static void ApplyRow(UIBuildingOverlayElementWorker row, BuildingCity building, int target)
        {
            RowBuilding(row) = building;
            int before = Current(building);
            int direction = Math.Sign(target - before);
            // Each native +/- click can change jobs or consume resources. Re-read its
            // result so a floor, cap or callback cannot cause a retry loop.
            int steps = Math.Abs(target - before);
            for (int i = 0; i < steps; i++)
            {
                row.ChangeAmount(direction);
                int after = Current(building);
                if (direction * (target - after) <= 0 || direction * (after - before) <= 0) break;
                before = after;
            }
        }
    }
}
