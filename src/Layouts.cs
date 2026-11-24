using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorkerPresets
{
    internal sealed class Entry
    {
        public string Key, Title;
        public int Amount;
        public bool Removed, Specialist;
    }

    internal sealed class Layout
    {
        public string Name;
        // Null distinguishes a slot that has never been saved.
        public List<Entry> Entries;
    }

    internal sealed class LayoutBook
    {
        public Layout[] Slots = { new Layout { Name = "Day Shift" }, new Layout { Name = "Night Shift" } };

        internal void Remove(string key)
        {
            foreach (var slot in Slots)
                if (slot.Entries != null)
                    foreach (var entry in slot.Entries.Where(e => e.Key == key))
                        entry.Removed = true;
        }

        internal bool Valid() => Slots?.Length == 2 && Slots.All(slot => slot != null &&
            !string.IsNullOrWhiteSpace(slot.Name) && slot.Name.Length <= 20 &&
            (slot.Entries == null || slot.Entries.All(e => e != null && !string.IsNullOrEmpty(e.Key) &&
                !string.IsNullOrEmpty(e.Title) && e.Amount >= 0) &&
                slot.Entries.Select(e => e.Key).Distinct().Count() == slot.Entries.Count));
    }

    internal sealed class Workplace
    {
        public string Key, Title;
        public int Current, Min, Max, JobCapacity, CrewSize = 1;
        public bool Carrier, Specialist, Individual;

        // The native save stores a prefab and world XY. Supplier IDs are regenerated on load.
        internal static string Plot(string prefab, float x, float y) =>
            string.Format(CultureInfo.InvariantCulture, "{0}@{1:R},{2:R}", prefab, x, y);

        internal Entry Capture() => new Entry { Key = Key, Title = Title, Amount = Current, Specialist = Specialist };
        internal string AmountText(int amount) => Individual && amount == 9 ? "∞" : amount.ToString();
    }

    internal sealed class Change
    {
        internal Entry Saved;
        internal Workplace Building;
        internal int Target;
        internal bool Changes => Building != null && Building.Current != Target;
    }

    internal sealed class Preview
    {
        internal readonly List<Change> Rows = new List<Change>();
        internal readonly int WorkersShort, CarriersShort;

        internal Preview(Layout layout, IReadOnlyDictionary<string, Workplace> buildings,
            int workers, int carriers, int otherWorkerJobs = 0, int otherCarrierJobs = 0)
        {
            var targets = new Dictionary<string, int>();
            foreach (var entry in layout.Entries)
            {
                var row = new Change { Saved = entry };
                if (!entry.Removed && buildings.TryGetValue(entry.Key, out var site) && site.Specialist == entry.Specialist)
                {
                    row.Building = site;
                    row.Target = Math.Max(site.Min, Math.Min(site.Max, entry.Amount));
                    targets.Add(site.Key, row.Target);
                }
                Rows.Add(row);
            }
            foreach (var site in buildings.Values)
            {
                int amount = targets.TryGetValue(site.Key, out int target) ? target : site.Current;
                int jobs = Math.Min(site.JobCapacity, amount);
                if (jobs > 0) jobs = Math.Max(site.CrewSize, jobs);
                if (site.Carrier) otherCarrierJobs += jobs;
                else otherWorkerJobs += jobs;
            }
            WorkersShort = Math.Max(0, otherWorkerJobs - workers);
            CarriersShort = Math.Max(0, otherCarrierJobs - carriers);
        }

        // Redraw and ask again if the world no longer matches the displayed preview.
        internal bool Matches(Preview other) => WorkersShort == other.WorkersShort &&
            CarriersShort == other.CarriersShort && Rows.Count == other.Rows.Count &&
            Rows.Zip(other.Rows, (a, b) => a.Saved.Key == b.Saved.Key && a.Target == b.Target &&
                a.Building?.Current == b.Building?.Current).All(equal => equal);

        internal IEnumerable<Change> InApplyOrder() => Rows.Where(row => row.Changes)
            .OrderBy(row => row.Target > row.Building.Current);
    }
}
